using System.IO;
using System.Text;
using FluentAssertions;
using PokeyApp.Transport;
using Xunit;

namespace PokeyApp.Tests.Transport;

public class MessageFramerTests
{
    [Fact]
    public async Task Encode_ThenReadFrame_RoundTrip()
    {
        var payload = Encoding.UTF8.GetBytes("{\"Type\":\"Poke\",\"From\":\"Alice\"}");
        var frame = MessageFramer.Encode(payload);

        using var stream = new MemoryStream(frame);
        var decoded = await MessageFramer.ReadFrameAsync(stream, CancellationToken.None);

        decoded.Should().NotBeNull();
        decoded.Should().Equal(payload);
    }

    [Fact]
    public async Task ReadFrame_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();
        var result = await MessageFramer.ReadFrameAsync(stream, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadFrame_MultipleFrames_ReadsFirst()
    {
        var payload1 = Encoding.UTF8.GetBytes("Frame1");
        var payload2 = Encoding.UTF8.GetBytes("Frame2LongerContent");

        var combined = new MemoryStream();
        combined.Write(MessageFramer.Encode(payload1));
        combined.Write(MessageFramer.Encode(payload2));
        combined.Position = 0;

        var first = await MessageFramer.ReadFrameAsync(combined, CancellationToken.None);
        var second = await MessageFramer.ReadFrameAsync(combined, CancellationToken.None);

        first.Should().Equal(payload1);
        second.Should().Equal(payload2);
    }

    [Fact]
    public void Encode_ProducesCorrectLength()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var frame = MessageFramer.Encode(payload);

        // 4 byte header + 5 byte payload = 9 byte
        frame.Should().HaveCount(9);
    }

    [Fact]
    public async Task ReadFrame_EmptyPayload_Works()
    {
        var payload = Array.Empty<byte>();
        var frame = MessageFramer.Encode(payload);

        using var stream = new MemoryStream(frame);
        var decoded = await MessageFramer.ReadFrameAsync(stream, CancellationToken.None);

        decoded.Should().NotBeNull();
        decoded.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadFrame_ExcessiveSize_ThrowsInvalidData()
    {
        // 2 MB boyutunda header yaz (1 MB limitin üstünde)
        var buffer = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer, 2_000_000);

        using var stream = new MemoryStream(buffer);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => MessageFramer.ReadFrameAsync(stream, CancellationToken.None));
    }
}

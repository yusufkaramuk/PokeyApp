using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using PokeyApp.Infrastructure;
using PokeyApp.Messages;
using PokeyApp.Services;
using PokeyApp.Transport;
using Xunit;

namespace PokeyApp.Tests.Services;

public class PokeServiceTests
{
    private readonly Mock<ITransport> _transportMock;
    private readonly Mock<IConfigurationService> _configMock;
    private readonly PokeService _sut;

    public PokeServiceTests()
    {
        _transportMock = new Mock<ITransport>();
        _configMock = new Mock<IConfigurationService>();

        _configMock.Setup(c => c.Load()).Returns(new AppSettings
        {
            LocalUsername = "TestUser",
            TcpPort = 14191
        });

        _sut = new PokeService(_transportMock.Object, _configMock.Object);
    }

    [Fact]
    public async Task SendPokeAsync_CallsTransportSendAsync()
    {
        byte[]? capturedPayload = null;
        _transportMock
            .Setup(t => t.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], CancellationToken>((p, _) => capturedPayload = p)
            .Returns(Task.CompletedTask);

        await _sut.SendPokeAsync();

        _transportMock.Verify(t => t.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedPayload.Should().NotBeNull();

        var json = Encoding.UTF8.GetString(capturedPayload!);
        var msg = JsonSerializer.Deserialize<PokeMessage>(json)!;
        msg.Type.Should().Be("Poke");
        msg.FromUsername.Should().Be("TestUser");
    }

    [Fact]
    public void FrameReceived_ValidPokeFrame_FiresPokeReceived()
    {
        PokeMessage? received = null;
        _sut.PokeReceived += (_, msg) => received = msg;

        var poke = new PokeMessage { FromUsername = "Ahmet", Timestamp = DateTime.UtcNow };
        var json = JsonSerializer.Serialize(poke);
        var payload = Encoding.UTF8.GetBytes(json);

        // Transport'tan gelen frame event'ini simüle et
        _transportMock.Raise(t => t.FrameReceived += null, _transportMock.Object, payload);

        received.Should().NotBeNull();
        received!.FromUsername.Should().Be("Ahmet");
        received.Type.Should().Be("Poke");
    }

    [Fact]
    public void FrameReceived_InvalidJson_DoesNotThrow()
    {
        var badPayload = Encoding.UTF8.GetBytes("not valid json {{{");

        var act = () => _transportMock.Raise(t => t.FrameReceived += null,
            _transportMock.Object, badPayload);

        act.Should().NotThrow();
    }

    [Fact]
    public void FrameReceived_WrongMessageType_DoesNotFirePokeReceived()
    {
        bool fired = false;
        _sut.PokeReceived += (_, _) => fired = true;

        var other = new { Type = "DiscoveryBeacon", Username = "X" };
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(other));

        _transportMock.Raise(t => t.FrameReceived += null, _transportMock.Object, payload);

        fired.Should().BeFalse();
    }
}

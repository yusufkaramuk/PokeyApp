using PokeyApp.Transport;

namespace PokeyApp.Services;

public interface IConnectionService
{
    TransportState State { get; }
    event EventHandler<TransportState>? StateChanged;
}

public class ConnectionService : IConnectionService
{
    private TransportState _state = TransportState.Disconnected;

    public TransportState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<TransportState>? StateChanged;

    public ConnectionService(ITransport transport)
    {
        transport.StateChanged += (_, state) => State = state;
    }
}

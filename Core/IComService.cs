namespace TcpDebugger.Core;

internal enum ComRole
{
    Server,
    Client,
}

internal sealed class DataReceivedEventArgs : EventArgs
{
    public string SessionId { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public byte[] Payload { get; init; } = Array.Empty<byte>();
}

internal sealed class PeerEventArgs : EventArgs
{
    public string SessionId { get; init; } = string.Empty;

    public string Display { get; init; } = string.Empty;

    public bool Connected { get; init; }

    public string? Reason { get; init; }
}

internal sealed record SessionInfo(string Id, string Display, DateTime JoinedAt);

/// <summary>
/// TCP 通信服务的统一抽象。事件可能在任意线程被触发，订阅方需自行切回 UI 线程。
/// </summary>
internal interface IComService : IDisposable
{
    bool IsRunning { get; }

    int PeerCount { get; }

    event EventHandler<DataReceivedEventArgs>? DataReceived;

    event EventHandler<PeerEventArgs>? PeerChanged;

    event EventHandler<string>? Notified;

    void Start();

    void Stop();

    /// <summary>
    /// 发送数据。sessionId 为 "*" 时广播到全部会话。
    /// </summary>
    void Send(string sessionId, byte[] payload);
}

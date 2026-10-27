using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TcpDebugger.Core;

/// <summary>
/// TCP 服务端：支持多客户端并发接入、按会话单发或广播。
/// </summary>
internal sealed class TcpServerService : IComService
{
    public const string BroadcastId = "*";

    private readonly int _port;
    private readonly ConcurrentDictionary<string, RemoteSession> _sessions = new(StringComparer.Ordinal);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _sequence;
    private int _boundPort;

    public TcpServerService(int port) => _port = port;

    public bool IsRunning => _listener != null && _cts is { IsCancellationRequested: false };

    public int PeerCount => _sessions.Count;

    /// <summary>
    /// 实际绑定的端口。构造时传入 0 表示让系统分配空闲端口，此处可回读到真实值。
    /// </summary>
    public int BoundPort => _boundPort;

    public IEnumerable<SessionInfo> Sessions =>
        _sessions.Values.Select(s => new SessionInfo(s.Id, s.Display, s.JoinedAt)).ToList();

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public event EventHandler<PeerEventArgs>? PeerChanged;

    public event EventHandler<string>? Notified;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        Notify($"开始监听 {IPAddress.Any}:{_boundPort}");
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                if (_listener is null)
                {
                    return;
                }

                client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                Notify("监听中断：" + ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Notify("监听出错：" + ex.Message);
                return;
            }

            Configure(client);

            RemoteSession session;
            if (client.Client.RemoteEndPoint is IPEndPoint endPoint)
            {
                session = new RemoteSession(
                    "S" + Interlocked.Increment(ref _sequence),
                    endPoint.ToString(),
                    DateTime.Now,
                    client);
            }
            else
            {
                session = new RemoteSession(
                    "S" + Interlocked.Increment(ref _sequence),
                    "未知来源",
                    DateTime.Now,
                    client);
            }

            _sessions[session.Id] = session;
            OnPeerChanged(session.Id, session.Display, true, null);

            _ = Task.Run(() => ReadLoopAsync(session, token));
        }
    }

    private async Task ReadLoopAsync(RemoteSession session, CancellationToken token)
    {
        try
        {
            var buffer = new byte[64 * 1024];
            NetworkStream stream = session.Client.GetStream();

            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (read <= 0)
                {
                    OnPeerChanged(session.Id, session.Display, false, "对端关闭连接");
                    break;
                }

                var payload = new byte[read];
                Buffer.BlockCopy(buffer, 0, payload, 0, read);

                DataReceived?.Invoke(this, new DataReceivedEventArgs
                {
                    SessionId = session.Id,
                    Source = session.Display,
                    Payload = payload,
                });
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            OnPeerChanged(session.Id, session.Display, false, ex.Message);
        }
        finally
        {
            Remove(session);
        }
    }

    public void Send(string sessionId, byte[] payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        if (sessionId == BroadcastId)
        {
            foreach (RemoteSession item in _sessions.Values.ToArray())
            {
                _ = WriteAsync(item, payload);
            }

            return;
        }

        if (_sessions.TryGetValue(sessionId, out RemoteSession? target))
        {
            _ = WriteAsync(target, payload);
        }
    }

    /// <summary>
    /// 主动踢掉某个会话。
    /// </summary>
    public void Kick(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out RemoteSession? session))
        {
            Remove(session);
        }
    }

    public void KickAll()
    {
        foreach (RemoteSession session in _sessions.Values.ToArray())
        {
            Remove(session);
        }
    }

    private async Task WriteAsync(RemoteSession session, byte[] payload)
    {
        try
        {
            await session.WriteGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await session.Client.GetStream()
                    .WriteAsync(payload, 0, payload.Length)
                    .ConfigureAwait(false);
            }
            finally
            {
                session.WriteGate.Release();
            }
        }
        catch (Exception ex)
        {
            Remove(session);
            Notify($"向 {session.Display} 发送失败：{ex.Message}");
        }
    }

    private void Remove(RemoteSession session)
    {
        if (_sessions.TryRemove(session.Id, out _))
        {
            OnPeerChanged(session.Id, session.Display, false, "连接已断开");
        }

        session.Dispose();
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // 忽略
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // 忽略
        }

        _listener = null;

        foreach (RemoteSession session in _sessions.Values.ToArray())
        {
            Remove(session);
        }

        _sessions.Clear();
        Notify("已停止监听");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _cts = null;
    }

    private void OnPeerChanged(string id, string display, bool connected, string? reason) =>
        PeerChanged?.Invoke(this, new PeerEventArgs
        {
            SessionId = id,
            Display = display,
            Connected = connected,
            Reason = reason,
        });

    private void Notify(string message) => Notified?.Invoke(this, message);

    private static void Configure(TcpClient client)
    {
        client.NoDelay = true;
        client.SendBufferSize = 64 * 1024;
        client.ReceiveBufferSize = 64 * 1024;
        client.ReceiveTimeout = 0;
    }

    private sealed class RemoteSession : IDisposable
    {
        public RemoteSession(string id, string display, DateTime joinedAt, TcpClient client)
        {
            Id = id;
            Display = display;
            JoinedAt = joinedAt;
            Client = client;
        }

        public string Id { get; }

        public string Display { get; }

        public DateTime JoinedAt { get; }

        public TcpClient Client { get; }

        public SemaphoreSlim WriteGate { get; } = new(1, 1);

        public void Dispose()
        {
            try
            {
                Client.Client?.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // 忽略
            }

            Client.Dispose();
            WriteGate.Dispose();
        }
    }
}

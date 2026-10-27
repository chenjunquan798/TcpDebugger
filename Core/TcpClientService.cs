using System.Net.Sockets;

namespace TcpDebugger.Core;

/// <summary>
/// TCP 客户端：连接远端主机并在后台泵取数据。
/// </summary>
internal sealed class TcpClientService : IComService
{
    public const string SelfId = "C0";

    private readonly string _host;
    private readonly int _port;

    private TcpClient? _client;
    private SemaphoreSlim? _writeGate;
    private CancellationTokenSource? _cts;
    private bool _connected;

    public TcpClientService(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public bool IsRunning => _connected && _client is { Connected: true };

    public int PeerCount => IsRunning ? 1 : 0;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public event EventHandler<PeerEventArgs>? PeerChanged;

    public event EventHandler<string>? Notified;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken token)
    {
        var client = new TcpClient();
        _client = client;
        _writeGate = new SemaphoreSlim(1, 1);

        try
        {
            Notify($"正在连接 {_host}:{_port} …");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            await client.ConnectAsync(_host, _port, timeout.Token).ConfigureAwait(false);

            client.NoDelay = true;
            client.SendBufferSize = 64 * 1024;
            client.ReceiveBufferSize = 64 * 1024;

            _connected = true;
            Notify($"已连接 {_host}:{_port}");
            PeerChanged?.Invoke(this, new PeerEventArgs
            {
                SessionId = SelfId,
                Display = $"{_host}:{_port}",
                Connected = true,
            });

            var buffer = new byte[64 * 1024];
            NetworkStream stream = client.GetStream();

            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (read <= 0)
                {
                    Notify("远端关闭了连接");
                    break;
                }

                var payload = new byte[read];
                Buffer.BlockCopy(buffer, 0, payload, 0, read);

                DataReceived?.Invoke(this, new DataReceivedEventArgs
                {
                    SessionId = SelfId,
                    Source = $"{_host}:{_port}",
                    Payload = payload,
                });
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Notify("连接超时：对方没有在 8 秒内响应。");
        }
        catch (SocketException ex)
        {
            Notify($"连接失败：{ex.Message}（错误码 {ex.ErrorCode}）");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Notify("连接异常：" + ex.Message);
        }
        finally
        {
            _connected = false;

            PeerChanged?.Invoke(this, new PeerEventArgs
            {
                SessionId = SelfId,
                Display = $"{_host}:{_port}",
                Connected = false,
                Reason = "已断开",
            });

            try
            {
                client.Dispose();
            }
            catch
            {
                // 忽略
            }

            if (ReferenceEquals(_client, client))
            {
                _client = null;
            }

            try
            {
                _writeGate?.Dispose();
            }
            catch
            {
                // 忽略
            }

            _writeGate = null;
        }
    }

    public void Send(string sessionId, byte[] payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        if (!IsRunning || _writeGate is null)
        {
            Notify("当前没有可用的连接，发送已忽略。");
            return;
        }

        _ = WriteToRemoteAsync(payload);
    }

    private async Task WriteToRemoteAsync(byte[] payload)
    {
        if (_client is null || _writeGate is null)
        {
            return;
        }

        try
        {
            await _writeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _client.GetStream().WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }
        }
        catch (Exception ex)
        {
            Notify("发送失败：" + ex.Message);
        }
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
            _client?.Close();
        }
        catch
        {
            // 忽略
        }

        _connected = false;
        Notify("已主动断开");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    private void Notify(string message) => Notified?.Invoke(this, message);
}

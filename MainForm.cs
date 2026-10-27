using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

using TcpDebugger.Core;
using TcpDebugger.UI;

namespace TcpDebugger.Forms;

public sealed partial class MainForm : Form
{
    private ComRole _role = ComRole.Server;
    private IComService? _service;
    private string _targetSessionId = TcpServerService.BroadcastId;

    private long _rxBytes;
    private long _txBytes;
    private long _rxFrames;
    private long _txFrames;
    private bool _paused;

    private readonly System.Windows.Forms.Timer _statsTimer;

    public MainForm()
    {
        _statsTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _statsTimer.Tick += (_, _) => RefreshStats();

        BuildUi();
        ApplyRole();
        RefreshStats();

        _statsTimer.Start();

        WriteSystem("欢迎使用 TCP 调试助手。选择「TCP 服务端」在本机开端口等别人连进来，或选择「TCP 客户端」连到远端设备。", Theme.TextDim);
        WriteSystem("提示：Ctrl+Enter 发送；HEX 模式下输入支持空格、0x 前缀、逗号分隔，例如 48 65 6C 6C 6F。", Theme.TextFaint);
    }

    private PayloadView CurrentView => _segView.SelectedIndex switch
    {
        0 => PayloadView.Both,
        1 => PayloadView.Hex,
        _ => PayloadView.Ascii,
    };

    private void ApplyRole()
    {
        bool server = _segRole.SelectedIndex == 0;
        _role = server ? ComRole.Server : ComRole.Client;

        StopService(silent: true);

        _fldLocalIp.Enable(server);
        _fldLocalPort.Enable(server);
        _fldRemoteHost.Enable(!server);
        _fldRemotePort.Enable(!server);

        _fldLocalPort.SetCaption(server ? "监听端口" : "本地端口(可选)");
        _connLed.LabelText = server ? "未监听" : "未连接";
        _connLed.Active = false;
        _btnAction.Text = server ? "开始监听" : "连接远端";
        _targetSessionId = TcpServerService.BroadcastId;

        ClearPeerList();
        ApplyLayout();
        RefreshStats();
    }

    private void ToggleService()
    {
        if (_service is { IsRunning: true })
        {
            StopService();
            return;
        }

        if (_role == ComRole.Server)
        {
            StartServer();
        }
        else
        {
            StartClient();
        }
    }

    private void StartServer()
    {
        string raw = _txtLocalPort.Text.Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
        {
            ReportError("端口不合法：请输入 1 到 65535 之间的整数。");
            return;
        }

        StopService(silent: true);

        var server = new TcpServerService(port);
        _service = server;
        Wire(server);

        try
        {
            server.Start();
        }
        catch (SocketException ex)
        {
            Unwire(server);
            _service = null;
            server.Dispose();
            ReportError($"无法监听端口 {port}：{ex.Message}（多半是被其它程序占用了）");
            return;
        }
        catch (Exception ex)
        {
            Unwire(server);
            _service = null;
            server.Dispose();
            ReportError("启动监听失败：" + ex.Message);
            return;
        }

        SetConnectionState(true);
        UpdateActionUi();
        SetStatus($"监听中：0.0.0.0:{port}", Theme.Success);
        WriteSystem($"服务端已启动，等待客户端接入：{IPAddress.Any}:{port}", Theme.Success);
    }

    private void StartClient()
    {
        string host = _txtRemoteHost.Text.Trim();
        if (string.IsNullOrEmpty(host))
        {
            ReportError("远端主机不能为空。");
            return;
        }

        string raw = _txtRemotePort.Text.Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
        {
            ReportError("远端端口不合法：请输入 1 到 65535 之间的整数。");
            return;
        }

        StopService(silent: true);

        var client = new TcpClientService(host, port);
        _service = client;
        Wire(client);
        client.Start();

        SetConnectionState(true);
        UpdateActionUi();
        SetStatus($"正在连接 {host}:{port} …", Theme.Warn);
    }

    private void StopService(bool silent = false)
    {
        if (_service is null)
        {
            return;
        }

        Unwire(_service);

        try
        {
            _service.Stop();
        }
        catch
        {
            // 关闭阶段无需抛给用户
        }

        _service.Dispose();
        _service = null;

        SetConnectionState(false);
        UpdateActionUi();
        ClearPeerList();
        _targetSessionId = TcpServerService.BroadcastId;

        if (!silent)
        {
            SetStatus("已断开全部连接", Theme.TextDim);
        }

        RefreshStats();
    }

    private void Wire(IComService service)
    {
        service.DataReceived += OnDataReceived;
        service.PeerChanged += OnPeerChanged;
        service.Notified += OnNotified;
    }

    private void Unwire(IComService service)
    {
        service.DataReceived -= OnDataReceived;
        service.PeerChanged -= OnPeerChanged;
        service.Notified -= OnNotified;
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnDataReceived(sender, e)));
            return;
        }

        _rxBytes += e.Payload.Length;
        _rxFrames++;

        WriteTraffic(false, e.Source, e.Payload);
        SetStatus($"收到来自 {e.Source} 的 {e.Payload.Length} 字节", Theme.Rx);
    }

    private void OnPeerChanged(object? sender, PeerEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnPeerChanged(sender, e)));
            return;
        }

        if (_role != ComRole.Server)
        {
            SetConnectionState(e.Connected);
            UpdateActionUi();
        }

        if (e.Connected)
        {
            AddPeer(e.SessionId, e.Display);
            WriteSystem($"客户端 {e.Display} 已接入", Theme.Success);
        }
        else
        {
            RemovePeer(e.SessionId);
            WriteSystem(
                $"{e.Display} 连接断开{(string.IsNullOrEmpty(e.Reason) ? string.Empty : "：" + e.Reason)}",
                Theme.Warn);
        }

        if (_role == ComRole.Server)
        {
            SetConnectionState(_service?.IsRunning == true);
            UpdateActionUi();
        }

        RefreshStats();
    }

    private void OnNotified(object? sender, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnNotified(sender, message)));
            return;
        }

        WriteSystem(message, Theme.TextDim);
    }

    private void SendPayload()
    {
        if (_service is not { IsRunning: true })
        {
            ReportError(_role == ComRole.Server ? "请先开始监听并确保有客户端接入。" : "请先连接到远端主机。");
            return;
        }

        if (_role == ComRole.Server && GetServerOrNull()?.PeerCount == 0)
        {
            ReportError("还没有客户端接入，无发送目标。");
            return;
        }

        byte[] payload;
        try
        {
            payload = _segSendMode.SelectedIndex == 1
                ? FrameCodec.ParseHex(_txtSend.Text)
                : FrameCodec.FromText(_txtSend.Text, Encoding.UTF8, _chkEscape.Checked, _chkCrLf.Checked);
        }
        catch (FormatException ex)
        {
            ReportError(ex.Message);
            return;
        }

        if (payload.Length == 0)
        {
            SetStatus("没有可发送的内容", Theme.Warn);
            return;
        }

        string target = _role == ComRole.Server ? _targetSessionId : TcpServerService.BroadcastId;
        string display = _role == ComRole.Server
            ? ResolveTargetName(target)
            : $"{_txtRemoteHost.Text.Trim()}:{_txtRemotePort.Text.Trim()}";

        _service.Send(target, payload);

        _txBytes += payload.Length;
        _txFrames++;

        WriteTraffic(true, display, payload);
        SetStatus($"已向 {display} 发送 {payload.Length} 字节", Theme.Tx);
        RefreshStats();
    }

    private string ResolveTargetName(string target)
    {
        if (target == TcpServerService.BroadcastId)
        {
            return "全部会话";
        }

        foreach (ListViewItem item in _clientList.Items)
        {
            if (item.Tag is string id && id == target)
            {
                return item.SubItems[1].Text;
            }
        }

        return target;
    }

    private TcpServerService? GetServerOrNull() => _service as TcpServerService;

    private void KickAll()
    {
        if (GetServerOrNull() is not { } server)
        {
            SetStatus("当前不是服务端模式", Theme.Warn);
            return;
        }

        server.KickAll();
        ClearPeerList();
        RefreshStats();
    }

    private void UpdateSendTarget()
    {
        if (_clientList.SelectedItems.Count == 1 && _clientList.SelectedItems[0].Tag is string id)
        {
            _targetSessionId = id;
        }
        else
        {
            _targetSessionId = TcpServerService.BroadcastId;
        }

        RefreshStats();
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _btnPause.Text = _paused ? "继续" : "暂停";
        SetStatus(_paused ? "已暂停日志刷新，数据仍在正常收发" : "日志已恢复刷新", Theme.TextDim);
    }

    private void ClearLog()
    {
        _logBox.Clear();
        SetStatus("日志已清空", Theme.TextDim);
    }

    private void ExportLog()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"tcp-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Title = "导出收发日志",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, _logBox.Snapshot(), Encoding.UTF8);
            SetStatus($"日志已导出到 {dialog.FileName}", Theme.Success);
        }
        catch (Exception ex)
        {
            ReportError("导出失败：" + ex.Message);
        }
    }

    private void ResetCounters()
    {
        _rxBytes = 0;
        _txBytes = 0;
        _rxFrames = 0;
        _txFrames = 0;
        RefreshStats();
        SetStatus("计数已清零", Theme.TextDim);
    }

    private void UpdateByteCount()
    {
        if (_txtSend is null || _lblByteCount is null)
        {
            return;
        }

        try
        {
            byte[] payload = _segSendMode.SelectedIndex == 1
                ? FrameCodec.ParseHex(_txtSend.Text)
                : FrameCodec.FromText(_txtSend.Text, Encoding.UTF8, _chkEscape.Checked, _chkCrLf.Checked);

            _lblByteCount.Text = $"{payload.Length} 字节";
            _lblByteCount.ForeColor = payload.Length > 0 ? Theme.TextDim : Theme.TextFaint;
        }
        catch (FormatException)
        {
            _lblByteCount.Text = "格式有误";
            _lblByteCount.ForeColor = Theme.Danger;
        }
    }

    private void SetConnectionState(bool active)
    {
        _connLed.Active = active;

        if (_role == ComRole.Server)
        {
            _connLed.LabelText = active ? "监听中" : "未监听";
        }
        else
        {
            _connLed.LabelText = active ? "已连接" : "未连接";
        }
    }

    private void UpdateActionUi()
    {
        bool running = _service is { IsRunning: true };
        _btnAction.Text = running
            ? (_role == ComRole.Server ? "停止监听" : "断开连接")
            : (_role == ComRole.Server ? "开始监听" : "连接远端");
        _btnAction.Variant = running ? ButtonVariant.Danger : ButtonVariant.Primary;
    }

    private void AddPeer(string id, string display)
    {
        foreach (ListViewItem existing in _clientList.Items)
        {
            if (existing.Tag is string tag && tag == id)
            {
                return;
            }
        }

        var item = new ListViewItem(string.Empty) { Tag = id };
        item.SubItems.Add(display);
        item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));
        _clientList.Items.Add(item);

        if (_clientList.SelectedItems.Count == 0)
        {
            item.Selected = true;
        }

        UpdatePeerCount();
    }

    private void RemovePeer(string id)
    {
        for (int i = _clientList.Items.Count - 1; i >= 0; i--)
        {
            if (_clientList.Items[i].Tag is string tag && tag == id)
            {
                _clientList.Items.RemoveAt(i);
                break;
            }
        }

        UpdatePeerCount();
        UpdateSendTarget();
    }

    private void ClearPeerList()
    {
        _clientList.Items.Clear();
        UpdatePeerCount();
    }

    private void UpdatePeerCount() => _lblPeerCount.Text = $"接入会话 ({_clientList.Items.Count})";

    private void RefreshStats()
    {
        string target = _role == ComRole.Server ? ResolveTargetName(_targetSessionId) : "远端";
        int peers = GetServerOrNull()?.PeerCount ?? (_service is { IsRunning: true } ? 1 : 0);

        _lblStats.Text =
            $"会话 {peers} · 目标 {target} · 接收 {FormatBytes(_rxBytes)} ({_rxFrames}) · 发送 {FormatBytes(_txBytes)} ({_txFrames})";
    }

    private void SetStatus(string text, Color? color = null)
    {
        _lblStatus.Text = text;
        _lblStatus.ForeColor = color ?? Theme.TextDim;
    }

    private void ReportError(string message)
    {
        SetStatus(message, Theme.Danger);
        WriteSystem(message, Theme.Danger);
    }

    private void WriteTraffic(bool outgoing, string label, byte[] payload)
    {
        if (_paused)
        {
            return;
        }

        const int labelWidth = 24;
        List<string> lines = FrameCodec.RenderLines(payload, CurrentView);
        Color accent = outgoing ? Theme.Tx : Theme.Rx;

        string stamp = _chkTimestamp.Checked ? "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " : string.Empty;
        string indent = string.Empty.PadRight(stamp.Length + 4 + labelWidth);

        for (int i = 0; i < lines.Count; i++)
        {
            if (i == 0)
            {
                _logBox.Push(stamp, Theme.TextFaint);
                _logBox.Push(outgoing ? "TX" : "RX", accent);
                _logBox.Push("  ", Theme.TextFaint);
                _logBox.Push(TrimDisplay(label, labelWidth).PadRight(labelWidth), Theme.TextDim);
            }
            else
            {
                _logBox.Push(indent, Theme.TextFaint);
            }

            _logBox.Push(lines[i], accent);

            if (i == lines.Count - 1)
            {
                _logBox.Push($"   ({payload.Length} B)", Theme.TextFaint);
            }

            _logBox.Push(Environment.NewLine, Theme.TextDim);
        }
    }

    private void WriteSystem(string message, Color color)
    {
        if (_paused)
        {
            return;
        }

        string stamp = _chkTimestamp.Checked ? "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " : string.Empty;
        _logBox.Push(stamp, Theme.TextFaint);
        _logBox.Push("--".PadRight(4), Theme.TextFaint);
        _logBox.Push(message + Environment.NewLine, color);
    }

    private static string TrimDisplay(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..(max - 2)] + "..";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }

        return (bytes / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetStatus("就绪：选择模式后点击右上按钮即可开始", Theme.TextDim);
        Console.WriteLine($"[TcpDebugger] UI ready, ui-font={Theme.UiFontName}, mono-font={Theme.MonoFontName}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statsTimer.Stop();
        StopService(silent: true);
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statsTimer.Dispose();
            _service?.Dispose();
        }

        base.Dispose(disposing);
    }
}

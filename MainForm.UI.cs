using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

using TcpDebugger.UI;

namespace TcpDebugger.Forms;

public sealed partial class MainForm : Form
{
    private const int Pad = 14;
    private const int TitleHeight = 48;
    private const int StatusHeight = 30;
    private const int ConnectCardHeight = 96;
    private const int SendCardHeight = 178;
    private const int ClientCardWidth = 252;
    private const int Gap = 10;

    private readonly List<Action> _cardLayouts = new();
    private readonly List<FlatButton> _captionButtons = new();
    private bool _layoutReady;

    private Panel _titleBar = null!;
    private CardPanel _connCard = null!;
    private CardPanel _clientCard = null!;
    private CardPanel _logCard = null!;
    private CardPanel _sendCard = null!;
    private Panel _statusBar = null!;

    private StatusLed _connLed = null!;
    private FlatButton _btnAction = null!;

    private SegmentedControl _segRole = null!;
    private DropDown _ddLocalIp = null!;
    private TextField _txtLocalPort = null!;
    private TextField _txtRemoteHost = null!;
    private TextField _txtRemotePort = null!;
    private LabeledField _fldLocalIp = null!;
    private LabeledField _fldLocalPort = null!;
    private LabeledField _fldRemoteHost = null!;
    private LabeledField _fldRemotePort = null!;

    private ClientListView _clientList = null!;
    private Label _lblPeerCount = null!;
    private FlatButton _btnKickAll = null!;

    private LogBox _logBox = null!;
    private SegmentedControl _segView = null!;
    private CheckTile _chkTimestamp = null!;
    private CheckTile _chkAutoScroll = null!;
    private FlatButton _btnPause = null!;
    private FlatButton _btnClear = null!;
    private FlatButton _btnSave = null!;
    private FlowLayoutPanel _logTools = null!;

    private TextField _txtSend = null!;
    private SegmentedControl _segSendMode = null!;
    private CheckTile _chkCrLf = null!;
    private CheckTile _chkEscape = null!;
    private FlowLayoutPanel _sendTools = null!;
    private Label _lblByteCount = null!;
    private FlatButton _btnSend = null!;

    private Label _lblStatus = null!;
    private Label _lblStats = null!;
    private FlatButton _btnResetStats = null!;

    private void BuildUi()
    {
        SuspendLayout();

        Font = Theme.Text(9f);
        BackColor = Theme.Backdrop;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1140, 720);
        Size = new Size(1210, 840);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "TCP 调试助手";

        BuildTitleBar();
        BuildConnectionCard();
        BuildClientCard();
        BuildLogCard();
        BuildSendCard();
        BuildStatusBar();

        // 上面若干赋值会触发 OnResize，此时子控件尚未全部就绪，必须先上闸。
        _layoutReady = true;
        ApplyLayout();

        ResumeLayout(true);
    }

    private void BuildTitleBar()
    {
        _titleBar = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Height = TitleHeight,
            BackColor = Theme.Surface,
        };

        var logo = new Panel
        {
            Size = new Size(22, 22),
            Location = new Point(18, 13),
            BackColor = Theme.Surface,
        };
        logo.Paint += DrawLogo;

        var title = new Label
        {
            Text = "TCP 调试助手",
            ForeColor = Theme.TextBody,
            Font = Theme.Text(10.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(50, 8),
        };

        var subtitle = new Label
        {
            Text = "TCP Server / Client Debugger",
            ForeColor = Theme.TextFaint,
            Font = Theme.Text(7.5f),
            AutoSize = true,
            Location = new Point(51, 26),
        };

        FlatButton minimize = MakeCaptionButton("—");
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;

        FlatButton maximize = MakeCaptionButton("□");
        maximize.Click += (_, _) => ToggleMaximize();

        FlatButton close = MakeCaptionButton("✕");
        close.HoverBack = Theme.Danger;
        close.HoverFore = Color.White;
        close.Click += (_, _) => Close();

        _titleBar.Controls.Add(logo);
        _titleBar.Controls.Add(title);
        _titleBar.Controls.Add(subtitle);

        _titleBar.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                WinApi.ReleaseCapture();
                WinApi.SendMessage(Handle, WinApi.WM_NCLBUTTONDOWN, (IntPtr)WinApi.HTCAPTION, IntPtr.Zero);
            }
        };

        _titleBar.DoubleClick += (_, _) => ToggleMaximize();

        Controls.Add(_titleBar);
    }

    private FlatButton MakeCaptionButton(string glyph)
    {
        var button = new FlatButton
        {
            Text = glyph,
            Variant = ButtonVariant.Ghost,
            Radius = 6,
            Size = new Size(42, 30),
            Font = Theme.Text(9f),
            ForeColor = Theme.TextDim,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TabStop = false,
        };

        _captionButtons.Add(button);
        _titleBar.Controls.Add(button);
        return button;
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;

    private static void DrawLogo(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var rect = new RectangleF(1f, 1f, 20f, 20f);
        using GraphicsPath path = Theme.RoundRect(rect, 6);
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            rect,
            Theme.AccentHi,
            Theme.Accent,
            System.Drawing.Drawing2D.LinearGradientMode.BackwardDiagonal);
        g.FillPath(brush, path);

        using var pen = new Pen(Theme.Backdrop, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(pen, 5.5f, 5.5f, 11f, 11f, 40f, 260f);
        g.DrawLine(pen, 11f, 11f, 11f, 14.5f);
    }

    private void BuildConnectionCard()
    {
        _connCard = new CardPanel { Radius = 12 };

        _segRole = new SegmentedControl
        {
            Items = new[] { "TCP 服务端", "TCP 客户端" },
            Size = new Size(232, 34),
            Location = new Point(16, 30),
            Font = Theme.Text(9f),
        };
        _segRole.SelectedIndexChanged += (_, _) => ApplyRole();

        _ddLocalIp = new DropDown { Font = Theme.Text(9f) };
        _ddLocalIp.Items = EnumerateLocalAddresses().ToArray();
        _ddLocalIp.Value = _ddLocalIp.Items.Length > 0 ? _ddLocalIp.Items[0] : "127.0.0.1";
        _fldLocalIp = new LabeledField("本机地址", _ddLocalIp, 176)
        {
            Location = new Point(262, 22),
        };

        _txtLocalPort = new TextField { Font = Theme.Text(9f) };
        _txtLocalPort.Text = "8080";
        _fldLocalPort = new LabeledField("端口", _txtLocalPort, 84)
        {
            Location = new Point(448, 22),
        };

        _txtRemoteHost = new TextField { Font = Theme.Text(9f), Placeholder = "127.0.0.1" };
        _txtRemoteHost.Text = "127.0.0.1";
        _fldRemoteHost = new LabeledField("远端主机", _txtRemoteHost, 176)
        {
            Location = new Point(546, 22),
        };

        _txtRemotePort = new TextField { Font = Theme.Text(9f) };
        _txtRemotePort.Text = "9000";
        _fldRemotePort = new LabeledField("远端端口", _txtRemotePort, 84)
        {
            Location = new Point(732, 22),
        };

        _connLed = new StatusLed
        {
            LabelText = "未连接",
            Size = new Size(140, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        _btnAction = new FlatButton
        {
            Text = "开始监听",
            Variant = ButtonVariant.Primary,
            Size = new Size(124, 36),
            Font = Theme.Text(9f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnAction.Click += (_, _) => ToggleService();

        _connCard.Controls.Add(_segRole);
        _connCard.Controls.Add(_fldLocalIp);
        _connCard.Controls.Add(_fldLocalPort);
        _connCard.Controls.Add(_fldRemoteHost);
        _connCard.Controls.Add(_fldRemotePort);
        _connCard.Controls.Add(_connLed);
        _connCard.Controls.Add(_btnAction);

        Controls.Add(_connCard);
    }

    private void BuildClientCard()
    {
        _clientCard = new CardPanel { Radius = 12 };

        var header = new Panel
        {
            Height = 44,
            BackColor = Theme.Surface,
        };

        _lblPeerCount = new Label
        {
            Text = "接入会话 (0)",
            ForeColor = Theme.TextBody,
            Font = Theme.Text(9f, FontStyle.Bold),
            AutoSize = false,
            Size = new Size(118, 20),
            Location = new Point(16, 13),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _btnKickAll = new FlatButton
        {
            Text = "全部断开",
            Variant = ButtonVariant.Ghost,
            Size = new Size(84, 26),
            Font = Theme.Text(8.25f),
            Radius = 6,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnKickAll.Click += (_, _) => KickAll();

        header.Controls.Add(_lblPeerCount);
        header.Controls.Add(_btnKickAll);

        var body = new Panel
        {
            Padding = new Padding(12, 0, 14, 12),
            BackColor = Theme.Surface,
        };

        _clientList = new ClientListView { Dock = DockStyle.Fill };
        _clientList.SelectedIndexChanged += (_, _) => UpdateSendTarget();
        body.Controls.Add(_clientList);

        _clientCard.Controls.Add(body);
        _clientCard.Controls.Add(header);

        WireCard(_clientCard, header, 44, body);
        Controls.Add(_clientCard);
    }

    private void BuildLogCard()
    {
        _logCard = new CardPanel { Radius = 12 };

        var toolbar = new Panel
        {
            Height = 46,
            BackColor = Theme.Surface,
        };

        var title = new Label
        {
            Text = "数据收发",
            ForeColor = Theme.TextBody,
            Font = Theme.Text(9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 15),
        };

        _logTools = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
        };

        _segView = new SegmentedControl
        {
            Items = new[] { "HEX + 字符", "HEX", "字符" },
            Size = new Size(288, 30),
            Font = Theme.Text(8.5f),
            Margin = new Padding(0, 8, 10, 0),
        };

        _chkTimestamp = new CheckTile
        {
            LabelText = "时间戳",
            Checked = true,
            Font = Theme.Text(8.5f),
            Margin = new Padding(0, 13, 12, 0),
        };

        _chkAutoScroll = new CheckTile
        {
            LabelText = "自动滚动",
            Checked = true,
            Font = Theme.Text(8.5f),
            Margin = new Padding(0, 13, 12, 0),
        };
        _chkAutoScroll.CheckedChanged += (_, _) =>
        {
            if (_logBox is not null)
            {
                _logBox.AutoScroll = _chkAutoScroll.Checked;
            }
        };

        _btnPause = new FlatButton
        {
            Text = "暂停",
            Variant = ButtonVariant.Ghost,
            Size = new Size(62, 28),
            Font = Theme.Text(8.5f),
            Radius = 7,
            Margin = new Padding(8, 9, 4, 0),
        };
        _btnPause.Click += (_, _) => TogglePause();

        _btnClear = new FlatButton
        {
            Text = "清空",
            Variant = ButtonVariant.Ghost,
            Size = new Size(62, 28),
            Font = Theme.Text(8.5f),
            Radius = 7,
            Margin = new Padding(0, 9, 4, 0),
        };
        _btnClear.Click += (_, _) => ClearLog();

        _btnSave = new FlatButton
        {
            Text = "导出",
            Variant = ButtonVariant.Ghost,
            Size = new Size(62, 28),
            Font = Theme.Text(8.5f),
            Radius = 7,
            Margin = new Padding(0, 9, 0, 0),
        };
        _btnSave.Click += (_, _) => ExportLog();

        _logTools.Controls.Add(_segView);
        _logTools.Controls.Add(_chkTimestamp);
        _logTools.Controls.Add(_chkAutoScroll);
        _logTools.Controls.Add(_btnPause);
        _logTools.Controls.Add(_btnClear);
        _logTools.Controls.Add(_btnSave);

        toolbar.Controls.Add(title);
        toolbar.Controls.Add(_logTools);

        var body = new Panel
        {
            Padding = new Padding(14, 0, 14, 14),
            BackColor = Theme.Surface,
        };

        _logBox = new LogBox { Dock = DockStyle.Fill, AutoScroll = true };
        body.Controls.Add(_logBox);

        _logCard.Controls.Add(body);
        _logCard.Controls.Add(toolbar);

        WireCard(_logCard, toolbar, 46, body);
        _cardLayouts.Add(() => _logTools.Location = new Point(_logCard.ClientSize.Width - 16 - _logTools.Width, 0));

        Controls.Add(_logCard);
    }

    private void BuildSendCard()
    {
        _sendCard = new CardPanel { Radius = 12 };

        var toolbar = new Panel
        {
            Height = 46,
            BackColor = Theme.Surface,
        };

        _segSendMode = new SegmentedControl
        {
            Items = new[] { "文本", "HEX" },
            Size = new Size(150, 30),
            Location = new Point(16, 8),
            Font = Theme.Text(9f),
        };
        _segSendMode.SelectedIndexChanged += (_, _) => UpdateByteCount();

        _chkCrLf = new CheckTile
        {
            LabelText = "追加 \\r\\n",
            Checked = false,
            Font = Theme.Text(8.5f),
            Margin = new Padding(0, 0, 14, 0),
        };
        _chkCrLf.CheckedChanged += (_, _) => UpdateByteCount();

        _chkEscape = new CheckTile
        {
            LabelText = "解析转义符",
            Checked = true,
            Font = Theme.Text(8.5f),
            Margin = new Padding(0),
        };
        _chkEscape.CheckedChanged += (_, _) => UpdateByteCount();

        _sendTools = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0),
        };
        _sendTools.Controls.Add(_chkCrLf);
        _sendTools.Controls.Add(_chkEscape);

        _lblByteCount = new Label
        {
            Text = "0 字节",
            ForeColor = Theme.TextDim,
            Font = Theme.Text(8.5f),
            AutoSize = false,
            Size = new Size(96, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        _btnSend = new FlatButton
        {
            Text = "发送   Ctrl+Enter",
            Variant = ButtonVariant.Primary,
            Size = new Size(164, 34),
            Font = Theme.Text(9f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnSend.Click += (_, _) => SendPayload();

        toolbar.Controls.Add(_segSendMode);
        toolbar.Controls.Add(_sendTools);
        toolbar.Controls.Add(_lblByteCount);
        toolbar.Controls.Add(_btnSend);

        var body = new Panel
        {
            Padding = new Padding(16, 0, 16, 14),
            BackColor = Theme.Surface,
        };

        _txtSend = new TextField { Dock = DockStyle.Fill, Font = Theme.Text(9.5f) };
        _txtSend.SetMultiline(true);
        _txtSend.Wrap = true;
        _txtSend.Scroll = ScrollBars.Vertical;
        _txtSend.Placeholder = "输入要发送的内容；文本模式支持 \\r \\n \\t，HEX 模式形如 48 65 6C 6C 6F";
        _txtSend.ValueChanged += (_, _) => UpdateByteCount();
        _txtSend.KeyDownFired += (_, args) =>
        {
            if (args.Control && args.KeyCode == Keys.Enter)
            {
                SendPayload();
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        };

        body.Controls.Add(_txtSend);

        _sendCard.Controls.Add(body);
        _sendCard.Controls.Add(toolbar);

        WireCard(_sendCard, toolbar, 46, body);
        _cardLayouts.Add(() =>
        {
            int width = _sendCard.ClientSize.Width;
            _btnSend.Location = new Point(width - 16 - 164, 6);
            _lblByteCount.Location = new Point(width - 16 - 164 - 12 - 96, 12);
            _sendTools.Location = new Point(_segSendMode.Right + 16, 12);
        });

        Controls.Add(_sendCard);
    }

    private void BuildStatusBar()
    {
        _statusBar = new Panel
        {
            Height = StatusHeight,
            BackColor = Theme.Surface,
        };

        _lblStatus = new Label
        {
            Text = "就绪",
            ForeColor = Theme.TextDim,
            Font = Theme.Text(8.5f),
            AutoSize = true,
            Location = new Point(18, 9),
        };

        _btnResetStats = new FlatButton
        {
            Text = "计数清零",
            Variant = ButtonVariant.Ghost,
            Size = new Size(84, 22),
            Font = Theme.Text(8f),
            Radius = 6,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnResetStats.Click += (_, _) => ResetCounters();

        _lblStats = new Label
        {
            Text = "会话 0 · 接收 0 B · 发送 0 B",
            ForeColor = Theme.TextDim,
            Font = Theme.Text(8.5f),
            AutoSize = false,
            Size = new Size(380, 18),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        _statusBar.Controls.Add(_lblStatus);
        _statusBar.Controls.Add(_lblStats);
        _statusBar.Controls.Add(_btnResetStats);

        Controls.Add(_statusBar);
    }

    /// <summary>
    /// 让卡片内的顶部工具条与主体区域随卡片尺寸自动伸缩，避免依赖 WinForms 的停靠顺序。
    /// </summary>
    private void WireCard(CardPanel card, Control topBar, int topHeight, Control body)
    {
        _cardLayouts.Add(() =>
        {
            int width = card.ClientSize.Width;
            int height = card.ClientSize.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            topBar.SetBounds(0, 0, width, topHeight);

            int bodyTop = topHeight;
            int bodyHeight = height - bodyTop;
            if (bodyHeight > 0)
            {
                body.SetBounds(0, bodyTop, width, bodyHeight);
            }
        });
    }

    private void ApplyLayout()
    {
        if (!_layoutReady)
        {
            return;
        }

        int width = ClientSize.Width;
        int height = ClientSize.Height;
        int contentWidth = Math.Max(600, width - Pad * 2);

        _titleBar.SetBounds(0, 0, width, TitleHeight);

        int captionX = width - 12 - 42;
        for (int i = _captionButtons.Count - 1; i >= 0; i--)
        {
            _captionButtons[i].Location = new Point(captionX, 9);
            captionX -= 46;
        }

        int connectTop = TitleHeight + Gap;
        _connCard.SetBounds(Pad, connectTop, contentWidth, ConnectCardHeight);
        _btnAction.Location = new Point(contentWidth - 16 - 124, 28);
        _connLed.Location = new Point(contentWidth - 16 - 124 - 12 - 140, 36);

        int sendBottom = height - StatusHeight - Pad;
        int sendTop = sendBottom - SendCardHeight;

        int cardTop = connectTop + ConnectCardHeight + Gap;
        int cardHeight = Math.Max(140, sendTop - Gap - cardTop);

        bool clientVisible = _segRole.SelectedIndex == 0;
        _clientCard.Visible = clientVisible;

        int logLeft = clientVisible ? Pad + ClientCardWidth + 12 : Pad;
        int logWidth = Math.Max(360, width - Pad - logLeft);

        if (clientVisible)
        {
            _clientCard.SetBounds(Pad, cardTop, ClientCardWidth, cardHeight);
            _btnKickAll.Location = new Point(ClientCardWidth - 16 - 84, 9);
        }

        _logCard.SetBounds(logLeft, cardTop, logWidth, cardHeight);
        _sendCard.SetBounds(Pad, sendTop, contentWidth, SendCardHeight);

        _statusBar.SetBounds(0, height - StatusHeight, width, StatusHeight);
        _btnResetStats.Location = new Point(width - 18 - 84, 4);
        _lblStats.Location = new Point(width - 18 - 84 - 10 - 380, 6);

        foreach (Action layout in _cardLayouts)
        {
            layout();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyLayout();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WinApi.WM_NCHITTEST && WindowState != FormWindowState.Maximized)
        {
            int packed = m.LParam.ToInt32();
            var point = PointToClient(new Point(packed & 0xFFFF, (packed >> 16) & 0xFFFF));

            const int grip = 6;
            IntPtr hit = IntPtr.Zero;

            bool left = point.X <= grip;
            bool right = point.X >= Width - grip;
            bool top = point.Y <= grip;
            bool bottom = point.Y >= Height - grip;

            if (left && top) hit = (IntPtr)WinApi.HTTOPLEFT;
            else if (right && top) hit = (IntPtr)WinApi.HTTOPRIGHT;
            else if (left && bottom) hit = (IntPtr)WinApi.HTBOTTOMLEFT;
            else if (right && bottom) hit = (IntPtr)WinApi.HTBOTTOMRIGHT;
            else if (left) hit = (IntPtr)WinApi.HTLEFT;
            else if (right) hit = (IntPtr)WinApi.HTRIGHT;
            else if (top) hit = (IntPtr)WinApi.HTTOP;
            else if (bottom) hit = (IntPtr)WinApi.HTBOTTOM;

            if (hit != IntPtr.Zero)
            {
                m.Result = hit;
                return;
            }
        }

        base.WndProc(ref m);
    }

    private static List<string> EnumerateLocalAddresses()
    {
        var result = new List<string> { "0.0.0.0", "127.0.0.1" };

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    string ip = address.Address.ToString();
                    if (!result.Contains(ip))
                    {
                        result.Add(ip);
                    }
                }
            }
        }

        return result;
    }
}

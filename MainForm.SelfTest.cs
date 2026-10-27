using System.Drawing;
using System.Drawing.Text;
using System.Text;

using TcpDebugger.Core;
using TcpDebugger.UI;

namespace TcpDebugger.Forms;

public sealed partial class MainForm
{
    /// <summary>
    /// 界面自检：真实创建主窗体句柄、切换模式、按多组尺寸重排布局，并驱动编解码联动。
    /// 不需要显示窗口，因此可以在无桌面的环境里跑。
    /// </summary>
    internal static List<SelfTestCase> ProbeUi()
    {
        var cases = new List<SelfTestCase>();

        void Add(string name, bool passed, string detail) =>
            cases.Add(new SelfTestCase(name, passed, detail));

        var form = new MainForm();

        try
        {
            _ = form.Handle;

            // 逐一强制创建句柄。窗体并未真正显示，句柄需要显式触碰才会建立。
            _ = form._logCard.Handle;
            _ = form._connCard.Handle;
            _ = form._sendCard.Handle;
            _ = form._clientCard.Handle;
            _ = form._txtSend.Handle;
            _ = form._txtSend.Inner.Handle;
            _ = form._clientList.Handle;
            _ = form._btnSend.Handle;

            Add("ui/window-handle", form.IsHandleCreated, $"主窗体句柄 0x{form.Handle.ToInt64():X}");

            var missing = new List<string>();
            if (!form._logCard.IsHandleCreated) missing.Add("日志卡片");
            if (!form._connCard.IsHandleCreated) missing.Add("连接卡片");
            if (!form._sendCard.IsHandleCreated) missing.Add("发送卡片");
            if (!form._clientCard.IsHandleCreated) missing.Add("会话卡片");
            if (!form._txtSend.IsHandleCreated) missing.Add("发送输入框");
            if (!form._txtSend.Inner.IsHandleCreated) missing.Add("输入框内层");
            if (!form._clientList.IsHandleCreated) missing.Add("会话列表");
            if (!form._btnSend.IsHandleCreated) missing.Add("发送按钮");

            Add(
                "ui/controls-built",
                missing.Count == 0,
                missing.Count == 0
                    ? $"顶层容器 {form.Controls.Count} 个，关键子控件句柄均已建立"
                    : "以下控件句柄未建立：" + string.Join("、", missing));

            Add(
                "ui/fonts-resolved",
                FontFamily.Families.Any(f => f.Name == Theme.UiFontName),
                $"界面字体 {Theme.UiFontName}，等宽字体 {Theme.MonoFontName}");

            // 多尺寸重排。历史上构造期被 MinimumSize 触发的 OnResize 就崩在这一步。
            foreach (var size in new[] { new Size(1140, 720), new Size(1210, 840), new Size(1600, 900) })
            {
                form.ClientSize = size;
                form.ApplyLayout();

                bool inside = form._logCard.Right <= form.ClientSize.Width
                              && form._sendCard.Bottom <= form.ClientSize.Height
                              && form._statusBar.Top == form.ClientSize.Height - StatusHeight
                              && form._logCard.Width > 300;

                Add(
                    $"ui/layout-{size.Width}x{size.Height}",
                    inside,
                    $"日志区 {form._logCard.Width}x{form._logCard.Height}，发送区底部 {form._sendCard.Bottom}，底栏顶部 {form._statusBar.Top}");
            }

            // 服务端模式应显示会话列表并把日志区右推，客户端模式应收起列表让日志区左扩。
            //
            // 注意：这里刻意不读 CardPanel.Visible。探针窗体从未 Show，
            // WinForms 在父容器不可见时对子控件一律读回 Visible=false，
            // 该标志此时不可观测。真正能反映布局分支是否生效的是几何量，故按坐标断言。
            form._segRole.SelectedIndex = 1;
            form._segRole.SelectedIndex = 0;
            form.ApplyLayout();

            int serverLogLeft = form._logCard.Left;
            int expectedServerLeft = Pad + ClientCardWidth + 12;
            bool serverPanelParked = form._clientCard.Left == Pad && form._clientCard.Width == ClientCardWidth;

            Add(
                "ui/role-server",
                serverLogLeft == expectedServerLeft && serverPanelParked,
                $"日志区左边界 {serverLogLeft}（应为 {expectedServerLeft}），会话面板占位 {form._clientCard.Left}~{form._clientCard.Right}");

            form._segRole.SelectedIndex = 1;
            bool clientLogLeft = form._logCard.Left == Pad;
            int clientLogWidth = form._logCard.Width;
            bool remoteReady = form._fldRemoteHost.Body.Enabled;
            bool localLocked = !form._fldLocalIp.Body.Enabled;

            Add(
                "ui/role-client",
                clientLogLeft && clientLogWidth > form.Width - Pad * 2 - 1 && remoteReady && localLocked,
                $"日志区 {form._logCard.Left}~{form._logCard.Right}（已左扩到 {clientLogWidth}），远端字段可用={remoteReady}，本机字段已锁={localLocked}");

            form._segRole.SelectedIndex = 0;

            // HEX 计数联动
            form._segSendMode.SelectedIndex = 1;
            form._txtSend.Text = "48 65 6C 6C 6F 21";
            Add("ui/hex-byte-count", form._lblByteCount.Text == "6 字节", $"HEX 输入计为 {form._lblByteCount.Text}");

            form._txtSend.Text = "48 65 6";
            Add("ui/hex-invalid-count", form._lblByteCount.Text == "格式有误", $"非法 HEX 显示为 {form._lblByteCount.Text}");

            // 文本模式 + 转义 + 追加 CRLF
            form._segSendMode.SelectedIndex = 0;
            form._chkEscape.Checked = true;
            form._chkCrLf.Checked = true;
            form._txtSend.Text = "abc";
            Add("ui/text-byte-count", form._lblByteCount.Text == "5 字节", $"文本 abc + CRLF 计为 {form._lblByteCount.Text}");

            // 日志写入与刷新
            byte[] frame = Encoding.UTF8.GetBytes("Hello TCP");
            form.WriteTraffic(false, "127.0.0.1:9000", frame);
            form.WriteTraffic(true, "127.0.0.1:9000", frame);

            PumpUi();

            string log = form._logBox.Snapshot();
            bool logOk = log.Contains("Hello TCP") && log.Contains("RX") && log.Contains("TX") && log.Contains("127.0.0.1");
            Add("ui/log-render", logOk, $"日志 {log.Length} 字符，含 RX/TX 与收发标记");

            // 暂停后不应再写入。此处直接调用按钮背后的逻辑，
            // 因为 PerformClick 依赖 CanSelect，控件未真正显示时会被静默忽略。
            form.TogglePause();
            form.WriteSystem("should-not-appear", Theme.TextDim);
            PumpUi();
            bool pausedOk = !form._logBox.Snapshot().Contains("should-not-appear");
            Add("ui/log-pause", pausedOk, $"暂停后按钮文案为 {form._btnPause.Text}，日志停止刷新={pausedOk}");

            form.TogglePause();
            Add("ui/log-resume", form._btnPause.Text == "暂停", $"再次点击后按钮文案为 {form._btnPause.Text}");

            form.ClearLog();
            PumpUi();
            Add("ui/log-clear", form._logBox.Snapshot().Length == 0, "清空后日志长度为 0");

            // 工具条排版：子控件必须按顺序排开、互不重叠，且完全落在工具条内。
            form._logTools.PerformLayout();
            var dump = new StringBuilder();
            bool ordered = true;
            int prevRight = int.MinValue;
            foreach (Control child in form._logTools.Controls)
            {
                dump.Append($"[{child.GetType().Name} {child.Left}+{child.Width}]");
                if (child.Left < prevRight || child.Right > form._logTools.Width || child.Width <= 0)
                {
                    ordered = false;
                }

                prevRight = child.Right;
            }

            Add("ui/log-toolbar-bounds", ordered, $"工具条宽 {form._logTools.Width}：" + dump);

            // 分段选择器每段要放得下自己的文案，否则会被裁成「HEX + …」。
            var segDump = new StringBuilder();
            bool segFits = true;
            int segCount = form._segView.Items.Length;
            float segWidth = (form._segView.Width - 6f) / segCount - 2f;
            foreach (string item in form._segView.Items)
            {
                Size measured = TextRenderer.MeasureText(
                    item,
                    form._segView.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

                segDump.Append($"[{item} {measured.Width}>{segWidth:0}]");
                if (measured.Width > segWidth)
                {
                    segFits = false;
                }
            }

            Add(
                "ui/seg-view-fits",
                segFits,
                $"控件宽 {form._segView.Width}、字体 {form._segView.Font.SizeInPoints}pt、每段可用 {segWidth:0}px：" + segDump);

            // 统计口径
            form._rxBytes = 2048;
            form._txBytes = 4096;
            form.RefreshStats();
            Add(
                "ui/stats-format",
                form._lblStats.Text.Contains("2.0 KB") && form._lblStats.Text.Contains("4.0 KB"),
                form._lblStats.Text);
        }
        catch (Exception ex)
        {
            Add("ui/unhandled", false, ex.GetType().Name + "：" + ex.Message);
        }
        finally
        {
            form.Dispose();
        }

        return cases;
    }

    private static void PumpUi()
    {
        for (int i = 0; i < 30; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}

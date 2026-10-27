using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 收发日志视图。写入通过缓冲队列，能在任意线程调用，UI 线程批量刷出，高频收发也不卡。
/// </summary>
internal sealed class LogBox : RichTextBox
{
    private readonly object _sync = new();
    private readonly Queue<Segment> _pending = new();
    private readonly System.Windows.Forms.Timer _timer;

    public LogBox()
    {
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Backdrop;
        ForeColor = Theme.TextBody;
        Font = Theme.Mono(9f);
        ReadOnly = true;
        ScrollBars = RichTextBoxScrollBars.Both;
        WordWrap = false;
        HideSelection = false;
        DetectUrls = false;
        AutoWordSelection = false;
        AllowDrop = false;
        Cursor = Cursors.IBeam;

        _timer = new System.Windows.Forms.Timer { Interval = 70 };
        _timer.Tick += Flush;
        _timer.Start();
    }

    public bool AutoScroll { get; set; } = true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WinApi.UseDarkScrollBars(Handle);
    }

    public int MaxChars { get; set; } = 320_000;

    /// <summary>线程安全。</summary>
    public void Push(string text, Color color)
    {
        lock (_sync)
        {
            _pending.Enqueue(new Segment(text, color));
        }
    }

    public void PushLine(string text, Color color)
    {
        Push(text, color);
        Push(Environment.NewLine, Theme.TextDim);
    }

    private void Flush(object? sender, EventArgs e)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var batch = new List<Segment>(Math.Min(_pending.Count, 600));
        lock (_sync)
        {
            while (_pending.Count > 0 && batch.Count < 600)
            {
                batch.Add(_pending.Dequeue());
            }
        }

        WinApi.SendMessage(Handle, WinApi.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        try
        {
            int start = TextLength;
            Select(start, 0);

            var builder = new StringBuilder();
            Color current = batch[0].Color;
            builder.Append(batch[0].Text);

            for (int i = 1; i < batch.Count; i++)
            {
                Segment segment = batch[i];
                if (segment.Color.ToArgb() == current.ToArgb())
                {
                    builder.Append(segment.Text);
                }
                else
                {
                    SelectionColor = current;
                    SelectedText = builder.ToString();
                    builder.Clear();
                    current = segment.Color;
                    builder.Append(segment.Text);
                }
            }

            SelectionColor = current;
            SelectedText = builder.ToString();

            Trim();

            if (AutoScroll)
            {
                SelectionStart = TextLength;
                ScrollToCaret();
            }
        }
        finally
        {
            WinApi.SendMessage(Handle, WinApi.WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            Invalidate();
        }
    }

    private void Trim()
    {
        if (TextLength <= MaxChars)
        {
            return;
        }

        int keepFrom = TextLength - MaxChars / 2;
        Select(0, keepFrom);
        SelectedText = string.Empty;
    }

    public string Snapshot()
    {
        return Text.Replace('\n', '\r');
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private readonly record struct Segment(string Text, Color Color);
}

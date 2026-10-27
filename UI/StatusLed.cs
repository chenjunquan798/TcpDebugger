using System.Drawing;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 状态指示灯。点亮时带呼吸光晕。
/// </summary>
internal sealed class StatusLed : Control
{
    private readonly System.Windows.Forms.Timer _timer;
    private bool _active;
    private int _tick;
    private Color _offColor = Theme.TextFaint;

    public StatusLed()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Font = Theme.Text(9f);
        ForeColor = Theme.TextDim;

        _timer = new System.Windows.Forms.Timer { Interval = 70 };
        _timer.Tick += (_, _) =>
        {
            _tick++;
            Invalidate();
        };
    }

    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value)
            {
                return;
            }

            _active = value;
            _timer.Enabled = value;
            Invalidate();
        }
    }

    public Color OnColor { get; set; } = Theme.Success;

    public Color OffColor
    {
        get => _offColor;
        set
        {
            _offColor = value;
            Invalidate();
        }
    }

    public string LabelText { get; set; } = string.Empty;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        Color core = _active ? OnColor : OffColor;
        var center = new PointF(9f, Height / 2f);
        const float radius = 4f;

        if (_active)
        {
            double wave = (Math.Sin(_tick * 0.28) + 1d) / 2d;
            float halo = radius + 3.5f + (float)(wave * 2.5f);

            using var haloBrush = new SolidBrush(Color.FromArgb((int)(28 + wave * 46), core));
            g.FillEllipse(haloBrush, center.X - halo, center.Y - halo, halo * 2f, halo * 2f);
        }

        using (var coreBrush = new SolidBrush(core))
        {
            g.FillEllipse(coreBrush, center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
        }

        var textRect = new Rectangle(20, 0, Width - 20, Height);

        const TextFormatFlags flags =
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis;

        TextRenderer.DrawText(g, LabelText, Font, textRect, _active ? Theme.TextBody : Theme.TextDim, flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}

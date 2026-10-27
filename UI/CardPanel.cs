using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 圆角卡片容器。
/// </summary>
internal sealed class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Surface;
        Padding = new Padding(0);
    }

    public int Radius { get; set; } = 12;

    public Color BorderColor { get; set; } = Theme.LineSoft;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        using GraphicsPath path = Theme.RoundRect(ClientRectangle, Radius);
        using (var brush = new SolidBrush(BackColor))
        {
            g.FillPath(brush, path);
        }

        if (BorderColor.A > 0)
        {
            using var pen = new Pen(BorderColor, 1f) { Alignment = PenAlignment.Inset };
            g.DrawPath(pen, path);
        }
    }
}

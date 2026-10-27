using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.SurfaceAlt;

    public override Color MenuBorder => Theme.Line;

    public override Color MenuItemSelected => Theme.Accent;

    public override Color MenuItemSelectedGradientBegin => Theme.Accent;

    public override Color MenuItemSelectedGradientEnd => Theme.Accent;

    public override Color MenuItemPressedGradientBegin => Theme.Accent;

    public override Color MenuItemPressedGradientEnd => Theme.Accent;

    public override Color MenuItemBorder => Color.Transparent;

    public override Color CheckBackground => Theme.Accent;

    public override Color CheckSelectedBackground => Theme.Accent;

    public override Color ImageMarginGradientBegin => Theme.SurfaceAlt;

    public override Color ImageMarginGradientMiddle => Theme.SurfaceAlt;

    public override Color ImageMarginGradientEnd => Theme.SurfaceAlt;

    public override Color SeparatorDark => Theme.Line;

    public override Color SeparatorLight => Theme.Line;
}

/// <summary>
/// 深色下拉菜单渲染器。
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkMenuColorTable())
    {
        RoundedEdges = true;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var rect = new RectangleF(4f, 1f, e.Item.Width - 8f, e.Item.Height - 2f);

        if (e.Item.Selected)
        {
            using GraphicsPath path = Theme.RoundRect(rect, 5);
            using var brush = new SolidBrush(Theme.Accent);
            g.FillPath(brush, path);
        }
        else
        {
            using var brush = new SolidBrush(Theme.SurfaceAlt);
            g.FillRectangle(brush, e.Item.Bounds);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? Color.White : Theme.TextBody;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Line, 1f);
        e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
    }
}

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

internal enum ButtonVariant
{
    Neutral,
    Primary,
    Success,
    Danger,
    Ghost,
}

/// <summary>
/// 自绘按钮，支持圆角、hover / 按下状态与多种语义色。
/// </summary>
internal sealed class FlatButton : Button
{
    private bool _hover;
    private bool _pressed;

    public FlatButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;

        BackColor = Theme.Surface;
        ForeColor = Theme.TextBody;
        Font = Theme.Text(9f);
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleCenter;
    }

    public ButtonVariant Variant { get; set; } = ButtonVariant.Neutral;

    public int Radius { get; set; } = 8;

    public Color GlyphColor { get; set; } = Color.Empty;

    /// <summary>悬停时强制使用的背景色，用于关闭按钮一类的特殊按钮。</summary>
    public Color HoverBack { get; set; } = Color.Empty;

    public Color HoverFore { get; set; } = Color.Empty;

    public int FixedHeight { get; set; } = 0;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        (Color back, Color fore, Color border) = ResolvePalette();

        if (_hover && HoverBack.A > 0)
        {
            back = HoverBack;
        }

        if (_hover && HoverFore.A > 0)
        {
            fore = HoverFore;
        }

        using GraphicsPath path = Theme.RoundRect(rect, Radius);
        using (var brush = new SolidBrush(back))
        {
            g.FillPath(brush, path);
        }

        if (border.A > 0)
        {
            using var pen = new Pen(border, 1f) { Alignment = PenAlignment.Inset };
            g.DrawPath(pen, path);
        }

        const TextFormatFlags flags =
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding;

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore, flags);
    }

    private (Color Back, Color Fore, Color Border) ResolvePalette()
    {
        if (!Enabled)
        {
            return (Theme.Surface, Theme.TextFaint, Theme.LineSoft);
        }

        // Ghost 变体刻意用不透明的 Surface 充当底色，而不是 Color.Transparent。
        // 同容器里若有多个透明子控件，WinForms 会通过父窗口的 WM_PRINTCLIENT 补背景，
        // 而容器重绘时会把兄弟控件一并画进来，结果就是按钮上出现别的控件的重影。
        return Variant switch
        {
            ButtonVariant.Primary => _pressed
                ? (ColorTranslator.FromHtml("#2F6FD6"), Color.White, Color.Transparent)
                : _hover
                    ? (ColorTranslator.FromHtml("#4F8EF7"), Color.White, Color.Transparent)
                    : (Theme.Accent, Color.White, Color.Transparent),

            ButtonVariant.Success => _pressed
                ? (ColorTranslator.FromHtml("#0F8A66"), Color.White, Color.Transparent)
                : _hover
                    ? (ColorTranslator.FromHtml("#3EE3B8"), Color.White, Color.Transparent)
                    : (ColorTranslator.FromHtml("#17956F"), Color.White, Color.Transparent),

            ButtonVariant.Danger => _pressed
                ? (ColorTranslator.FromHtml("#B22F35"), Color.White, Color.Transparent)
                : _hover
                    ? (ColorTranslator.FromHtml("#FF6A70"), Color.White, Color.Transparent)
                    : (ColorTranslator.FromHtml("#D93F45"), Color.White, Color.Transparent),

            ButtonVariant.Ghost => _pressed
                ? (Theme.Field, Theme.TextBody, Theme.Line)
                : _hover
                    ? (Theme.FieldHover, Theme.TextBody, Theme.Line)
                    : (Theme.Surface, Theme.TextDim, Theme.LineSoft),

            _ => _pressed
                ? (ColorTranslator.FromHtml("#141924"), Theme.TextBody, Theme.Line)
                : _hover
                    ? (Theme.FieldHover, Theme.TextBody, Theme.Line)
                    : (Theme.Field, Theme.TextBody, Theme.Line),
        };
    }
}

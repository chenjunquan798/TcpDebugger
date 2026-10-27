using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 自绘复选框。原生 CheckBox 的勾选框在深色背景上很突兀，所以自己画一个。
/// </summary>
internal sealed class CheckTile : Control
{
    private const int BoxSize = 16;
    private const int BoxGap = 7;

    private bool _checked;
    private bool _hover;

    public CheckTile()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        // 用不透明底色，理由同 FlatButton：同容器内多个透明控件会互相重绘出重影。
        BackColor = Theme.Surface;
        Font = Theme.Text(9f);
        Cursor = Cursors.Hand;
        TabStop = true;

        FitToText();
    }

    /// <summary>
    /// 按文字实际宽度算尺寸，避免各语言的字体度量差异把标签截断成「时…」。
    /// </summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(
            LabelText,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        return new Size(BoxSize + BoxGap + text.Width + 2, Math.Max(BoxSize + 6, text.Height + 2));
    }

    private void FitToText() => Size = GetPreferredSize(Size.Empty);

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value ?? string.Empty;
            FitToText();
            Invalidate();
        }
    }

    private string _labelText = string.Empty;

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitToText();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var box = new RectangleF(0f, (Height - BoxSize) / 2f, BoxSize, BoxSize);

        using (GraphicsPath boxPath = Theme.RoundRect(box, 5))
        {
            if (_checked)
            {
                using var fill = new SolidBrush(Enabled ? Theme.Accent : Theme.Line);
                g.FillPath(fill, boxPath);
            }
            else
            {
                using var fill = new SolidBrush(_hover && Enabled ? Theme.FieldHover : Theme.Field);
                g.FillPath(fill, boxPath);

                using var border = new Pen(Theme.Line, 1f) { Alignment = PenAlignment.Inset };
                g.DrawPath(border, boxPath);
            }
        }

        if (_checked)
        {
            using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(
                pen,
                new[]
                {
                    new PointF(box.X + 3.8f, box.Y + BoxSize / 2f),
                    new PointF(box.X + BoxSize * 0.44f, box.Y + BoxSize - 4.6f),
                    new PointF(box.X + BoxSize - 3.8f, box.Y + 4.4f),
                });
        }

        Color textColor = Enabled ? Theme.TextBody : Theme.TextFaint;
        var textRect = new Rectangle(BoxSize + BoxGap, 0, Width - BoxSize - BoxGap, Height);

        const TextFormatFlags flags =
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.Left |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis;

        TextRenderer.DrawText(g, LabelText, Font, textRect, textColor, flags);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        Focus();
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}

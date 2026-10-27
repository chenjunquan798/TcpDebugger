using System.Drawing;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 标题 + 输入控件的小组合，让连接区字段排版保持一致。
/// </summary>
internal sealed class LabeledField : UserControl
{
    public LabeledField(string caption, Control body, int width, int bodyHeight = 32)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Surface;
        Size = new Size(width, 20 + bodyHeight);

        Caption = new Label
        {
            Text = caption,
            ForeColor = Theme.TextDim,
            Font = Theme.Text(8.25f),
            AutoSize = false,
            Height = 16,
            Width = width,
            Location = new Point(0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        Body = body;
        body.Size = new Size(width, bodyHeight);
        body.Location = new Point(0, 20);

        Controls.Add(Body);
        Controls.Add(Caption);
    }

    public Label Caption { get; }

    public Control Body { get; }

    public void SetCaption(string text) => Caption.Text = text;

    public void Enable(bool enabled)
    {
        Caption.ForeColor = enabled ? Theme.TextDim : Theme.TextFaint;
        Body.Enabled = enabled;
    }
}

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 分段选择器，用来替代枯燥的单选按钮组。
/// </summary>
internal sealed class SegmentedControl : Control
{
    private const int Pad = 3;

    private string[] _items = { "选项 A", "选项 B" };
    private int _selectedIndex;
    private int _hoverIndex = -1;

    public SegmentedControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        BackColor = Theme.Field;
        ForeColor = Theme.TextDim;
        Font = Theme.Text(9f);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public event EventHandler? SelectedIndexChanged;

    public string[] Items
    {
        get => _items;
        set
        {
            if (value.Length == 0)
            {
                return;
            }

            _items = value;
            _selectedIndex = Math.Clamp(_selectedIndex, 0, value.Length - 1);
            Invalidate();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= _items.Length || value == _selectedIndex)
            {
                return;
            }

            _selectedIndex = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string SelectedItem => _items[_selectedIndex];

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var track = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        using (GraphicsPath trackPath = Theme.RoundRect(track, Radius))
        using (var brush = new SolidBrush(Theme.Field))
        {
            g.FillPath(brush, trackPath);
        }

        int count = _items.Length;
        float usable = track.Width - Pad * 2f;
        float segWidth = usable / count;

        for (int i = 0; i < count; i++)
        {
            var segment = new RectangleF(
                Pad + i * segWidth + 1f,
                Pad,
                segWidth - 2f,
                track.Height - Pad * 2f);

            bool active = i == _selectedIndex;

            if (active)
            {
                using GraphicsPath segPath = Theme.RoundRect(segment, Radius - 2);
                using var brush = new SolidBrush(Enabled ? Theme.Accent : Theme.Line);
                g.FillPath(brush, segPath);
            }
            else if (i == _hoverIndex && Enabled)
            {
                using GraphicsPath segPath = Theme.RoundRect(segment, Radius - 2);
                using var brush = new SolidBrush(Theme.FieldHover);
                g.FillPath(brush, segPath);
            }

            Color textColor = active
                ? Color.White
                : Enabled
                    ? Theme.TextDim
                    : Theme.TextFaint;

            const TextFormatFlags flags =
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis;

            TextRenderer.DrawText(g, _items[i], Font, Rectangle.Round(segment), textColor, flags);
        }
    }

    public int Radius { get; set; } = 9;

    private int HitTest(Point location)
    {
        float usable = Width - Pad * 2f;
        float segWidth = usable / _items.Length;
        if (segWidth <= 0)
        {
            return -1;
        }

        int index = (int)((location.X - Pad) / segWidth);
        return index >= 0 && index < _items.Length ? index : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = HitTest(e.Location);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int index = HitTest(e.Location);
            if (index >= 0)
            {
                SelectedIndex = index;
                Focus();
            }
        }

        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
        {
            SelectedIndex = Math.Max(0, _selectedIndex - 1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
        {
            SelectedIndex = Math.Min(_items.Length - 1, _selectedIndex + 1);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}

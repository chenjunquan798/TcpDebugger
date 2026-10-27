using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 自绘下拉框。原生 ComboBox 无法做出满意的深色圆角样式，这里用 ContextMenuStrip 实现弹出。
/// </summary>
internal sealed class DropDown : Control
{
    private readonly ContextMenuStrip _menu;
    private bool _hover;
    private string[] _items = Array.Empty<string>();

    public DropDown()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        BackColor = Theme.Field;
        Font = Theme.Text(9f);
        Cursor = Cursors.Hand;
        TabStop = true;

        _menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            AutoClose = true,
        };

        _menu.Closed += (_, _) => Invalidate();
    }

    public event EventHandler? ValueChanged;

    public int Radius { get; set; } = 8;

    public string[] Items
    {
        get => _items;
        set
        {
            _items = value;
            if (_items.Length > 0 && !_items.Contains(_value))
            {
                _value = _items[0];
                Invalidate();
            }
        }
    }

    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        using GraphicsPath path = Theme.RoundRect(rect, Radius);
        using (var brush = new SolidBrush(_menu.Visible || _hover ? Theme.FieldHover : BackColor))
        {
            g.FillPath(brush, path);
        }

        using var pen = new Pen(_menu.Visible || _hover ? Theme.Line : Theme.Line, 1f) { Alignment = PenAlignment.Inset };
        g.DrawPath(pen, path);

        var textRect = new Rectangle(11, 0, Width - 30, Height);

        const TextFormatFlags flags =
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding;

        TextRenderer.DrawText(g, _value, Font, textRect, Theme.TextBody, flags);

        // 箭头
        float cx = Width - 16f;
        float cy = Height / 2f;
        using var arrow = new SolidBrush(Theme.TextDim);
        g.FillPolygon(
            arrow,
            new[]
            {
                new PointF(cx - 4.5f, cy - 2f),
                new PointF(cx + 3.5f, cy - 2f),
                new PointF(cx - 0.5f, cy + 2.5f),
            });
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

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            OpenMenu();
        }

        base.OnMouseClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
        {
            OpenMenu();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OpenMenu()
    {
        if (_items.Length == 0)
        {
            return;
        }

        _menu.Items.Clear();

        foreach (string item in _items)
        {
            var menuItem = new ToolStripMenuItem(item)
            {
                AutoSize = true,
                ForeColor = Theme.TextBody,
                BackColor = Theme.SurfaceAlt,
            };

            menuItem.Click += (_, _) => Value = item;
            _menu.Items.Add(menuItem);
        }

        _menu.MinimumSize = new Size(Math.Max(Width, 120), 0);
        _menu.Show(this, new Point(0, Height + 4));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}

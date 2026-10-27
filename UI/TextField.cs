using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 圆角输入框。内部包了一个无边框 TextBox，外层负责画出深色背景与聚焦描边。
/// </summary>
internal sealed class TextField : Control
{
    private readonly TextBox _box;
    private bool _focused;
    private string? _placeholder;

    public TextField()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Field;

        _box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Field,
            ForeColor = Theme.TextBody,
            Font = Theme.Text(9f),
        };

        _box.GotFocus += (_, _) =>
        {
            _focused = true;
            Invalidate();
        };

        _box.LostFocus += (_, _) =>
        {
            _focused = false;
            Invalidate();
        };

        _box.TextChanged += (_, _) =>
        {
            OnTextChanged(EventArgs.Empty);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };

        _box.KeyDown += (_, args) => KeyDownFired?.Invoke(this, args);
        _box.HandleCreated += (_, _) => WinApi.UseDarkScrollBars(_box.Handle);

        Controls.Add(_box);
    }

    public event EventHandler? ValueChanged;

    public event KeyEventHandler? KeyDownFired;

    public TextBox Inner => _box;

    public int Radius { get; set; } = 8;

    public string Placeholder
    {
        get => _placeholder ?? string.Empty;
        set
        {
            _placeholder = value;
            ApplyPlaceholder();
        }
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value;
    }

    public bool ReadOnlyBox
    {
        get => _box.ReadOnly;
        set
        {
            _box.ReadOnly = value;
            _box.BackColor = value ? Theme.Surface : Theme.Field;
            Invalidate();
        }
    }

    public bool Wrap
    {
        get => _box.WordWrap;
        set => _box.WordWrap = value;
    }

    public ScrollBars Scroll
    {
        get => _box.ScrollBars;
        set => _box.ScrollBars = value;
    }

    public void SetMultiline(bool multiline)
    {
        _box.Multiline = multiline;
        _box.AcceptsReturn = multiline;
        PerformLayout();
        Invalidate();
    }

    public void SelectAllText() => _box.SelectAll();

    public void AppendTextSafe(string text)
    {
        _box.AppendText(text);
    }

    public void FocusInput() => _box.Focus();

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        const int padX = 11;

        if (_box.Multiline)
        {
            _box.Bounds = new Rectangle(padX, 8, Math.Max(1, Width - padX * 2), Math.Max(1, Height - 16));
            _box.TextAlign = HorizontalAlignment.Left;
        }
        else
        {
            int boxHeight = Math.Max(_box.Height, Math.Min(Height - 8, 26));
            _box.Bounds = new Rectangle(padX, (Height - boxHeight) / 2, Math.Max(1, Width - padX * 2), boxHeight);
        }
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        ApplyPlaceholder();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _box.BackColor = Enabled ? BackColor : Theme.Surface;
        _box.ForeColor = Enabled ? Theme.TextBody : Theme.TextFaint;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

        using GraphicsPath path = Theme.RoundRect(rect, Radius);
        using (var brush = new SolidBrush(Enabled ? BackColor : Theme.Surface))
        {
            g.FillPath(brush, path);
        }

        Color border = !Enabled
            ? Theme.LineSoft
            : _focused
                ? Theme.Accent
                : Theme.Line;

        using var pen = new Pen(border, _focused ? 1.4f : 1f) { Alignment = PenAlignment.Inset };
        g.DrawPath(pen, path);
    }

    private void ApplyPlaceholder()
    {
        if (!string.IsNullOrEmpty(_placeholder) && _box.IsHandleCreated)
        {
            WinApi.SetCueBanner(_box.Handle, _placeholder);
        }
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace TcpDebugger.UI;

/// <summary>
/// 客户端列表。原生 ListView 在深色背景下没法看，这里全部自绘。
/// </summary>
internal sealed class ClientListView : ListView
{
    private const int RowHeight = 30;

    private readonly ImageList _rowHeight;

    public ClientListView()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint,
            true);

        OwnerDraw = true;
        View = View.Details;
        FullRowSelect = true;
        GridLines = false;
        MultiSelect = false;
        HideSelection = false;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Surface;
        ForeColor = Theme.TextBody;
        Activation = ItemActivation.Standard;

        Columns.Add(string.Empty, 26);
        Columns.Add("客户端", 100);
        Columns.Add("接入时间", 96);

        // ListView 的行高由 SmallImageList 决定，这里塞一张透明占位图把行高撑到 30。
        //
        // 注意：ImageList 会直接持有传入的位图而不做拷贝，等 ListView 建句柄时再去读它。
        // 因此这张占位图绝不能提前 Dispose，必须留给 ImageList 自己释放——
        // 否则 CreateBitmap 读已释放的位图会抛 "Parameter is not valid"。
        _rowHeight = new ImageList
        {
            ImageSize = new Size(1, RowHeight),
            ColorDepth = ColorDepth.Depth32Bit,
            TransparentColor = Color.Transparent,
        };
        _rowHeight.Images.Add("row", new Bitmap(1, RowHeight));
        SmallImageList = _rowHeight;
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Smooth(g);

        using (var brush = new SolidBrush(Theme.SurfaceAlt))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        using var pen = new Pen(Theme.LineSoft);
        g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        if (string.IsNullOrEmpty(e.Header?.Text))
        {
            return;
        }

        var rect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height);

        const TextFormatFlags flags =
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix;

        TextRenderer.DrawText(g, e.Header.Text, SmallTitle, rect, Theme.TextDim, flags);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        // 每个单元格交由 DrawSubItem 绘制，这里保持静默以避免重复绘制背景。
        e.DrawDefault = false;
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.Item is null)
        {
            return;
        }

        Graphics g = e.Graphics;
        Theme.Smooth(g);

        bool selected = e.Item.Selected;
        bool hot = (e.ItemState & ListViewItemStates.Hot) != 0;

        Color back = selected
            ? Theme.AccentSoft
            : hot
                ? Theme.SurfaceAlt
                : Theme.Surface;

        using (var brush = new SolidBrush(back))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        if (e.ColumnIndex == 0)
        {
            float cy = e.Bounds.Top + e.Bounds.Height / 2f;
            using var dot = new SolidBrush(selected ? Theme.AccentHi : Theme.Success);
            g.FillEllipse(dot, e.Bounds.Left + 9f, cy - 3f, 6f, 6f);
            return;
        }

        Color textColor = e.ColumnIndex == 2 ? Theme.TextFaint : Theme.TextBody;
        var rect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 6, e.Bounds.Height);

        const TextFormatFlags flags =
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine;

        TextRenderer.DrawText(g, e.SubItem?.Text ?? string.Empty, Font, rect, textColor, flags);
    }

    public Font SmallTitle { get; set; } = Theme.Text(8.25f);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rowHeight.Dispose();
        }

        base.Dispose(disposing);
    }
}

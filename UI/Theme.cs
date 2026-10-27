using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TcpDebugger.UI;

/// <summary>
/// 全局配色与字体。整个界面走深蓝灰现代风。
/// </summary>
internal static class Theme
{
    public static readonly Color Backdrop = ColorTranslator.FromHtml("#0B0E13");
    public static readonly Color Surface = ColorTranslator.FromHtml("#12161D");
    public static readonly Color SurfaceAlt = ColorTranslator.FromHtml("#171C25");
    public static readonly Color Field = ColorTranslator.FromHtml("#1B212B");
    public static readonly Color FieldHover = ColorTranslator.FromHtml("#212836");
    public static readonly Color Line = ColorTranslator.FromHtml("#252C38");
    public static readonly Color LineSoft = ColorTranslator.FromHtml("#1E242E");

    public static readonly Color TextBody = ColorTranslator.FromHtml("#E4E9F2");
    public static readonly Color TextDim = ColorTranslator.FromHtml("#7C8798");
    public static readonly Color TextFaint = ColorTranslator.FromHtml("#525B6B");

    public static readonly Color Accent = ColorTranslator.FromHtml("#3B82F6");
    public static readonly Color AccentHi = ColorTranslator.FromHtml("#60A5FA");
    public static readonly Color AccentSoft = ColorTranslator.FromHtml("#1A2740");

    public static readonly Color Success = ColorTranslator.FromHtml("#2DD4A7");
    public static readonly Color Danger = ColorTranslator.FromHtml("#F2555A");
    public static readonly Color Warn = ColorTranslator.FromHtml("#F5A524");

    public static readonly Color Rx = ColorTranslator.FromHtml("#37D6B0");
    public static readonly Color Tx = ColorTranslator.FromHtml("#FFAE57");

    public static readonly string UiFontName = PickFont("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");
    public static readonly string MonoFontName = PickFont("Cascadia Mono", "Consolas", "Courier New");

    public static Font Text(float pt, FontStyle style = FontStyle.Regular) =>
        new(UiFontName, pt, style, GraphicsUnit.Point);

    public static Font Mono(float pt, FontStyle style = FontStyle.Regular) =>
        new(MonoFontName, pt, style, GraphicsUnit.Point);

    public static GraphicsPath RoundRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float r = Math.Max(0f, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));

        if (r <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = r * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    public static GraphicsPath RoundRect(Rectangle rect, float radius) => RoundRect((RectangleF)rect, radius);

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    private static string PickFont(params string[] candidates)
    {
        using var fonts = new InstalledFontCollection();
        foreach (string candidate in candidates)
        {
            foreach (FontFamily family in fonts.Families)
            {
                if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return candidates[^1];
    }
}

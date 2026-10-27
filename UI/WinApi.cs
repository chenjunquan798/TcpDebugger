using System.Runtime.InteropServices;

namespace TcpDebugger.UI;

internal static class WinApi
{
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_SETREDRAW = 0x000B;
    public const int EM_SETCUEBANNER = 0x1501;

    public const int HTCAPTION = 0x0002;
    public const int HTLEFT = 0x000A;
    public const int HTRIGHT = 0x000B;
    public const int HTTOP = 0x000C;
    public const int HTTOPLEFT = 0x000D;
    public const int HTTOPRIGHT = 0x000E;
    public const int HTBOTTOM = 0x000F;
    public const int HTBOTTOMLEFT = 0x0010;
    public const int HTBOTTOMRIGHT = 0x0011;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);

    /// <summary>
    /// 把原生滚动条切到深色主题。RichTextBox / TextBox 的滚动条是系统画的，
    /// 不处理的话会在深色界面里拉出一条刺眼的白带。
    /// </summary>
    public static void UseDarkScrollBars(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetWindowTheme(handle, "DarkMode_Explorer", null);
        }
        catch
        {
            // 老系统没有 uxtheme 深色主题，保持默认即可
        }
    }

    public static void SetCueBanner(IntPtr handle, string text)
    {
        IntPtr pointer = Marshal.StringToHGlobalUni(text);
        try
        {
            SendMessage(handle, EM_SETCUEBANNER, (IntPtr)1, pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}

using System.Text;
using System.Windows.Forms;

using TcpDebugger.Core;

namespace TcpDebugger;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // 启用 GBK / GB2312 等中文编码支持
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();

        if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSelfTest();
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args2) => Report(args2.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args2) =>
            Report(args2.ExceptionObject as Exception ?? new Exception("未知的非托管异常"));

        using var form = new Forms.MainForm();
        Application.Run(form);
        return 0;
    }

    /// <summary>
    /// 把异常写进崩溃日志。调试工具自己崩了却没有可查的线索是最糟的，
    /// 所以除了弹窗，也落一份 UTF-8 文本到 exe 同目录。
    /// </summary>
    private static void LogCrash(string text)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            string file = Path.Combine(dir, "crash.log");
            File.AppendAllText(file, text, new UTF8Encoding(false));
        }
        catch
        {
            // 写日志失败不能影响主流程
        }
    }

    /// <summary>
    /// 自检：跑完界面探针 + 编解码 + 真实回环通信，输出每条结果并返回退出码。
    /// </summary>
    private static int RunSelfTest()
    {
        // 窗口过程里的异常（比如控件建句柄时炸）会被 WinForms 吞进 Application.ThreadException
        // 而不往外抛。自检必须挂上钩子，否则这类只在真实建句柄时才暴露的问题会悄悄溜过去。
        var uiErrors = new List<Exception>();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => uiErrors.Add(e.Exception);

        var cases = new List<SelfTestCase>();
        cases.AddRange(Forms.MainForm.ProbeUi());
        cases.AddRange(SelfTest.RunCodecCases());
        cases.AddRange(SelfTest.RunCommCases());

        if (uiErrors.Count > 0)
        {
            foreach (Exception error in uiErrors)
            {
                cases.Add(new SelfTestCase(
                    "ui/wndproc-exception",
                    false,
                    $"{error.GetType().Name}：{error.Message}"));
            }
        }
        else
        {
            cases.Add(new SelfTestCase("ui/wndproc-exception", true, "控件建句柄与绘制过程中没有异常"));
        }

        int failed = cases.Count(c => !c.Passed);

        var report = new StringBuilder();
        report.AppendLine("=== TCP 调试助手 自检 ===");
        foreach (SelfTestCase item in cases)
        {
            report.AppendLine($"[{(item.Passed ? "PASS" : "FAIL")}] {item.Name}  {item.Detail}");
        }

        report.AppendLine($"=== 共 {cases.Count} 项，通过 {cases.Count - failed} 项，失败 {failed} 项 ===");

        // 报告里有大量中文。重定向到文件或管道时 Console 仍会走 ANSI 代码页，
        // 设置 OutputEncoding 未必生效，所以这里直接按 UTF-8 字节写入标准输出流。
        using Stream stdout = Console.OpenStandardOutput();
        byte[] bytes = new UTF8Encoding(false).GetBytes(report.ToString());
        stdout.Write(bytes, 0, bytes.Length);
        stdout.Flush();

        return failed == 0 ? 0 : 1;
    }

    private static void Report(Exception ex)
    {
        LogCrash(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}"
            + ex.StackTrace
            + (ex.InnerException is { } inner
                ? $"{Environment.NewLine}--- 内部异常 ---{Environment.NewLine}{inner}"
                : string.Empty)
            + Environment.NewLine + Environment.NewLine);

        MessageBox.Show(
            ex.Message + "\n\n" + ex.StackTrace,
            "TCP 调试助手 - 未处理的异常",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

using System.Text;

namespace TcpDebugger.Core;

internal sealed record SelfTestCase(string Name, bool Passed, string Detail);

/// <summary>
/// 进程内自检。以 --selftest 启动时会跑完后直接退出，方便在没有桌面会话的环境里
/// 验证编解码与真实 TCP 收发链路是否正常。
/// </summary>
internal static class SelfTest
{
    public static List<SelfTestCase> RunCodecCases()
    {
        var cases = new List<SelfTestCase>();

        byte[] hello = FrameCodec.ParseHex("48 65 6C 6C 6F");
        cases.Add(new SelfTestCase(
            "codec/parse-hex-basic",
            hello.Length == 5 && Encoding.ASCII.GetString(hello) == "Hello",
            $"得到 [{FrameCodec.ToHex(hello)}]"));

        byte[] prefixed = FrameCodec.ParseHex("0x48,0x65;0x6C-0x6C_0x6F");
        cases.Add(new SelfTestCase(
            "codec/parse-hex-tolerates-noise",
            prefixed.Length == 5 && Encoding.ASCII.GetString(prefixed) == "Hello",
            $"带 0x 前缀与多种分隔符时得到 [{FrameCodec.ToHex(prefixed)}]"));

        bool threw = false;
        try
        {
            FrameCodec.ParseHex("48 65 6");
        }
        catch (FormatException)
        {
            threw = true;
        }

        cases.Add(new SelfTestCase(
            "codec/parse-hex-rejects-odd-length",
            threw,
            "奇数个十六进制字符应抛 FormatException"));

        threw = false;
        try
        {
            FrameCodec.ParseHex("ZZ");
        }
        catch (FormatException)
        {
            threw = true;
        }

        cases.Add(new SelfTestCase(
            "codec/parse-hex-rejects-bad-char",
            threw,
            "非十六进制字符应抛 FormatException"));

        byte[] escaped = FrameCodec.FromText("a\\tb", Encoding.UTF8, true, false);
        cases.Add(new SelfTestCase(
            "codec/text-escape",
            escaped.Length == 3 && escaped[1] == 0x09,
            $"输入 a\\tb 得到 [{FrameCodec.ToHex(escaped)}]"));

        byte[] raw = FrameCodec.FromText("a\\tb", Encoding.UTF8, false, false);
        cases.Add(new SelfTestCase(
            "codec/text-no-escape",
            raw.Length == 4 && raw[1] == 0x5C,
            $"关闭转义后应保留反斜杠，得到 [{FrameCodec.ToHex(raw)}]"));

        byte[] crlf = FrameCodec.FromText(string.Empty, Encoding.UTF8, true, true);
        cases.Add(new SelfTestCase(
            "codec/append-crlf",
            crlf.Length == 2 && crlf[0] == 0x0D && crlf[1] == 0x0A,
            $"空输入 + 追加换行得到 [{FrameCodec.ToHex(crlf)}]"));

        byte[] utf8 = FrameCodec.FromText("温度", Encoding.UTF8, true, false);
        cases.Add(new SelfTestCase(
            "codec/utf8-multibyte",
            utf8.Length == 6,
            $"中文两字应为 6 字节，得到 {utf8.Length} 字节 [{FrameCodec.ToHex(utf8)}]"));

        byte[] printable = Encoding.ASCII.GetBytes("Hi!");
        List<string> both = FrameCodec.RenderLines(printable, PayloadView.Both);
        cases.Add(new SelfTestCase(
            "codec/render-both",
            both.Count == 1 && both[0].StartsWith("48 69 21", StringComparison.Ordinal) && both[0].EndsWith("|Hi!|", StringComparison.Ordinal),
            "HEX + 字符双栏渲染：" + both[0]));

        byte[] sample = { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0xFF, 0x41 };
        List<string> control = FrameCodec.RenderLines(sample, PayloadView.Both);
        cases.Add(new SelfTestCase(
            "codec/render-both-control-chars",
            control.Count == 1 && control[0].Contains("Hello\\0.A"),
            "不可打印字符应以转义形式出现：" + control[0]));

        List<string> asciiOnly = FrameCodec.RenderLines(sample, PayloadView.Ascii);
        cases.Add(new SelfTestCase(
            "codec/render-ascii",
            asciiOnly.Count == 1 && asciiOnly[0].Contains("\\0"),
            "字符模式渲染：" + asciiOnly[0]));

        var longData = new byte[40];
        for (int i = 0; i < longData.Length; i++)
        {
            longData[i] = (byte)i;
        }

        List<string> paged = FrameCodec.RenderLines(longData, PayloadView.Hex);
        cases.Add(new SelfTestCase(
            "codec/render-multiline",
            paged.Count == 3 && paged[1].StartsWith("0010", StringComparison.Ordinal),
            $"40 字节应折成 3 行，实际 {paged.Count} 行"));

        List<string> empty = FrameCodec.RenderLines(Array.Empty<byte>(), PayloadView.Both);
        cases.Add(new SelfTestCase(
            "codec/render-empty",
            empty.Count == 1 && empty[0] == "<空>",
            "空报文应有占位显示"));

        return cases;
    }

    public static List<SelfTestCase> RunCommCases()
    {
        var cases = new List<SelfTestCase>();
        using var server = new TcpServerService(0);

        using var peerUp = new ManualResetEventSlim(false);
        using var clientUp = new ManualResetEventSlim(false);
        using var serverRx = new ManualResetEventSlim(false);
        using var clientRx = new ManualResetEventSlim(false);

        var serverGot = new List<byte[]>();
        var clientGot = new List<byte[]>();

        server.PeerChanged += (_, e) =>
        {
            if (e.Connected)
            {
                peerUp.Set();
            }
        };

        server.DataReceived += (_, e) =>
        {
            lock (serverGot)
            {
                serverGot.Add(e.Payload);
            }

            serverRx.Set();
        };

        try
        {
            server.Start();
        }
        catch (Exception ex)
        {
            cases.Add(new SelfTestCase("comm/server-start", false, "无法启动监听：" + ex.Message));
            return cases;
        }

        int port = server.BoundPort;
        cases.Add(new SelfTestCase(
            "comm/server-bind-ephemeral",
            port is > 0 and <= 65535,
            $"传入端口 0 后系统分配到 {port}"));

        using var client = new TcpClientService("127.0.0.1", port);

        client.PeerChanged += (_, e) =>
        {
            if (e.Connected)
            {
                clientUp.Set();
            }
        };

        client.DataReceived += (_, e) =>
        {
            lock (clientGot)
            {
                clientGot.Add(e.Payload);
            }

            clientRx.Set();
        };

        client.Start();

        bool peerJoined = peerUp.Wait(TimeSpan.FromSeconds(8));
        bool clientJoined = clientUp.Wait(TimeSpan.FromSeconds(8));

        cases.Add(new SelfTestCase(
            "comm/handshake",
            peerJoined && clientJoined,
            $"服务端收到接入={peerJoined}，客户端连上={clientJoined}"));

        cases.Add(new SelfTestCase(
            "comm/peer-count",
            server.PeerCount == 1,
            $"服务端会话数={server.PeerCount}"));

        // 客户端 -> 服务端
        byte[] ping = Encoding.UTF8.GetBytes("Hello TCP");
        client.Send(TcpServerService.BroadcastId, ping);

        bool gotPing = serverRx.Wait(TimeSpan.FromSeconds(8));
        byte[]? serverFirst = serverGot.Count > 0 ? serverGot[0] : null;
        cases.Add(new SelfTestCase(
            "comm/client-to-server",
            gotPing && serverFirst is not null && serverFirst.AsSpan().SequenceEqual(ping),
            $"服务端收到 [{FrameCodec.ToReadable(serverFirst ?? Array.Empty<byte>())}]"));

        // 服务端 -> 客户端（广播）
        byte[] pong = { 0x01, 0x02, 0x03, 0xFE, 0xFF };
        server.Send(TcpServerService.BroadcastId, pong);

        bool gotPong = clientRx.Wait(TimeSpan.FromSeconds(8));
        byte[]? clientFirst = clientGot.Count > 0 ? clientGot[0] : null;
        cases.Add(new SelfTestCase(
            "comm/server-to-client",
            gotPong && clientFirst is not null && clientFirst.AsSpan().SequenceEqual(pong),
            $"客户端收到 [{FrameCodec.ToHex(clientFirst ?? Array.Empty<byte>())}]"));

        // 大包分帧不丢字节。此处只统计本次批量发送之后到达的分片。
        var bulk = new byte[64 * 1024];
        new Random(2026).NextBytes(bulk);
        int baseline;
        lock (serverGot)
        {
            baseline = serverGot.Select(p => p.Length).Sum();
        }

        serverRx.Reset();
        client.Send(TcpServerService.BroadcastId, bulk);

        int received = 0;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && received < bulk.Length)
        {
            serverRx.Wait(TimeSpan.FromMilliseconds(400));
            serverRx.Reset();
            lock (serverGot)
            {
                received = serverGot.Select(p => p.Length).Sum() - baseline;
            }
        }

        int frames;
        lock (serverGot)
        {
            frames = serverGot.Count;
        }

        cases.Add(new SelfTestCase(
            "comm/bulk-transfer",
            received == bulk.Length,
            $"发送 {bulk.Length} 字节，服务端累计收齐 {received} 字节（分 {frames - 1} 个到达批次）"));

        client.Stop();
        cases.Add(new SelfTestCase(
            "comm/client-stop",
            !client.IsRunning,
            "主动断开后客户端不再处于连接状态"));

        return cases;
    }
}

using System.Text;

namespace TcpDebugger.Core;

/// <summary>
/// 报文展示格式。
/// </summary>
internal enum PayloadView
{
    /// <summary>只显示可打印字符</summary>
    Ascii,

    /// <summary>只显示十六进制</summary>
    Hex,

    /// <summary>十六进制 + 右侧可读字符</summary>
    Both,
}

/// <summary>
/// HEX / 文本互转，以及日志侧的可读渲染。
/// </summary>
internal static class FrameCodec
{
    /// <summary>
    /// 解析十六进制输入。容忍空格、换行、逗号、分号、冒号、连字符以及 0x 前缀。
    /// </summary>
    public static byte[] ParseHex(string raw)
    {
        var digits = new List<byte>(raw.Length / 2 + 1);

        int index = 0;
        while (index < raw.Length)
        {
            char c = raw[index];

            if (char.IsWhiteSpace(c) || c is ',' or ';' or ':' or '-' or '_' or '|')
            {
                index++;
                continue;
            }

            if (c == '0' && index + 1 < raw.Length && raw[index + 1] is 'x' or 'X')
            {
                index += 2;
                continue;
            }

            digits.Add((byte)HexValue(c, index));
            index++;
        }

        if (digits.Count == 0)
        {
            return Array.Empty<byte>();
        }

        if (digits.Count % 2 != 0)
        {
            throw new FormatException("十六进制字符个数必须是偶数，当前为 " + digits.Count + " 个。");
        }

        var result = new byte[digits.Count / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)((digits[i * 2] << 4) | digits[i * 2 + 1]);
        }

        return result;
    }

    private static int HexValue(char c, int position)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;

        throw new FormatException($"第 {position + 1} 个字符 “{c}” 不是合法的十六进制字符。");
    }

    /// <summary>
    /// 把文本按指定编码转成字节，可选解析 \r \n \t \0 转义并追加 CRLF。
    /// </summary>
    public static byte[] FromText(string text, Encoding encoding, bool parseEscape, bool appendCrLf)
    {
        if (string.IsNullOrEmpty(text))
        {
            return appendCrLf ? new byte[] { 0x0D, 0x0A } : Array.Empty<byte>();
        }

        if (parseEscape)
        {
            text = text
                .Replace("\\r\\n", "\r\n", StringComparison.OrdinalIgnoreCase)
                .Replace("\\r", "\r", StringComparison.OrdinalIgnoreCase)
                .Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase)
                .Replace("\\t", "\t", StringComparison.OrdinalIgnoreCase)
                .Replace("\\0", "\0", StringComparison.Ordinal);
        }

        if (appendCrLf)
        {
            text += "\r\n";
        }

        return encoding.GetBytes(text);
    }

    /// <summary>
    /// 一段字节转成十六进制文本。
    /// </summary>
    public static string ToHex(ReadOnlySpan<byte> data, int width = 0)
    {
        var sb = new StringBuilder(data.Length * 3);
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(width > 0 && i % width == 0 ? Environment.NewLine : ' ');
            }

            sb.Append(data[i].ToString("X2"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 一段字节转成可读文本，控制字符用转义形式表示，不可打印字符用 '.' 代替。
    /// </summary>
    public static string ToReadable(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        foreach (byte b in data)
        {
            switch (b)
            {
                case 0x0D: sb.Append("\\r"); break;
                case 0x0A: sb.Append("\\n"); break;
                case 0x09: sb.Append("\\t"); break;
                case 0x00: sb.Append("\\0"); break;
                default:
                    sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 按展示格式把一段数据渲染成若干日志行。
    /// </summary>
    public static List<string> RenderLines(ReadOnlySpan<byte> data, PayloadView view, int width = 16)
    {
        var lines = new List<string>();
        if (data.Length == 0)
        {
            lines.Add("<空>");
            return lines;
        }

        bool singleChunk = data.Length <= width;

        for (int offset = 0; offset < data.Length; offset += width)
        {
            int take = Math.Min(width, data.Length - offset);
            var chunk = data.Slice(offset, take);

            string body = view switch
            {
                PayloadView.Ascii => ToReadable(chunk),
                PayloadView.Hex => ToHex(chunk),
                _ => Pad(ToHex(chunk), width) + "  |" + ToReadable(chunk) + "|",
            };

            lines.Add(singleChunk ? body : offset.ToString("X4") + "  " + body);
        }

        return lines;
    }

    private static string Pad(string hex, int width)
    {
        int target = width * 3 - 1;
        return hex.Length >= target ? hex : hex.PadRight(target);
    }

    /// <summary>
    /// 把一段数据压成单行摘要，用于紧凑模式。
    /// </summary>
    public static string ToSingleLine(ReadOnlySpan<byte> data, PayloadView view)
    {
        return view switch
        {
            PayloadView.Ascii => ToReadable(data),
            PayloadView.Hex => ToHex(data),
            _ => ToReadable(data) + "   " + ToHex(data),
        };
    }
}

using System.Text;

namespace Go212.POS.Infrastructure.Printing;

/// <summary>
/// Fluent byte buffer builder for creating structured ESC/POS thermal receipts.
/// Supports standard 80mm (42 columns) and 58mm (32 columns) thermal printers.
/// </summary>
public class EscPosReceiptBuilder
{
    private readonly MemoryStream _stream = new();
    private readonly int _lineWidth;
    private readonly Encoding _encoding;

    public EscPosReceiptBuilder(int lineWidth = 42, string codePage = "CP850")
    {
        _lineWidth = lineWidth;
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(codePage);
        }
        catch
        {
            _encoding = Encoding.UTF8;
        }

        // Initialize printer
        WriteBytes(EscPosCommands.Initialize);
    }

    public EscPosReceiptBuilder AppendText(string text)
    {
        var bytes = _encoding.GetBytes(text);
        _stream.Write(bytes, 0, bytes.Length);
        return this;
    }

    public EscPosReceiptBuilder AppendLine(string text = "")
    {
        AppendText(text);
        WriteBytes(EscPosCommands.LineFeed);
        return this;
    }

    public EscPosReceiptBuilder CenterText(string text, bool bold = false, bool doubleSize = false)
    {
        WriteBytes(EscPosCommands.AlignCenter);
        if (bold) WriteBytes(EscPosCommands.BoldOn);
        if (doubleSize) WriteBytes(EscPosCommands.DoubleSize);

        AppendLine(text);

        if (doubleSize) WriteBytes(EscPosCommands.NormalSize);
        if (bold) WriteBytes(EscPosCommands.BoldOff);
        WriteBytes(EscPosCommands.AlignLeft);
        return this;
    }

    public EscPosReceiptBuilder LeftRightLine(string left, string right, bool bold = false)
    {
        if (bold) WriteBytes(EscPosCommands.BoldOn);
        WriteBytes(EscPosCommands.AlignLeft);

        int spacesNeeded = _lineWidth - left.Length - right.Length;
        if (spacesNeeded < 1)
        {
            // Truncate left text to fit
            int maxLeft = Math.Max(1, _lineWidth - right.Length - 1);
            left = left.Length > maxLeft ? left.Substring(0, maxLeft) : left;
            spacesNeeded = Math.Max(1, _lineWidth - left.Length - right.Length);
        }

        string line = left + new string(' ', spacesNeeded) + right;
        AppendLine(line);

        if (bold) WriteBytes(EscPosCommands.BoldOff);
        return this;
    }

    public EscPosReceiptBuilder Divider(char pattern = '-')
    {
        AppendLine(new string(pattern, _lineWidth));
        return this;
    }

    public EscPosReceiptBuilder DoubleDivider()
    {
        AppendLine(new string('=', _lineWidth));
        return this;
    }

    public EscPosReceiptBuilder OpenCashDrawer()
    {
        WriteBytes(EscPosCommands.OpenDrawer);
        return this;
    }

    public EscPosReceiptBuilder FeedAndCut(int emptyLines = 4)
    {
        for (int i = 0; i < emptyLines; i++)
        {
            WriteBytes(EscPosCommands.LineFeed);
        }
        WriteBytes(EscPosCommands.PartialCut);
        return this;
    }

    public byte[] Build() => _stream.ToArray();

    private void WriteBytes(byte[] bytes)
    {
        _stream.Write(bytes, 0, bytes.Length);
    }
}

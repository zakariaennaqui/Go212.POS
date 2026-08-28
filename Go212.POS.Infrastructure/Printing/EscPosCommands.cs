namespace Go212.POS.Infrastructure.Printing;

/// <summary>
/// Standard ESC/POS binary command constants for 58mm and 80mm thermal receipt printers.
/// Conforms to Epson / Star standard POS thermal protocols.
/// </summary>
public static class EscPosCommands
{
    // Hardware control
    public static readonly byte[] Initialize = [0x1B, 0x40];           // ESC @
    public static readonly byte[] PartialCut = [0x1D, 0x56, 0x01];     // GS V 1
    public static readonly byte[] FullCut = [0x1D, 0x56, 0x00];        // GS V 0
    public static readonly byte[] OpenDrawer = [0x1B, 0x70, 0x00, 0x19, 0xFA]; // ESC p 0 25 250 (Pin 2 kick)
    public static readonly byte[] OpenDrawerPin5 = [0x1B, 0x70, 0x01, 0x19, 0xFA]; // ESC p 1 25 250 (Pin 5 kick)

    // Text Alignment
    public static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];      // ESC a 0
    public static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];    // ESC a 1
    public static readonly byte[] AlignRight = [0x1B, 0x61, 0x02];     // ESC a 2

    // Text Styling
    public static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];         // ESC E 1
    public static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];        // ESC E 0
    public static readonly byte[] UnderlineOn = [0x1B, 0x2D, 0x01];    // ESC - 1
    public static readonly byte[] UnderlineOff = [0x1B, 0x2D, 0x00];   // ESC - 0
    public static readonly byte[] InvertedOn = [0x1D, 0x42, 0x01];     // GS B 1
    public static readonly byte[] InvertedOff = [0x1D, 0x42, 0x00];    // GS B 0

    // Font Sizing
    public static readonly byte[] NormalSize = [0x1D, 0x21, 0x00];     // GS ! 0
    public static readonly byte[] DoubleHeight = [0x1D, 0x21, 0x01];   // GS ! 1
    public static readonly byte[] DoubleWidth = [0x1D, 0x21, 0x10];    // GS ! 16
    public static readonly byte[] DoubleSize = [0x1D, 0x21, 0x11];     // GS ! 17 (Double height + width)

    // Line Feeds
    public static readonly byte[] LineFeed = [0x0A];
    public static readonly byte[] Feed3Lines = [0x1B, 0x64, 0x03];     // ESC d 3
    public static readonly byte[] Feed5Lines = [0x1B, 0x64, 0x05];     // ESC d 5
}

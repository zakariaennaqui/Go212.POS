using System.Text;
using FluentAssertions;
using Go212.POS.Infrastructure.Printing;
using Xunit;

namespace Go212.POS.Tests;

public class ReceiptFormattingTests
{
    [Fact]
    public void EscPosReceiptBuilder_LeftRightLine_AlignsColumnsProperly()
    {
        var builder = new EscPosReceiptBuilder(lineWidth: 42);
        builder.LeftRightLine("Café Expresso", "15.00 MAD");

        var bytes = builder.Build();
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().Contain("Café Expresso");
        text.Should().Contain("15.00 MAD");
    }

    [Fact]
    public void EscPosReceiptBuilder_Divider_ProducesCorrectWidth()
    {
        var builder = new EscPosReceiptBuilder(lineWidth: 42);
        builder.Divider();

        var bytes = builder.Build();
        var text = Encoding.UTF8.GetString(bytes);

        text.Should().Contain(new string('-', 42));
    }

    [Fact]
    public void EscPosReceiptBuilder_CenterText_AddsAlignmentCommand()
    {
        var builder = new EscPosReceiptBuilder(lineWidth: 42);
        builder.CenterText("GO212 POS", bold: true);

        var bytes = builder.Build();
        bytes.Should().Contain(EscPosCommands.AlignCenter);
        bytes.Should().Contain(EscPosCommands.BoldOn);
    }
}

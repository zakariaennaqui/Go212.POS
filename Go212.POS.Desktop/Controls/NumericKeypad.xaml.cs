using System.Windows;
using System.Windows.Controls;

namespace Go212.POS.Desktop.Controls;

public partial class NumericKeypad : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(NumericKeypad),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public NumericKeypad()
    {
        InitializeComponent();
    }

    private void OnDigitClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string digit)
        {
            if (digit == "." && Value.Contains('.'))
                return;

            Value = (Value ?? string.Empty) + digit;
        }
    }

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        Value = string.Empty;
    }
}

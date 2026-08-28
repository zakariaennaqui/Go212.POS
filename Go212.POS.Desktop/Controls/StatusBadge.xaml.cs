using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Go212.POS.Desktop.Controls;

public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge),
            new PropertyMetadata("Actif", (d, e) => ((StatusBadge)d).UpdateVisuals()));

    public static readonly DependencyProperty StatusTypeProperty =
        DependencyProperty.Register(nameof(StatusType), typeof(string), typeof(StatusBadge),
            new PropertyMetadata("Success", (d, e) => ((StatusBadge)d).UpdateVisuals()));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string StatusType
    {
        get => (string)GetValue(StatusTypeProperty);
        set => SetValue(StatusTypeProperty, value);
    }

    public StatusBadge()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (BadgeText == null || BadgeBorder == null) return;

        BadgeText.Text = Text;

        switch (StatusType?.ToLowerInvariant())
        {
            case "danger":
            case "error":
            case "rupture":
                BadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                BadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
                BadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
                break;
            case "warning":
            case "alerte":
            case "held":
                BadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                BadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCD34D"));
                BadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));
                break;
            case "info":
                BadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE"));
                BadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7DD3FC"));
                BadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0369A1"));
                break;
            default: // Success
                BadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                BadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
                BadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
                break;
        }
    }
}

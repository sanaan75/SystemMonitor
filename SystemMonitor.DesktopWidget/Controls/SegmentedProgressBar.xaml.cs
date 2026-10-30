using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SystemMonitor.DesktopWidget.Controls;

public partial class SegmentedProgressBar : System.Windows.Controls.UserControl
{
    private const int SegmentCount = 18;
    private static readonly System.Windows.Media.Brush FilledBrush = CreateBrush(System.Windows.Media.Color.FromRgb(0x67, 0xE8, 0xF2));
    private static readonly System.Windows.Media.Brush EmptyBrush = CreateBrush(System.Windows.Media.Color.FromRgb(0x13, 0x2D, 0x43));
    private readonly List<Border> _segments = [];

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(SegmentedProgressBar),
        new FrameworkPropertyMetadata(0d, OnValueChanged));

    public SegmentedProgressBar()
    {
        InitializeComponent();

        for (var index = 0; index < SegmentCount; index++)
        {
            var segment = new Border
            {
                Margin = new Thickness(1, 0, 1, 0),
                Background = EmptyBrush
            };
            _segments.Add(segment);
            SegmentHost.Children.Add(segment);
        }

        UpdateSegments();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static System.Windows.Media.Brush CreateBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((SegmentedProgressBar)dependencyObject).UpdateSegments();
    }

    private void UpdateSegments()
    {
        if (_segments.Count == 0)
        {
            return;
        }

        var value = double.IsFinite(Value) ? Math.Clamp(Value, 0d, 100d) : 0d;
        var filledCount = (int)Math.Ceiling(value / 100d * SegmentCount);
        for (var index = 0; index < _segments.Count; index++)
        {
            _segments[index].Background = index < filledCount ? FilledBrush : EmptyBrush;
        }
    }
}

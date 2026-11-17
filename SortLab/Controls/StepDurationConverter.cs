using System.Globalization;
using Avalonia.Data.Converters;

namespace SortLab.Controls;

public sealed class StepDurationConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [double delay, double percent]) return 0d;
        // PlaybackEngine uses whole milliseconds for the step interval.
        return (int)Math.Clamp(delay, 0, 2000) * Math.Clamp(percent, 0, 100) / 100;
    }
}

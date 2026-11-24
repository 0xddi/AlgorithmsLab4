using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SortLab.Models;

namespace SortLab.Controls;

/// <summary>
/// Кастомный визуализатор сортировки: рисует столбики с подсветкой состояний
/// (обычный / сравнивается / переставляется / отсортирован / опорный).
/// </summary>
public class SortVisualizer : Control
{
    static SortVisualizer()
    {
        AffectsRender<SortVisualizer>(BarsProperty, ShowLabelsProperty,
            NormalBrushProperty, CompareBrushProperty, SwapBrushProperty, SortedBrushProperty,
            PivotBrushProperty, LabelBrushProperty, ShellGapProperty, ActiveCallProperty);
    }

    public static readonly StyledProperty<IReadOnlyList<SortBar>?> BarsProperty =
        AvaloniaProperty.Register<SortVisualizer, IReadOnlyList<SortBar>?>(nameof(Bars));

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<SortVisualizer, bool>(nameof(ShowLabels), defaultValue: true);

    public static readonly StyledProperty<IBrush?> NormalBrushProperty =
        AvaloniaProperty.Register<SortVisualizer, IBrush?>(nameof(NormalBrush));

    public static readonly StyledProperty<IBrush?> CompareBrushProperty =
        AvaloniaProperty.Register<SortVisualizer, IBrush?>(nameof(CompareBrush));

    public static readonly StyledProperty<IBrush?> SwapBrushProperty =
        AvaloniaProperty.Register<SortVisualizer, IBrush?>(nameof(SwapBrush));

    public static readonly StyledProperty<IBrush?> SortedBrushProperty =
        AvaloniaProperty.Register<SortVisualizer, IBrush?>(nameof(SortedBrush));

    public static readonly StyledProperty<IBrush?> PivotBrushProperty =
        AvaloniaProperty.Register<SortVisualizer, IBrush?>(nameof(PivotBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SortVisualizer, IBrush?>(nameof(LabelBrush));
    public static readonly StyledProperty<int> ShellGapProperty =
        AvaloniaProperty.Register<SortVisualizer, int>(nameof(ShellGap));
    public int ShellGap { get => GetValue(ShellGapProperty); set => SetValue(ShellGapProperty, value); }

    public static readonly StyledProperty<RangeFrame?> ActiveCallProperty =
        AvaloniaProperty.Register<SortVisualizer, RangeFrame?>(nameof(ActiveCall));
    public RangeFrame? ActiveCall { get => GetValue(ActiveCallProperty); set => SetValue(ActiveCallProperty, value); }

    public IReadOnlyList<SortBar>? Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public IBrush? NormalBrush { get => GetValue(NormalBrushProperty); set => SetValue(NormalBrushProperty, value); }
    public IBrush? CompareBrush { get => GetValue(CompareBrushProperty); set => SetValue(CompareBrushProperty, value); }
    public IBrush? SwapBrush { get => GetValue(SwapBrushProperty); set => SetValue(SwapBrushProperty, value); }
    public IBrush? SortedBrush { get => GetValue(SortedBrushProperty); set => SetValue(SortedBrushProperty, value); }
    public IBrush? PivotBrush { get => GetValue(PivotBrushProperty); set => SetValue(PivotBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    public static readonly StyledProperty<double> MoveDurationMsProperty =
        AvaloniaProperty.Register<SortVisualizer, double>(nameof(MoveDurationMs), 250);
    public double MoveDurationMs { get => GetValue(MoveDurationMsProperty); set => SetValue(MoveDurationMsProperty, value); }

    private readonly Dictionary<int, FormattedText> _labels = new();
    private static readonly Typeface LabelTypeface = new("Inter, Segoe UI");
    private double _labelSize;
    private double[] _fromSlot = [];
    private double[] _fromValue = [];
    private long _started;
    private double _duration;
    private bool _frameRequested;
    private double _bracketFromLeft, _bracketFromRight;
    private int _bracketLeft = -1, _bracketRight = -1;
    private long _bracketStarted;

    private double Progress()
    {
        if (_duration <= 0) return 1;
        double t = Math.Min(1, Stopwatch.GetElapsedTime(_started).TotalMilliseconds / _duration);
        return t * t * (3 - 2 * t);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelBrushProperty) _labels.Clear();
        if (change.Property == MoveDurationMsProperty && MoveDurationMs <= 0)
        {
            _duration = 0;
            InvalidateVisual();
        }
        if (change.Property == BarsProperty)
            BeginMove(change.GetOldValue<IReadOnlyList<SortBar>?>(), Bars);
    }

    private void BeginMove(IReadOnlyList<SortBar>? previous, IReadOnlyList<SortBar>? next)
    {
        if (previous is null || next is null || previous.Count != next.Count || MoveDurationMs <= 0)
        {
            _duration = 0;
            return;
        }

        var sources = MovementSources.Match(previous, next, bar => bar.Value);
        if (sources is null) return;
        double t = Progress();
        var slots = new double[next.Count];
        var values = new double[next.Count];
        for (int i = 0; i < next.Count; i++)
        {
            int source = sources[i];
            slots[i] = t >= 1 ? source : _fromSlot[source] + (source - _fromSlot[source]) * t;
            values[i] = t >= 1 ? previous[source].Value
                : _fromValue[source] + (previous[source].Value - _fromValue[source]) * t;
        }
        _fromSlot = slots;
        _fromValue = values;
        _duration = MoveDurationMs;
        _started = Stopwatch.GetTimestamp();
    }

    private void RequestNextFrame()
    {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } window) return;
        _frameRequested = true;
        window.RequestAnimationFrame(_ =>
        {
            _frameRequested = false;
            InvalidateVisual();
        });
    }

    public override void Render(DrawingContext context)
    {
        var bars = Bars;
        var bounds = new Rect(Bounds.Size);
        if (bars is null || bars.Count == 0)
        {
            DrawMessage(context, bounds, "Задайте массив и запустите сортировку");
            return;
        }

        int n = bars.Count;
        const double topPad = 8;
        bool showPositions = ShowLabels && bounds.Width / n >= 32;
        double bottomPad = showPositions || ShellGap > 0 ? 48 : 4;
        double labelSpace = ShowLabels ? 18 : 0;
        double slotWidth = bounds.Width / n;
        double barW = slotWidth * (n <= 24 ? 0.60 : 0.85);
        double gap = slotWidth - barW;
        if (barW <= 0) return;

        double t = Progress();
        double max = 1;
        for (int i = 0; i < n; i++)
        {
            double value = t >= 1 ? bars[i].Value : _fromValue[i] + (bars[i].Value - _fromValue[i]) * t;
            max = Math.Max(max, value);
        }
        double plotHeight = Math.Max(1, bounds.Height - topPad - bottomPad - labelSpace);

        if (ActiveCall is { } call)
        {
            var range = new Rect(call.Start * slotWidth, 4, (call.End - call.Start + 1) * slotWidth,
                topPad + labelSpace + plotHeight - 4);
            var ink = LabelBrush ?? Brushes.Gray;
            using (context.PushOpacity(0.10))
                context.DrawRectangle(ink, null, range);
            context.DrawLine(new Pen(ink, 2), range.TopLeft, range.TopRight);
        }

        double fontSize = Math.Clamp(barW * 0.6, 7, 16);

        if (fontSize != _labelSize)
        {
            _labelSize = fontSize;
            _labels.Clear();
        }
        int firstCompared = -1, lastCompared = -1;
        for (int i = 0; i < n; i++)
        {
            var bar = bars[i];
            if (bar.Mark == StepMark.Compare || bar.IsCompared)
            {
                if (firstCompared < 0) firstCompared = i;
                lastCompared = i;
            }
            double slot = t >= 1 ? i : _fromSlot[i] + (i - _fromSlot[i]) * t;
            double value = t >= 1 ? bar.Value : _fromValue[i] + (bar.Value - _fromValue[i]) * t;
            double x = slot * slotWidth + gap / 2;
            double h = Math.Max(2, value / max * plotHeight);
            double y = topPad + labelSpace + plotHeight - h;
            var rect = new RoundedRect(new Rect(x, y, barW, h), Math.Min(7, barW / 2));
            context.DrawRectangle(BrushFor(bar.Mark), null, rect);

            if (ShowLabels && barW >= 9)
            {
                var ft = Label(bar.Value);
                if (showPositions)
                {
                    var position = Label(i);
                    double center = (i + 0.5) * slotWidth;
                    context.DrawText(position, new Point(center - position.Width / 2, bounds.Height - bottomPad + 2));
                }
                if (ft.Width > barW + gap) continue;
                double tx = x + (barW - ft.Width) / 2;
                context.DrawText(ft, new Point(tx, y - ft.Height));
            }
        }

        if (t < 1) RequestNextFrame();

        // Базовая линия под столбиками.
        var pen = new Pen(LabelBrush ?? Brushes.Gray, 1, null, PenLineCap.Round);
        context.DrawLine(pen, new Point(0, topPad + labelSpace + plotHeight), new Point(bounds.Width, topPad + labelSpace + plotHeight));

        if (ShellGap == 0 || lastCompared <= firstCompared)
            _bracketLeft = _bracketRight = -1;
        if ((showPositions || ShellGap > 0) && lastCompared > firstCompared)
        {
            double left = (firstCompared + 0.5) * slotWidth;
            double right = (lastCompared + 0.5) * slotWidth;
            if (ShellGap > 0 && MoveDurationMs > 0)
            {
                double fraction = Math.Min(1, Stopwatch.GetElapsedTime(_bracketStarted).TotalMilliseconds / MoveDurationMs);
                fraction = fraction * fraction * (3 - 2 * fraction);
                if (firstCompared != _bracketLeft || lastCompared != _bracketRight)
                {
                    _bracketFromLeft = _bracketLeft < 0 ? firstCompared : _bracketFromLeft + (_bracketLeft - _bracketFromLeft) * fraction;
                    _bracketFromRight = _bracketRight < 0 ? firstCompared : _bracketFromRight + (_bracketRight - _bracketFromRight) * fraction;
                    _bracketLeft = firstCompared;
                    _bracketRight = lastCompared;
                    _bracketStarted = Stopwatch.GetTimestamp();
                    fraction = 0;
                }
                left = (_bracketFromLeft + (_bracketLeft - _bracketFromLeft) * fraction + 0.5) * slotWidth;
                right = (_bracketFromRight + (_bracketRight - _bracketFromRight) * fraction + 0.5) * slotWidth;
                if (fraction < 1) RequestNextFrame();
            }
            double y = bounds.Height - 20;
            context.DrawLine(pen, new Point(left, y - 5), new Point(left, y));
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
            context.DrawLine(pen, new Point(right, y), new Point(right, y - 5));
            var caption = new FormattedText(ShellGap > 0 ? $"расстояние: {ShellGap}" : "сравнение", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, LabelTypeface, 12, LabelBrush ?? Brushes.Gray);
            double x = Math.Clamp((left + right - caption.Width) / 2, 0, Math.Max(0, bounds.Width - caption.Width));
            context.DrawText(caption, new Point(x, y + 2));
        }
    }

    private FormattedText Label(int number)
    {
        if (!_labels.TryGetValue(number, out var text))
        {
            text = new FormattedText(number.ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, _labelSize, LabelBrush ?? Brushes.Gray);
            _labels.Add(number, text);
        }
        return text;
    }

    private IBrush BrushFor(StepMark mark) => mark switch
    {
        StepMark.Compare or StepMark.HeapChild => CompareBrush ?? Brushes.Orange,
        StepMark.Swap => SwapBrush ?? Brushes.Red,
        StepMark.Sorted => SortedBrush ?? Brushes.Green,
        StepMark.Pivot or StepMark.HeapParent => PivotBrush ?? Brushes.Purple,
        StepMark.LessThanPivot => Brushes.Teal,
        StepMark.AtLeastPivot => Brushes.Peru,
        _ => NormalBrush ?? Brushes.SteelBlue,
    };

    private void DrawMessage(DrawingContext context, Rect bounds, string message)
    {
        var typeface = new Typeface("Inter, Segoe UI");
        var ft = new FormattedText(message, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, 14, LabelBrush ?? Brushes.Gray);
        context.DrawText(ft, new Point((bounds.Width - ft.Width) / 2, (bounds.Height - ft.Height) / 2));
    }
}

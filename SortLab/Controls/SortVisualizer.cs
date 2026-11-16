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
            PivotBrushProperty, LabelBrushProperty);
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

    public static readonly StyledProperty<double> DelayMsProperty =
        AvaloniaProperty.Register<SortVisualizer, double>(nameof(DelayMs), 150);
    public double DelayMs { get => GetValue(DelayMsProperty); set => SetValue(DelayMsProperty, value); }

    private readonly Dictionary<int, FormattedText> _labels = new();
    private static readonly Typeface LabelTypeface = new("Inter, Segoe UI");
    private double _labelSize;
    private double[] _fromSlot = [];
    private double[] _fromValue = [];
    private long _started;
    private double _duration;
    private bool _frameRequested;

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
        if (change.Property == DelayMsProperty && DelayMs <= 0)
        {
            _duration = 0;
            InvalidateVisual();
        }
        if (change.Property == BarsProperty)
            BeginMove(change.GetOldValue<IReadOnlyList<SortBar>?>(), Bars);
    }

    private void BeginMove(IReadOnlyList<SortBar>? previous, IReadOnlyList<SortBar>? next)
    {
        if (previous is null || next is null || previous.Count != next.Count || DelayMs <= 0)
        {
            _duration = 0;
            return;
        }

        int first = -1, second = -1, changes = 0;
        for (int i = 0; i < next.Count; i++)
        {
            if (previous[i].Value == next[i].Value) continue;
            if (changes == 0) first = i;
            if (changes == 1) second = i;
            changes++;
        }
        if (changes == 0) return; // Изменение подсветки не прерывает перемещение.
        bool swap = changes == 2 && previous[first].Value == next[second].Value
                                && previous[second].Value == next[first].Value;
        double t = Progress();
        if (_fromSlot.Length != next.Count)
        {
            _fromSlot = new double[next.Count];
            _fromValue = new double[next.Count];
            t = 1;
        }
        for (int i = 0; i < next.Count; i++)
        {
            _fromSlot[i] += (i - _fromSlot[i]) * t;
            _fromValue[i] += (previous[i].Value - _fromValue[i]) * t;
        }
        if (swap)
        {
            (_fromSlot[first], _fromSlot[second]) = (_fromSlot[second], _fromSlot[first]);
            (_fromValue[first], _fromValue[second]) = (_fromValue[second], _fromValue[first]);
        }
        _duration = Math.Min(170, DelayMs);
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
        double bottomPad = showPositions ? 22 : 4;
        double labelSpace = ShowLabels ? 18 : 0;
        double slotWidth = bounds.Width / n;
        double gap = Math.Min(28, slotWidth * 0.30);
        double barW = slotWidth - gap;
        if (barW <= 0) return;

        double t = Progress();
        double max = 1;
        for (int i = 0; i < n; i++)
        {
            double value = t >= 1 ? bars[i].Value : _fromValue[i] + (bars[i].Value - _fromValue[i]) * t;
            max = Math.Max(max, value);
        }
        double plotHeight = Math.Max(1, bounds.Height - topPad - bottomPad - labelSpace);

        double fontSize = Math.Clamp(barW * 0.6, 7, 16);

        if (fontSize != _labelSize)
        {
            _labelSize = fontSize;
            _labels.Clear();
        }
        for (int i = 0; i < n; i++)
        {
            var bar = bars[i];
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
                    var position = Label(i + 1);
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
        StepMark.Compare => CompareBrush ?? Brushes.Orange,
        StepMark.Swap => SwapBrush ?? Brushes.Red,
        StepMark.Sorted => SortedBrush ?? Brushes.Green,
        StepMark.Pivot => PivotBrush ?? Brushes.Purple,
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

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SortLab.Models;

namespace SortLab.Controls;

/// <summary>
/// Визуализация слов: рисует «чипы» со словами, перенося их по строкам. Перестановка двух слов анимируется — чипы плавно обмениваются местами.
/// Если слов нет, показывает сообщение (например, «слишком много слов»).
/// </summary>
public class WordSortVisualizer : Control
{
    private const double GapX = 6, GapY = 6, PadX = 9, PadY = 3;

    static WordSortVisualizer()
    {
        AffectsRender<WordSortVisualizer>(WordsProperty, MessageProperty, ActiveRangeProperty,
            ChipBrushProperty, TextBrushProperty,
            CompareBrushProperty, SwapBrushProperty, SortedBrushProperty, PivotBrushProperty);
    }

    public static readonly StyledProperty<IReadOnlyList<WordChipVm>?> WordsProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IReadOnlyList<WordChipVm>?>(nameof(Words));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<WordSortVisualizer, string?>(nameof(Message));

    public static readonly StyledProperty<RangeCard?> ActiveRangeProperty =
        AvaloniaProperty.Register<WordSortVisualizer, RangeCard?>(nameof(ActiveRange));
    public RangeCard? ActiveRange { get => GetValue(ActiveRangeProperty); set => SetValue(ActiveRangeProperty, value); }

    public static readonly StyledProperty<IBrush?> ChipBrushProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IBrush?>(nameof(ChipBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> CompareBrushProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IBrush?>(nameof(CompareBrush));

    public static readonly StyledProperty<IBrush?> SwapBrushProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IBrush?>(nameof(SwapBrush));

    public static readonly StyledProperty<IBrush?> SortedBrushProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IBrush?>(nameof(SortedBrush));

    public static readonly StyledProperty<IBrush?> PivotBrushProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IBrush?>(nameof(PivotBrush));

    public IReadOnlyList<WordChipVm>? Words
    {
        get => GetValue(WordsProperty);
        set => SetValue(WordsProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public IBrush? ChipBrush { get => GetValue(ChipBrushProperty); set => SetValue(ChipBrushProperty, value); }
    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush? CompareBrush { get => GetValue(CompareBrushProperty); set => SetValue(CompareBrushProperty, value); }
    public IBrush? SwapBrush { get => GetValue(SwapBrushProperty); set => SetValue(SwapBrushProperty, value); }
    public IBrush? SortedBrush { get => GetValue(SortedBrushProperty); set => SetValue(SortedBrushProperty, value); }
    public IBrush? PivotBrush { get => GetValue(PivotBrushProperty); set => SetValue(PivotBrushProperty, value); }

    public static readonly StyledProperty<double> MoveDurationMsProperty =
        AvaloniaProperty.Register<WordSortVisualizer, double>(nameof(MoveDurationMs), 250);
    public double MoveDurationMs { get => GetValue(MoveDurationMsProperty); set => SetValue(MoveDurationMsProperty, value); }

    private Point[] _origins = [];
    private double _duration;
    private readonly Dictionary<(string Text, IBrush Foreground), FormattedText> _labels = new();
    private static readonly Typeface LabelTypeface = new("Inter, Segoe UI");
    private double _animT = 1;
    private long _animStart;
    private DispatcherTimer? _timer;

    // Кэш раскладки.
    private double _layoutW = -1, _contentHeight;
    private int _layoutCount = -1;
    private const double LabelFontSize = 16;
    private double _chipH = 20;
    private readonly List<double> _widths = new();
    private readonly List<double> _xs = new();
    private readonly List<double> _ys = new();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WordsProperty) OnWordsChanged(change.GetOldValue<IReadOnlyList<WordChipVm>?>());
        else if (change.Property == TextBrushProperty) _labels.Clear();
        else if (change.Property == MoveDurationMsProperty && MoveDurationMs <= 0) { StopAnim(); InvalidateVisual(); }
        else if (change.Property == MessageProperty) InvalidateVisual();
    }

    private void InvalidateLayout()
    {
        _layoutW = -1;
        _layoutCount = -1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnWordsChanged(IReadOnlyList<WordChipVm>? previous)
    {
        var next = Words;
        if (previous is not null && next is not null && previous.Count == next.Count)
        {
            var sources = MovementSources.Match(previous, next, chip => chip.Text);
            if (sources is null) return;
            if (MoveDurationMs > 0 && _layoutCount == next.Count)
            {
                _origins = sources.Select(CurrentPosition).ToArray();
                StartAnim();
                InvalidateLayout();
                return;
            }
        }
        _labels.Clear();
        StopAnim();
        InvalidateLayout();
    }

    private Point CurrentPosition(int index)
    {
        var target = new Point(_xs[index], _ys[index]);
        if (_animT >= 1) return target;
        var from = _origins[index];
        double t = Ease(_animT);
        return new Point(Lerp(from.X, target.X, t), Lerp(from.Y, target.Y, t));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAnim();
        base.OnDetachedFromVisualTree(e);
    }

    private void StopAnim()
    {
        _timer?.Stop();
        _animT = 1;
    }

    private void StartAnim()
    {
        _duration = MoveDurationMs;
        _animT = 0;
        _animStart = Stopwatch.GetTimestamp();
        if (_timer is null)
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTimerTick);
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e) => Tick();

    private void Tick()
    {
        _animT = Math.Clamp(Stopwatch.GetElapsedTime(_animStart).TotalMilliseconds / _duration, 0, 1);
        InvalidateVisual();
        if (_animT >= 1) _timer?.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var words = Words;
        if (words is null || words.Count == 0)
        {
            var msg = Message;
            DrawMessage(context, bounds, string.IsNullOrWhiteSpace(msg)
                ? "Слова ещё не разобраны."
                : msg);
            return;
        }

        EnsureLayout(bounds.Width);
        var activeRange = ActiveRange;
        var rangePen = activeRange is null ? null : new Pen(TextBrush ?? Brushes.Gray, 1.5);
        for (int i = 0; i < words.Count; i++)
        {
            var (x, y) = CurrentPosition(i);
            var chip = words[i];
            var rect = new RoundedRect(new Rect(x, y, _widths[i], _chipH), Math.Min(5, _chipH / 2));
            var background = BrushFor(chip.Mark);
            context.DrawRectangle(background, null, rect);
            if (activeRange is not null && i + 1 >= activeRange.Start && i + 1 <= activeRange.End)
                context.DrawRectangle(null, rangePen, rect);

            var fg = background is ISolidColorBrush solid
                ? ContrastingText(solid.Color)
                : TextBrush ?? Brushes.Gray;
            var ft = Label(chip.Text, fg);
            context.DrawText(ft, new Point(x + PadX, y + (_chipH - ft.Height) / 2));
        }
    }

    private static IBrush ContrastingText(Color color)
    {
        static double Linear(byte component)
        {
            double s = component / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        double luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        return luminance > 0.179 ? Brushes.Black : Brushes.White;
    }

    private FormattedText Label(string text, IBrush foreground)
    {
        var key = (text, foreground);
        if (!_labels.TryGetValue(key, out var label))
        {
            label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                LabelTypeface, LabelFontSize, foreground);
            _labels.Add(key, label);
        }
        return label;
    }

    private IBrush BrushFor(StepMark mark) => mark switch
    {
        StepMark.Compare => CompareBrush ?? Brushes.Orange,
        StepMark.Swap => SwapBrush ?? Brushes.Green,
        StepMark.Pivot => PivotBrush ?? Brushes.Purple,
        StepMark.Sorted => SortedBrush ?? Brushes.SteelBlue,
        _ => ChipBrush ?? Brushes.SteelBlue,
    };

    private static void DrawMessage(DrawingContext context, Rect bounds, string message)
    {
        var typeface = new Typeface("Inter, Segoe UI");
        var ft = new FormattedText(message, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, 13, Brushes.Gray);
        context.DrawText(ft, new Point(Math.Max(4, (bounds.Width - ft.Width) / 2), Math.Max(4, (bounds.Height - ft.Height) / 2)));
    }

    /// <summary>Для диагностики: куда писать лог раскладки (headless-тесты).</summary>
    public static System.Action<string>? DebugSink;

    private void LogLayout(string msg) => DebugSink?.Invoke(msg);

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayout(availableSize.Width);
        return new Size(0, _contentHeight);
    }

    private void EnsureLayout(double width)
    {
        var words = Words;
        if (words is null || words.Count == 0)
        {
            _contentHeight = 0;
            return;
        }
        if (_layoutW == width && _layoutCount == words.Count) return;
        _layoutW = width;
        _layoutCount = words.Count;
        _widths.Clear();
        double chipH = 0;
        foreach (var w in words)
        {
            var ft = Label(w.Text, TextBrush ?? Brushes.Gray);
            _widths.Add(ft.Width + PadX * 2);
            chipH = Math.Max(chipH, ft.Height + PadY * 2);
        }
        _chipH = chipH;

        _xs.Clear();
        _ys.Clear();
        double x = 0, y = 0;
        for (int i = 0; i < words.Count; i++)
        {
            double w = _widths[i];
            if (x > 0 && x + w > width)
            {
                x = 0;
                y += _chipH + GapY;
            }
            _xs.Add(x);
            _ys.Add(y);
            x += w + GapX;
        }

        _contentHeight = y + _chipH;
        LogLayout($"EnsureLayout w={width:F0} h={_contentHeight:F0} n={words.Count}");
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Плавное замедление в конце (smoothstep).</summary>
    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }
}

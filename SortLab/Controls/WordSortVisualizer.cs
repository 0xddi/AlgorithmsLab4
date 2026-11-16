using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SortLab.Models;

namespace SortLab.Controls;

/// <summary>
/// Визуализация слов: рисует «чипы» со словами, перенося их по строкам и подбирая размер шрифта,
/// чтобы всё поместилось. Перестановка двух слов анимируется — чипы плавно обмениваются местами.
/// Если слов нет, показывает сообщение (например, «слишком много слов»).
/// </summary>
public class WordSortVisualizer : Control
{
    private const int AnimMs = 180;   // длительность анимации перестановки
    private const double GapX = 6, GapY = 6, PadX = 9, PadY = 3;
    /// <summary>Резерв по высоте под подпись «… и ещё N слов» внизу.</summary>
    private const double FooterReserve = 20;
    private static readonly IBrush MarkedTextBrush = new SolidColorBrush(Color.Parse("#212121"));

    static WordSortVisualizer()
    {
        AffectsRender<WordSortVisualizer>(WordsProperty, MessageProperty,
            ChipBrushProperty, TextBrushProperty,
            CompareBrushProperty, SwapBrushProperty, SortedBrushProperty, PivotBrushProperty);
    }

    public static readonly StyledProperty<IReadOnlyList<WordChipVm>?> WordsProperty =
        AvaloniaProperty.Register<WordSortVisualizer, IReadOnlyList<WordChipVm>?>(nameof(Words));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<WordSortVisualizer, string?>(nameof(Message));

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

    // Состояние анимации: два слота, чипы которых едут навстречу друг другу.
    public static readonly StyledProperty<double> DelayMsProperty =
        AvaloniaProperty.Register<WordSortVisualizer, double>(nameof(DelayMs), 150);
    public double DelayMs { get => GetValue(DelayMsProperty); set => SetValue(DelayMsProperty, value); }

    private Point _fromA, _fromB;
    private double _duration;
    private readonly Dictionary<(string Text, IBrush Foreground), FormattedText> _labels = new();
    private static readonly Typeface LabelTypeface = new("Inter, Segoe UI");
    private int _animA = -1, _animB = -1;
    private double _animT = 1;
    private long _animStart;
    private DispatcherTimer? _timer;

    // Кэш раскладки.
    private double _layoutW = -1, _layoutH = -1;
    private int _layoutCount = -1;
    private double _fontSize = 12;
    private double _chipH = 20;
    private readonly List<double> _widths = new();
    private readonly List<double> _xs = new();
    private readonly List<double> _ys = new();
    private int _visibleCount;
    private int _hiddenCount;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WordsProperty) OnWordsChanged(change.GetOldValue<IReadOnlyList<WordChipVm>?>());
        else if (change.Property == TextBrushProperty) _labels.Clear();
        else if (change.Property == DelayMsProperty && DelayMs <= 0) { StopAnim(); InvalidateVisual(); }
        else if (change.Property == BoundsProperty) InvalidateLayout();
        else if (change.Property == MessageProperty) InvalidateVisual();
    }

    private void InvalidateLayout()
    {
        _layoutW = _layoutH = -1;
        _layoutCount = -1;
        InvalidateVisual();
    }

    private void OnWordsChanged(IReadOnlyList<WordChipVm>? previous)
    {
        var next = Words;
        if (previous is not null && next is not null && previous.Count == next.Count)
        {
            int first = -1, second = -1, count = 0;
            for (int i = 0; i < next.Count; i++)
            {
                if (next[i].Text == previous[i].Text) continue;
                if (++count == 1) first = i;
                else if (count == 2) second = i;
                else break;
            }
            if (count == 0) return;
            if (count == 2 && DelayMs > 0 && second < _visibleCount && _layoutCount == next.Count
                && previous[first].Text == next[second].Text && previous[second].Text == next[first].Text)
            {
                var fromA = CurrentPosition(second);
                var fromB = CurrentPosition(first);
                _fromA = fromA;
                _fromB = fromB;
                StartAnim(first, second);
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
        if (_animT >= 1 || (index != _animA && index != _animB)) return target;
        var from = index == _animA ? _fromA : _fromB;
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
        _animA = _animB = -1;
    }

    private void StartAnim(int a, int b)
    {
        _animA = a;
        _animB = b;
        _duration = Math.Min(AnimMs, DelayMs);
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

        EnsureLayout(bounds);
        for (int i = 0; i < _visibleCount; i++)
        {
            var (x, y) = CurrentPosition(i);
            var chip = words[i];
            var rect = new RoundedRect(new Rect(x, y, _widths[i], _chipH), Math.Min(5, _chipH / 2));
            context.DrawRectangle(BrushFor(chip.Mark), null, rect);

            var fg = chip.Mark switch
            {
                StepMark.Compare => MarkedTextBrush,
                StepMark.Swap or StepMark.Pivot => Brushes.White,
                _ => TextBrush ?? Brushes.Gray,
            };
            var ft = Label(chip.Text, fg);
            context.DrawText(ft, new Point(x + PadX, y + (_chipH - ft.Height) / 2));
        }

        if (_hiddenCount > 0)
        {
            var ft = new FormattedText($"… и ещё {_hiddenCount} слов — не хватает места",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 11,
                TextBrush ?? Brushes.Gray);
            context.DrawText(ft, new Point(4, bounds.Height - ft.Height - 2));
        }
    }

    private FormattedText Label(string text, IBrush foreground)
    {
        var key = (text, foreground);
        if (!_labels.TryGetValue(key, out var label))
        {
            label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                LabelTypeface, _fontSize, foreground);
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

    private void EnsureLayout(Rect bounds)
    {
        var words = Words;
        if (words is null || words.Count == 0) return;
        if (_layoutW == bounds.Width && _layoutH == bounds.Height && _layoutCount == words.Count) return;
        _layoutW = bounds.Width;
        _layoutH = bounds.Height;
        _layoutCount = words.Count;
        LogLayout($"EnsureLayout w={bounds.Width:F0} h={bounds.Height:F0} n={words.Count}");

        double availW = Math.Max(40, bounds.Width);

        // Размер шрифта подбираем по числу слов, раскладку делаем один раз:
        // перенос по строкам, а если по высоте не влезает — показываем сколько поместилось.
        _fontSize = words.Count <= 60 ? 12 : words.Count <= 150 ? 11 : words.Count <= 220 ? 10 : 9;
        TryLayout(words, availW, bounds.Height);
        // Если есть скрытые слова — резервируем снизу место под подпись «… и ещё N слов»
        // и пересчитываем раскладку, чтобы подпись не наезжала на чипы.
        if (_hiddenCount > 0)
            TryLayout(words, availW, bounds.Height - FooterReserve);
        LogLayout($"  font={_fontSize} visible={_visibleCount} hidden={_hiddenCount}");
    }

    /// <summary>Раскладка «чипов» по строкам; рисуем только те, что влезли по высоте.</summary>
    private void TryLayout(IReadOnlyList<WordChipVm> words,
        double availW, double availH)
    {
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
        double x = 0, y = 0, rowH = 0;
        int visible = 0;
        for (int i = 0; i < words.Count; i++)
        {
            double w = _widths[i];
            if (x > 0 && x + w > availW)
            {
                x = 0;
                y += rowH + GapY;
                rowH = 0;
            }
            if (y + _chipH > availH) break;
            _xs.Add(x);
            _ys.Add(y);
            x += w + GapX;
            rowH = Math.Max(rowH, _chipH);
            visible++;
        }

        _visibleCount = visible;
        _hiddenCount = words.Count - visible;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Плавное замедление в конце (smoothstep).</summary>
    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }
}

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
        const double topPad = 8, bottomPad = 4;
        double labelSpace = ShowLabels ? 18 : 0;
        double gap = n > 60 ? 1 : 2;
        double barW = (bounds.Width - (n - 1) * gap) / n;
        if (barW <= 0) return;

        int max = 1;
        foreach (var b in bars) max = Math.Max(max, b.Value);
        double plotHeight = Math.Max(1, bounds.Height - topPad - bottomPad - labelSpace);

        var typeface = new Typeface("Inter, Segoe UI");
        double fontSize = Math.Clamp(barW * 0.6, 7, 12);

        for (int i = 0; i < n; i++)
        {
            var bar = bars[i];
            double x = i * (barW + gap);
            double h = Math.Max(2, (double)bar.Value / max * plotHeight);
            double y = topPad + plotHeight - h;
            var rect = new RoundedRect(new Rect(x, y, barW, h), Math.Min(3, barW / 2));
            context.DrawRectangle(BrushFor(bar.Mark), null, rect);

            if (ShowLabels && barW >= 9)
            {
                var ft = new FormattedText(bar.Value.ToString(),
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, fontSize, LabelBrush ?? Brushes.Gray);
                double tx = x + (barW - ft.Width) / 2;
                context.DrawText(ft, new Point(tx, topPad + plotHeight + 2));
            }
        }

        // Базовая линия под столбиками.
        var pen = new Pen(LabelBrush ?? Brushes.Gray, 1, null, PenLineCap.Round);
        context.DrawLine(pen, new Point(0, topPad + plotHeight), new Point(bounds.Width, topPad + plotHeight));
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

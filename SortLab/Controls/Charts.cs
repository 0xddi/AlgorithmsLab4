using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SortLab.Controls;

/// <summary>Серия данных диаграммы: имя + значения по категориям.</summary>
public sealed record ChartSeriesData(string Name, IReadOnlyList<double> Values);

/// <summary>Простая столбчатая диаграмма с группировкой по категориям и легендой.</summary>
public class BarChart : Control
{
    static BarChart()
    {
        AffectsRender<BarChart>(CategoriesProperty, SeriesProperty,
            GridBrushProperty, LabelBrushProperty,
            BarBrush1Property, BarBrush2Property, BarBrush3Property, BarBrush4Property,
            BarBrush5Property, BarBrush6Property,
            XAxisTitleProperty, YAxisTitleProperty);
    }

    public static readonly StyledProperty<IReadOnlyList<string>?> CategoriesProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<string>?>(nameof(Categories));

    public static readonly StyledProperty<IReadOnlyList<ChartSeriesData>?> SeriesProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<ChartSeriesData>?>(nameof(Series));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> BarBrush1Property =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(BarBrush1));
    public static readonly StyledProperty<IBrush?> BarBrush2Property =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(BarBrush2));
    public static readonly StyledProperty<IBrush?> BarBrush3Property =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(BarBrush3));
    public static readonly StyledProperty<IBrush?> BarBrush4Property =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(BarBrush4));
    public static readonly StyledProperty<IBrush?> BarBrush5Property =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(BarBrush5));
    public static readonly StyledProperty<IBrush?> BarBrush6Property =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(BarBrush6));

    public static readonly StyledProperty<string?> XAxisTitleProperty =
        AvaloniaProperty.Register<BarChart, string?>(nameof(XAxisTitle));

    public static readonly StyledProperty<string?> YAxisTitleProperty =
        AvaloniaProperty.Register<BarChart, string?>(nameof(YAxisTitle));

    public IReadOnlyList<string>? Categories { get => GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }
    public IReadOnlyList<ChartSeriesData>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? BarBrush1 { get => GetValue(BarBrush1Property); set => SetValue(BarBrush1Property, value); }
    public IBrush? BarBrush2 { get => GetValue(BarBrush2Property); set => SetValue(BarBrush2Property, value); }
    public IBrush? BarBrush3 { get => GetValue(BarBrush3Property); set => SetValue(BarBrush3Property, value); }
    public IBrush? BarBrush4 { get => GetValue(BarBrush4Property); set => SetValue(BarBrush4Property, value); }
    public IBrush? BarBrush5 { get => GetValue(BarBrush5Property); set => SetValue(BarBrush5Property, value); }
    public IBrush? BarBrush6 { get => GetValue(BarBrush6Property); set => SetValue(BarBrush6Property, value); }
    public string? XAxisTitle { get => GetValue(XAxisTitleProperty); set => SetValue(XAxisTitleProperty, value); }
    public string? YAxisTitle { get => GetValue(YAxisTitleProperty); set => SetValue(YAxisTitleProperty, value); }

    public override void Render(DrawingContext context)
    {
        var categories = Categories;
        var series = Series;
        var bounds = new Rect(Bounds.Size);
        var typeface = new Typeface("Inter, Segoe UI");
        var labelBrush = LabelBrush ?? Brushes.Gray;

        if (categories is null || series is null || series.Count == 0 || categories.Count == 0)
        {
            var empty = new FormattedText("Нет данных. Запустите эксперименты",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 13, labelBrush);
            context.DrawText(empty, new Point((bounds.Width - empty.Width) / 2, (bounds.Height - empty.Height) / 2));
            return;
        }

        var yTitle = YAxisTitle;
        var xTitle = XAxisTitle;
        double left = 52, right = 12, bottom = 26;
        double top = BarChart.LegendRowCount(series, typeface, bounds.Width) > 1 ? 48 : 30;
        if (!string.IsNullOrWhiteSpace(yTitle)) left += 22;   // место под вертикальную подпись оси
        if (!string.IsNullOrWhiteSpace(xTitle)) bottom += 16; // место под подпись оси категорий
        double plotW = Math.Max(10, bounds.Width - left - right);
        double plotH = Math.Max(10, bounds.Height - top - bottom);

        double max = 0;
        foreach (var s in series)
            foreach (var v in s.Values)
                max = Math.Max(max, v);
        if (max <= 0) max = 1;
        max = NiceMax(max);

        // Сетка и подписи значений по вертикали.
        var gridPen = new Pen(GridBrush ?? Brushes.LightGray, 1, new DashStyle(new double[] { 4, 3 }, 0), PenLineCap.Round);
        for (int g = 0; g <= 4; g++)
        {
            double y = top + plotH * g / 4;
            context.DrawLine(gridPen, new Point(left, y), new Point(left + plotW, y));
            var val = max * (4 - g) / 4;
            var ft = new FormattedText(FormatVal(val), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 10, labelBrush);
            context.DrawText(ft, new Point(left - ft.Width - 4, y - ft.Height / 2));
        }

        // Столбики.
        int cats = categories.Count;
        int seriesCount = Math.Min(6, series.Count);
        double groupW = plotW / cats;
        double barW = Math.Max(2, groupW * 0.7 / seriesCount);

        for (int i = 0; i < cats; i++)
        {
            double gx = left + i * groupW;
            double groupInnerW = barW * seriesCount + (seriesCount - 1) * 2;
            double startX = gx + (groupW - groupInnerW) / 2;
            for (int s = 0; s < seriesCount; s++)
            {
                var values = series[s].Values;
                if (i >= values.Count) continue;
                double v = values[i];
                double h = Math.Max(v <= 0 ? 0 : 2, v / max * plotH);
                double x = startX + s * (barW + 2);
                var rect = new RoundedRect(new Rect(x, top + plotH - h, barW, h), Math.Min(3, barW / 2));
                context.DrawRectangle(BarBrush(s), null, rect);

                if (barW >= 18)
                {
                    var ft = new FormattedText(FormatVal(v), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        typeface, 10, labelBrush);
                    context.DrawText(ft, new Point(x + (barW - ft.Width) / 2, top + plotH - h - ft.Height - 2));
                }
            }

            // Подпись категории.
            var catFt = new FormattedText(categories[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            context.DrawText(catFt, new Point(gx + (groupW - catFt.Width) / 2, top + plotH + 6));
        }

        DrawLegend(context, series, typeface, labelBrush, BarBrush, bounds.Width);

        // Подписи осей с единицами измерения.
        if (!string.IsNullOrWhiteSpace(yTitle))
        {
            var ft = new FormattedText(yTitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            // Поворачиваем текст на -90°: сначала вращение, затем перенос (вложенные трансформации).
            var cy = top + plotH / 2;
            using (context.PushTransform(Matrix.CreateTranslation(new Vector(12, cy + ft.Width / 2))))
            using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2)))
                context.DrawText(ft, new Point(0, 0));
        }
        if (!string.IsNullOrWhiteSpace(xTitle))
        {
            var ft = new FormattedText(xTitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            context.DrawText(ft, new Point(left + (plotW - ft.Width) / 2, top + plotH + 22));
        }
    }

    private IBrush BarBrush(int index) => index switch
    {
        1 => BarBrush2 ?? Brushes.Orange,
        2 => BarBrush3 ?? Brushes.Green,
        3 => BarBrush4 ?? Brushes.Purple,
        4 => BarBrush5 ?? Brushes.Teal,
        5 => BarBrush6 ?? Brushes.MediumVioletRed,
        _ => BarBrush1 ?? Brushes.SteelBlue,
    };

    internal static void DrawLegend(DrawingContext context, IReadOnlyList<ChartSeriesData> series,
        Typeface typeface, IBrush labelBrush, Func<int, IBrush> brushFor, double maxWidth)
    {
        double x = 8;
        double y = 6;
        for (int s = 0; s < Math.Min(6, series.Count); s++)
        {
            var swatchBrush = brushFor(s);
            var ft = new FormattedText(series[s].Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            double itemWidth = 14 + ft.Width + 16;
            if (x > 8 && x + itemWidth > maxWidth)
            {
                x = 8;
                y += 18;
            }
            context.FillRectangle(swatchBrush, new Rect(x, y + 3, 10, 10));
            context.DrawText(ft, new Point(x + 14, y));
            x += itemWidth;
        }
    }

    /// <summary>Сколько строк займёт легенда при переносе по ширине диаграммы.</summary>
    internal static int LegendRowCount(IReadOnlyList<ChartSeriesData> series, Typeface typeface, double maxWidth)
    {
        double x = 8;
        int rows = 1;
        for (int s = 0; s < Math.Min(6, series.Count); s++)
        {
            var ft = new FormattedText(series[s].Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, Brushes.Black);
            double itemWidth = 14 + ft.Width + 16;
            if (x > 8 && x + itemWidth > maxWidth)
            {
                x = 8;
                rows++;
            }
            x += itemWidth;
        }
        return rows;
    }

    internal static string FormatVal(double v) =>
        v >= 100 ? v.ToString("F0", CultureInfo.InvariantCulture)
        : v >= 10 ? v.ToString("F1", CultureInfo.InvariantCulture)
        : v.ToString("F2", CultureInfo.InvariantCulture);

    internal static double NiceMax(double v)
    {
        double pow = Math.Pow(10, Math.Floor(Math.Log10(v)));
        double norm = v / pow;
        double nice = norm switch { <= 1 => 1, <= 2 => 2, <= 5 => 5, _ => 10 };
        return nice * pow;
    }
}

/// <summary>Линейный график: значения серий по категориям.</summary>
public class LineChart : Control
{
    static LineChart()
    {
        AffectsRender<LineChart>(CategoriesProperty, SeriesProperty,
            GridBrushProperty, LabelBrushProperty,
            LineBrush1Property, LineBrush2Property, LineBrush3Property, LineBrush4Property,
            LineBrush5Property, LineBrush6Property,
            XAxisTitleProperty, YAxisTitleProperty);
    }

    public static readonly StyledProperty<IReadOnlyList<string>?> CategoriesProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<string>?>(nameof(Categories));

    public static readonly StyledProperty<IReadOnlyList<ChartSeriesData>?> SeriesProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<ChartSeriesData>?>(nameof(Series));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> LineBrush1Property =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush1));
    public static readonly StyledProperty<IBrush?> LineBrush2Property =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush2));
    public static readonly StyledProperty<IBrush?> LineBrush3Property =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush3));
    public static readonly StyledProperty<IBrush?> LineBrush4Property =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush4));
    public static readonly StyledProperty<IBrush?> LineBrush5Property =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush5));
    public static readonly StyledProperty<IBrush?> LineBrush6Property =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LineBrush6));

    public static readonly StyledProperty<string?> XAxisTitleProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(XAxisTitle));

    public static readonly StyledProperty<string?> YAxisTitleProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(YAxisTitle));

    public IReadOnlyList<string>? Categories { get => GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }
    public IReadOnlyList<ChartSeriesData>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? LineBrush1 { get => GetValue(LineBrush1Property); set => SetValue(LineBrush1Property, value); }
    public IBrush? LineBrush2 { get => GetValue(LineBrush2Property); set => SetValue(LineBrush2Property, value); }
    public IBrush? LineBrush3 { get => GetValue(LineBrush3Property); set => SetValue(LineBrush3Property, value); }
    public IBrush? LineBrush4 { get => GetValue(LineBrush4Property); set => SetValue(LineBrush4Property, value); }
    public IBrush? LineBrush5 { get => GetValue(LineBrush5Property); set => SetValue(LineBrush5Property, value); }
    public IBrush? LineBrush6 { get => GetValue(LineBrush6Property); set => SetValue(LineBrush6Property, value); }
    public string? XAxisTitle { get => GetValue(XAxisTitleProperty); set => SetValue(XAxisTitleProperty, value); }
    public string? YAxisTitle { get => GetValue(YAxisTitleProperty); set => SetValue(YAxisTitleProperty, value); }

    public override void Render(DrawingContext context)
    {
        var categories = Categories;
        var series = Series;
        var bounds = new Rect(Bounds.Size);
        var typeface = new Typeface("Inter, Segoe UI");
        var labelBrush = LabelBrush ?? Brushes.Gray;

        if (categories is null || series is null || series.Count == 0 || categories.Count == 0)
        {
            var empty = new FormattedText("Нет данных. Запустите эксперименты",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 13, labelBrush);
            context.DrawText(empty, new Point((bounds.Width - empty.Width) / 2, (bounds.Height - empty.Height) / 2));
            return;
        }

        var yTitle = YAxisTitle;
        var xTitle = XAxisTitle;
        double left = 52, right = 12, bottom = 26;
        double top = BarChart.LegendRowCount(series, typeface, bounds.Width) > 1 ? 48 : 30;
        if (!string.IsNullOrWhiteSpace(yTitle)) left += 22;   // место под вертикальную подпись оси
        if (!string.IsNullOrWhiteSpace(xTitle)) bottom += 16; // место под подпись оси категорий
        double plotW = Math.Max(10, bounds.Width - left - right);
        double plotH = Math.Max(10, bounds.Height - top - bottom);

        double max = 0;
        foreach (var s in series)
            foreach (var v in s.Values)
                max = Math.Max(max, v);
        if (max <= 0) max = 1;
        max = BarChart.NiceMax(max);

        var gridPen = new Pen(GridBrush ?? Brushes.LightGray, 1, new DashStyle(new double[] { 4, 3 }, 0), PenLineCap.Round);
        for (int g = 0; g <= 4; g++)
        {
            double y = top + plotH * g / 4;
            context.DrawLine(gridPen, new Point(left, y), new Point(left + plotW, y));
            var val = max * (4 - g) / 4;
            var ft = new FormattedText(BarChart.FormatVal(val), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 10, labelBrush);
            context.DrawText(ft, new Point(left - ft.Width - 4, y - ft.Height / 2));
        }

        int cats = categories.Count;
        var lengths = new int[cats];
        for (int i = 0; i < cats; i++)
        {
            if (int.TryParse(categories[i], out lengths[i])) continue;
            lengths = Enumerable.Range(0, cats).ToArray();
            break;
        }
        int minimum = lengths.Min();
        double range = (double)lengths.Max() - minimum;
        var xs = new double[cats];
        double lastLabelRight = double.NegativeInfinity;
        for (int i = 0; i < cats; i++)
        {
            xs[i] = range == 0 ? left + plotW / 2 : left + plotW * ((double)lengths[i] - minimum) / range;
            var catFt = new FormattedText(categories[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            double labelLeft = Math.Clamp(xs[i] - catFt.Width / 2, 0, Math.Max(0, bounds.Width - catFt.Width));
            if (labelLeft >= lastLabelRight + 4)
            {
                context.DrawText(catFt, new Point(labelLeft, top + plotH + 6));
                lastLabelRight = labelLeft + catFt.Width;
            }
        }

        var pen = new Pen(GridBrush ?? Brushes.LightGray, 1);
        context.DrawLine(pen, new Point(left, top + plotH), new Point(left + plotW, top + plotH));

        int seriesCount = Math.Min(6, series.Count);
        for (int s = 0; s < seriesCount; s++)
        {
            var brush = LineBrush(s);
            var values = series[s].Values;
            var points = new List<Point>();
            for (int i = 0; i < cats && i < values.Count; i++)
            {
                double h = values[i] / max * plotH;
                points.Add(new Point(xs[i], top + plotH - h));
            }
            if (points.Count > 1)
            {
                var linePen = new Pen(brush, 2.4, null, PenLineCap.Round, PenLineJoin.Round);
                for (int i = 1; i < points.Count; i++)
                    context.DrawLine(linePen, points[i - 1], points[i]);
            }
            foreach (var p in points)
                context.DrawEllipse(brush, null, p, 4, 4);
        }

        BarChart.DrawLegend(context, series, typeface, labelBrush, LineBrush, bounds.Width);

        // Подписи осей с единицами измерения.
        if (!string.IsNullOrWhiteSpace(yTitle))
        {
            var ft = new FormattedText(yTitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            var cy = top + plotH / 2;
            using (context.PushTransform(Matrix.CreateTranslation(new Vector(12, cy + ft.Width / 2))))
            using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2)))
                context.DrawText(ft, new Point(0, 0));
        }
        if (!string.IsNullOrWhiteSpace(xTitle))
        {
            var ft = new FormattedText(xTitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, 11, labelBrush);
            context.DrawText(ft, new Point(left + (plotW - ft.Width) / 2, top + plotH + 22));
        }
    }

    private IBrush LineBrush(int index) => index switch
    {
        1 => LineBrush2 ?? Brushes.Orange,
        2 => LineBrush3 ?? Brushes.Green,
        3 => LineBrush4 ?? Brushes.Purple,
        4 => LineBrush5 ?? Brushes.Teal,
        5 => LineBrush6 ?? Brushes.MediumVioletRed,
        _ => LineBrush1 ?? Brushes.SteelBlue,
    };
}

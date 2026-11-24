using System.Globalization;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SortLab.Models;

namespace SortLab.Controls;

public sealed class HeapTree : Control
{
    public static readonly StyledProperty<IReadOnlyList<SortBar>?> NodesProperty =
        AvaloniaProperty.Register<HeapTree, IReadOnlyList<SortBar>?>(nameof(Nodes));
    public static readonly StyledProperty<IBrush> InkProperty =
        AvaloniaProperty.Register<HeapTree, IBrush>(nameof(Ink), Brushes.Gray);
    public static readonly StyledProperty<IBrush> ParentBrushProperty =
        AvaloniaProperty.Register<HeapTree, IBrush>(nameof(ParentBrush), Brushes.Purple);
    public static readonly StyledProperty<IBrush> ChildBrushProperty =
        AvaloniaProperty.Register<HeapTree, IBrush>(nameof(ChildBrush), Brushes.Orange);
    public static readonly StyledProperty<IBrush> SwapBrushProperty =
        AvaloniaProperty.Register<HeapTree, IBrush>(nameof(SwapBrush), Brushes.Red);

    public IReadOnlyList<SortBar>? Nodes { get => GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IBrush Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public IBrush ParentBrush { get => GetValue(ParentBrushProperty); set => SetValue(ParentBrushProperty, value); }
    public IBrush ChildBrush { get => GetValue(ChildBrushProperty); set => SetValue(ChildBrushProperty, value); }
    public IBrush SwapBrush { get => GetValue(SwapBrushProperty); set => SetValue(SwapBrushProperty, value); }
    public static readonly StyledProperty<double> MoveDurationMsProperty =
        AvaloniaProperty.Register<HeapTree, double>(nameof(MoveDurationMs));
    public double MoveDurationMs { get => GetValue(MoveDurationMsProperty); set => SetValue(MoveDurationMsProperty, value); }
    private Point[] _origins = [];
    private long _started;
    private bool _pending;
    private SortBar? _extracted;
    private Point _extractionStart;

    private static Point Slot(int position)
    {
        int level = BitOperations.Log2((uint)position);
        int slots = 1 << level;
        return new Point((position - slots + 0.5) / slots, 19 + 38 * level);
    }

    private double Progress()
    {
        if (_origins.Length == 0 || MoveDurationMs <= 0) return 1;
        double t = Math.Min(1, Stopwatch.GetElapsedTime(_started).TotalMilliseconds / MoveDurationMs);
        return t * t * (3 - 2 * t);
    }

    private Point MovingSlot(int i, double t)
    {
        var target = Slot(i + 1);
        if (t >= 1 || i >= _origins.Length) return target;
        var from = _origins[i];
        // Opposite arcs keep both values visible as they pass.
        double arc = Math.Sign(target.Y - from.Y) * Math.Sin(t * Math.PI) * 18 / Math.Max(1, Bounds.Width);
        return new Point(from.X + (target.X - from.X) * t + arc, from.Y + (target.Y - from.Y) * t);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MoveDurationMsProperty && MoveDurationMs <= 0)
        {
            _origins = [];
            _extracted = null;
            InvalidateMeasure();
            InvalidateVisual();
        }
        if (change.Property != NodesProperty) return;
        var old = change.GetOldValue<IReadOnlyList<SortBar>?>();
        if (MoveDurationMs <= 0 || old is not { Count: > 0 } || Nodes is not { Count: > 0 } next ||
            Math.Abs(old.Count - next.Count) > 1)
        {
            _origins = [];
            _extracted = null;
            return;
        }
        int[]? sources;
        if (old.Count == next.Count)
            sources = MovementSources.Match(old, next, node => node.Value);
        else
        {
            sources = Enumerable.Range(0, next.Count).ToArray();
            if (next.Count < old.Count) sources[0] = old.Count - 1;
            else { sources[0] = next.Count - 1; sources[^1] = 0; }
        }
        if (sources is null) return;
        double t = Progress();
        _extracted = old.Count == next.Count + 1 ? old[0] : null;
        _extractionStart = MovingSlot(0, t);
        _origins = sources.Select(i => MovingSlot(i, t)).ToArray();
        _started = Stopwatch.GetTimestamp();
    }

    static HeapTree()
    {
        AffectsMeasure<HeapTree>(NodesProperty);
        AffectsRender<HeapTree>(NodesProperty, InkProperty, ParentBrushProperty, ChildBrushProperty, SwapBrushProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Nodes?.Count ?? 0;
        if (_extracted is not null) count++;
        int lastLevel = BitOperations.Log2((uint)Math.Max(1, count));
        return new Size((1 << lastLevel) * 54, (lastLevel + 1) * 38);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Nodes is not { Count: > 0 } nodes) return;

        Point Position(int position)
        {
            int level = BitOperations.Log2((uint)position);
            int slots = 1 << level;
            return new Point((position - slots + 0.5) * Bounds.Width / slots, 19 + 38 * level);
        }

        var edge = new Pen(Ink, 1);
        for (int i = 2; i <= nodes.Count; i++)
        {
            var parent = Position(i / 2);
            var child = Position(i);
            var link = nodes[i - 1].IsCompared || nodes[i - 1].Mark is StepMark.HeapChild or StepMark.Swap
                ? new Pen(nodes[i - 1].Mark == StepMark.Swap ? SwapBrush : ChildBrush, 2) : edge;
            context.DrawLine(link, new Point(parent.X, parent.Y + 15), new Point(child.X, child.Y - 15));
        }
        double t = Progress();
        void DrawNode(Point p, string label, IPen outline)
        {
            context.DrawRectangle(null, outline, new Rect(p.X - 25, p.Y - 15, 50, 30), 6, 6);
            var text = new FormattedText(label, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, Ink);
            context.DrawText(text, new Point(p.X - text.Width / 2, p.Y - text.Height / 2));
        }
        for (int i = 1; i <= nodes.Count; i++)
        {
            var node = nodes[i - 1];
            var slot = MovingSlot(i - 1, t);
            var p = new Point(slot.X * Bounds.Width, slot.Y);
            var outline = node.Mark switch
            {
                StepMark.HeapParent => new Pen(ParentBrush, 3),
                StepMark.HeapChild => new Pen(ChildBrush, 3),
                StepMark.Swap => new Pen(SwapBrush, 3),
                _ => edge
            };
            bool travelling = t < 1 && i <= _origins.Length && _origins[i - 1] != Slot(i);
            DrawNode(p, travelling ? node.Value.ToString() : $"{i - 1}: {node.Value}", outline);
        }
        if (_extracted is { } extracted && t < 1)
        {
            var end = Position(nodes.Count + 1);
            double x = _extractionStart.X * Bounds.Width;
            DrawNode(new Point(x + (end.X - x) * t + 18 * Math.Sin(t * Math.PI),
                _extractionStart.Y + (end.Y - _extractionStart.Y) * t), extracted.Value.ToString(), new Pen(SwapBrush, 3));
        }
        if ((t < 1 || _extracted is not null) && !_pending && TopLevel.GetTopLevel(this) is { } window)
        {
            _pending = true;
            window.RequestAnimationFrame(_ =>
            {
                _pending = false;
                if (_extracted is not null && Progress() >= 1)
                {
                    _extracted = null;
                    InvalidateMeasure();
                }
                InvalidateVisual();
            });
        }
    }
}

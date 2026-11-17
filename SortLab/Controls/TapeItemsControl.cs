using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using SortLab.Models;

namespace SortLab.Controls;

public class TapeItemsControl : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    public static readonly StyledProperty<double> MoveDurationMsProperty =
        AvaloniaProperty.Register<TapeItemsControl, double>(nameof(MoveDurationMs), 250);
    public double MoveDurationMs
    {
        get => GetValue(MoveDurationMsProperty);
        set => SetValue(MoveDurationMsProperty, value);
    }

    private readonly Dictionary<(string Tape, int Id), Point> _previous = new();
    private readonly Dictionary<int, Point> _copies = new();
    private readonly List<(TranslateTransform Transform, Vector Offset)> _moves = new();
    private long _started;
    private double _duration;
    private bool _framePending;

    private IEnumerable<(HighlightBorder Chip, string Tape, ChipVm Row)> Rows()
    {
        foreach (var chip in this.GetVisualDescendants().OfType<HighlightBorder>())
            if (chip.DataContext is ChipVm { RecordId: > 0 } row &&
                chip.GetVisualAncestors().OfType<Control>().Select(c => c.DataContext)
                    .OfType<TapeState>().FirstOrDefault() is { } tape)
                yield return (chip, tape.Name, row);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ItemsSourceProperty && MoveDurationMs > 0)
        {
            // Capture the rendered coordinates before the old containers are recycled.
            LayoutUpdated -= AnimateLayout;
            _previous.Clear();
            _copies.Clear();
            foreach (var (chip, tape, row) in Rows())
                if (chip.TranslatePoint(default, this) is { } point)
                {
                    _previous[(tape, row.RecordId)] = point;
                    _copies[row.RecordId] = point;
                }
            FinishMoves();
            _started = Stopwatch.GetTimestamp();
            _duration = MoveDurationMs;
            LayoutUpdated += AnimateLayout;
        }
        base.OnPropertyChanged(change);
        if (change.Property == MoveDurationMsProperty && MoveDurationMs <= 0)
        {
            LayoutUpdated -= AnimateLayout;
            FinishMoves();
        }
    }

    private void AnimateLayout(object? sender, EventArgs args)
    {
        LayoutUpdated -= AnimateLayout;
        if (Stopwatch.GetElapsedTime(_started).TotalMilliseconds >= _duration) return;
        foreach (var (chip, tape, row) in Rows())
        {
            if (chip.TranslatePoint(default, this) is not { } target) continue;
            if (!_previous.TryGetValue((tape, row.RecordId), out var origin) &&
                !_copies.TryGetValue(row.RecordId, out origin))
            {
                if (!row.IsMoved) continue;
                origin = target - new Point(0, 12);
            }
            var offset = new Vector(origin.X - target.X, origin.Y - target.Y);
            if (offset.Length < 0.5) continue;
            var transform = new TranslateTransform(offset.X, offset.Y);
            chip.RenderTransform = transform;
            _moves.Add((transform, offset));
        }
        RequestFrame();
    }

    private void FinishMoves()
    {
        foreach (var (transform, _) in _moves)
            transform.X = transform.Y = 0;
        _moves.Clear();
    }

    private void RequestFrame()
    {
        if (_framePending || _moves.Count == 0 || TopLevel.GetTopLevel(this) is not { } window) return;
        _framePending = true;
        window.RequestAnimationFrame(_ =>
        {
            _framePending = false;
            if (_moves.Count == 0) return;
            double t = Stopwatch.GetElapsedTime(_started).TotalMilliseconds / _duration;
            if (t >= 1) { FinishMoves(); return; }
            double remaining = 1 - t * t * (3 - 2 * t);
            foreach (var (transform, offset) in _moves)
            {
                transform.X = offset.X * remaining;
                transform.Y = offset.Y * remaining;
            }
            RequestFrame();
        });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= AnimateLayout;
        FinishMoves();
        base.OnDetachedFromVisualTree(e);
    }
}

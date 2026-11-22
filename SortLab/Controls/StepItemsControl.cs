using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using SortLab.Models;

namespace SortLab.Controls;

// Uses the same layout-to-layout movement as the external tapes.
public sealed class StepItemsControl : TapeItemsControl
{
    public static readonly StyledProperty<string?> LinesProperty =
        AvaloniaProperty.Register<StepItemsControl, string?>(nameof(Lines));
    public string? Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinesProperty)
            ItemsSource = Lines?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    protected override IEnumerable<(Control Chip, string Lane, string Key, bool Enter)> Cells()
    {
        foreach (var chip in this.GetVisualDescendants().OfType<HighlightBorder>())
        {
            if (chip.DataContext is not WordCell cell) continue;
            if (cell.SourceIndex is int id)
                yield return (chip, "слово", id.ToString(), true);
            else if (cell.Active && chip.GetVisualAncestors().OfType<Control>()
                .Select(c => c.DataContext).OfType<WordLane>().FirstOrDefault() is { } lane)
                yield return (chip, "символ", lane.Label, true);
        }
        foreach (var line in this.GetVisualDescendants().OfType<TextBlock>())
            if (line.DataContext is string text && line.Text == text)
            {
                int range = text.IndexOf("позиции ", StringComparison.Ordinal);
                yield return (line, "стек", range < 0 ? text : text[range..], true);
            }
        foreach (var card in this.GetVisualDescendants().OfType<Border>())
            if (card.Classes.Contains("rangeCard") && card.DataContext is RangeCard rangeCard)
                yield return (card, "участок", $"{rangeCard.Start}:{rangeCard.End}", true);
    }
}

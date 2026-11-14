using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SortLab.Controls;

/// <summary>
/// Блок (чип) визуализации, подсвечивающийся классами hl-compare / hl-move.
/// Цвета задаются стилями через DynamicResource — переключение темы работает мгновенно.
/// </summary>
public class HighlightBorder : ContentControl
{
    public static readonly StyledProperty<bool> IsComparedProperty =
        AvaloniaProperty.Register<HighlightBorder, bool>(nameof(IsCompared));

    public static readonly StyledProperty<bool> IsMovedProperty =
        AvaloniaProperty.Register<HighlightBorder, bool>(nameof(IsMoved));

    public bool IsCompared
    {
        get => GetValue(IsComparedProperty);
        set => SetValue(IsComparedProperty, value);
    }

    public bool IsMoved
    {
        get => GetValue(IsMovedProperty);
        set => SetValue(IsMovedProperty, value);
    }

    private void UpdateClasses()
    {
        Classes.Set("hl-compare", IsCompared && !IsMoved);
        Classes.Set("hl-move", IsMoved);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsComparedProperty || e.Property == IsMovedProperty)
            UpdateClasses();
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SortLab.Controls;

namespace SortLab.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.Alt || e.Key is not (Key.P or Key.Right))
            return;

        var playback = this.GetVisualDescendants().OfType<PlaybackControls>()
            .FirstOrDefault(control => control.IsEffectivelyVisible);
        if (playback is not null)
            e.Handled = playback.TryExecute(e.Key == Key.Right);
    }
}

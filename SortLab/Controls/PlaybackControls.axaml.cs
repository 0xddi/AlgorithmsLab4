using Avalonia.Controls;
using Avalonia.Input;

namespace SortLab.Controls;

public partial class PlaybackControls : UserControl
{
    public PlaybackControls()
    {
        InitializeComponent();
    }

    public bool TryExecute(Key key)
    {
        var button = key == Key.Left ? BackButton : key == Key.Right ? StepButton : ContinueButton.IsVisible ? ContinueButton
            : PauseButton.IsVisible ? PauseButton : StartButton;
        if (!button.IsEnabled || button.Command?.CanExecute(null) != true)
            return false;

        button.Command.Execute(null);
        return true;
    }
}

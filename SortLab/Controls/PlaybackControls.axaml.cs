using Avalonia.Controls;

namespace SortLab.Controls;

public partial class PlaybackControls : UserControl
{
    public PlaybackControls()
    {
        InitializeComponent();
    }

    public bool TryExecute(bool step)
    {
        var button = step ? StepButton : ContinueButton.IsVisible ? ContinueButton
            : PauseButton.IsVisible ? PauseButton : StartButton;
        if (!button.IsEnabled || button.Command?.CanExecute(null) != true)
            return false;

        button.Command.Execute(null);
        return true;
    }
}

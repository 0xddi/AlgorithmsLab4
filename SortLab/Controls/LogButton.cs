using Avalonia.Controls;

namespace SortLab.Controls;

public sealed class LogButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    protected override async void OnClick()
    {
        base.OnClick();
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var journal = new LogView { DataContext = DataContext, Margin = new Avalonia.Thickness(16) };
        var window = new Window
        {
            Title = "Журнал операций",
            Width = Math.Min(960, owner.Bounds.Width),
            Height = Math.Min(620, owner.Bounds.Height),
            MinWidth = 540,
            MinHeight = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = journal
        };
        await window.ShowDialog(owner);
        journal.DataContext = null;
    }
}

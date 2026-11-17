using Avalonia.Controls;

namespace SortLab.Controls;

public sealed class DetailButton : Button
{
    private readonly Flyout _panel = new() { Placement = PlacementMode.Top };

    public Control? Detail { get; set; }

    protected override Type StyleKeyOverride => typeof(Button);

    public DetailButton()
    {
        _panel.FlyoutPresenterClasses.Add("details");
        _panel.OverlayInputPassThroughElement = this;
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (_panel.IsOpen)
        {
            _panel.Hide();
            return;
        }

        if (Detail is null) return;
        _panel.Content = Detail;
        _panel.ShowAt(this);
    }
}

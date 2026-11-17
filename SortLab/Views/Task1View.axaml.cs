using Avalonia.Controls;

namespace SortLab.Views;

public partial class Task1View : UserControl
{
    public Task1View()
    {
        InitializeComponent();
        LegendLayout.Attach(this, LegendRail, LegendButton);
    }
}

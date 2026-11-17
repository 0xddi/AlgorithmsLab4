using Avalonia.Controls;

namespace SortLab.Views;

internal static class LegendLayout
{
    internal static void Attach(UserControl page, Grid rail, Button button)
    {
        page.SizeChanged += (_, e) =>
        {
            // Центрированной сцене нужны равные поля по обеим сторонам.
            bool hasSideSpace = e.NewSize.Width >= 1160 + 2 * 230;
            rail.IsVisible = hasSideSpace;
            button.IsVisible = !hasSideSpace;
        };
    }
}

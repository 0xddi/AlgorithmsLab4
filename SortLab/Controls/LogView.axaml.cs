using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SortLab.Controls;

/// <summary>
/// Единый компонент журнала операций: таблица с прокруткой, фильтр по типу и очистка.
/// Автопрокрутка — только когда пользователь находится у низа списка, с троттлингом
/// и защитой от исключений (чтобы поток записей не мог уронить приложение).
/// </summary>
public partial class LogView : UserControl
{
    private readonly Stopwatch _scrollClock = Stopwatch.StartNew();

    public LogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookScroll();
    }

    private async void OpenFullLog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var journal = new LogView { DataContext = DataContext, Margin = new Avalonia.Thickness(16) };
        journal.ExpandButton.IsVisible = false;
        journal.Filters.IsVisible = true;
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

    private INotifyCollectionChanged? _observed;

    private void HookScroll()
    {
        if (_observed is not null) _observed.CollectionChanged -= OnEntriesChanged;
        _observed = DataContext is null ? null : LogGrid.ItemsSource as INotifyCollectionChanged;
        if (_observed is not null) _observed.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is not { Count: > 0 }) return;

        // Троттлинг: не чаще ~8 раз в секунду, иначе поток шагов завалит очередь диспетчера.
        if (_scrollClock.ElapsedMilliseconds < 120) return;
        _scrollClock.Restart();

        var last = e.NewItems[^1];
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // Прокручиваем вниз, только если пользователь и так внизу (не мешаем читать).
                if (LogGrid.GetVisualDescendants().OfType<ScrollViewer>().LastOrDefault() is not { } sv)
                    return;
                bool atBottom = sv.Offset.Y >= sv.Extent.Height - sv.Viewport.Height - 40;
                if (!atBottom) return;
                LogGrid.ScrollIntoView(last, null);
            }
            catch
            {
                // Прокрутка не критична — любая ошибка здесь не должна ронять приложение.
            }
        }, DispatcherPriority.Background);
    }
}

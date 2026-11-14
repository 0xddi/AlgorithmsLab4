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

    private void HookScroll()
    {
        if (LogGrid.ItemsSource is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged -= OnEntriesChanged;
            incc.CollectionChanged += OnEntriesChanged;
        }
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

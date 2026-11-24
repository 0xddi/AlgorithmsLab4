namespace SortLab.Models;

/// <summary>Тип операции в шаге сортировки.</summary>
public enum SortOpType
{
    Compare,   // сравнение
    Swap,      // перестановка / обмен
    Assign,    // присваивание / перемещение
    MarkSorted,// отметка отсортированной части
    Info,      // пояснение без операции
}

/// <summary>Как подсвечивать элемент массива на текущем шаге.</summary>
public enum StepMark
{
    Normal,
    Compare, // сравниваемый
    Swap,    // участвующий в перестановке / ключевой
    Sorted,  // уже отсортирован
    Pivot,   // опорный элемент
    HeapParent,
    HeapChild,
    LessThanPivot,
    AtLeastPivot,
}

/// <summary>Один шаг алгоритма сортировки: снимок массива, подсветка и пояснение.</summary>
public sealed record SortStep(
    SortOpType Op,
    int[] Values,
    StepMark[] Marks,
    int IndexA,
    int IndexB,
    string Explanation,
    int? SavedKey = null,
    HeapFrame? Heap = null,
    int Gap = 0,
    RangeFrame? Call = null);

// Границы участков включительные, с нуля.
public sealed record RangeFrame(int Start, int End, RangeFrame? Caller = null, string Detail = "", string Status = "Сейчас");

public sealed record RangeCard(string Status, int Start, int End, string Detail, bool IsCurrent = false)
{
    public string Title => $"{Status} · {(Start == End ? $"позиция {Start}" : $"позиции {Start}–{End}")}";
}

// Индексы элементов кучи начинаются с нуля.
public sealed record HeapFrame(int Size, int? Parent = null)
{
    public int? Left => 2 * Parent + 1;
    public int? Right => Left + 1;
}

/// <summary>
/// Контекст выполнения алгоритма: массив, подсветка, удобные методы для формирования шагов.
/// Алгоритмы не зависят от UI и работают только с этим контекстом.
/// </summary>
public sealed class SortCtx
{
    public readonly int[] A;
    public readonly int N;
    private readonly StepMark[] _m;

    public SortCtx(int[] input)
    {
        A = (int[])input.Clone();
        N = A.Length;
        _m = new StepMark[N];
    }

    /// <summary>
    /// true: каждый шаг содержит копию массива и подсветки (для визуализации).
    /// false: снимки не копируются (для замеров времени на больших массивах).
    /// </summary>
    public bool Snapshots { get; init; } = true;

    /// <summary>Сформировать шаг с копией текущего состояния массива и подсветки.</summary>
    public SortStep S(SortOpType op, int ia, int ib, string explanation, HeapFrame? heap = null) =>
        new(op,
            Snapshots ? (int[])A.Clone() : Array.Empty<int>(),
            Snapshots ? (StepMark[])_m.Clone() : Array.Empty<StepMark>(),
            ia, ib, explanation, Heap: heap);

    public void MarkAll(StepMark m)
    {
        for (int i = 0; i < N; i++) _m[i] = m;
    }

    public void Mark(int i, StepMark m)
    {
        if (i >= 0 && i < N) _m[i] = m;
    }

    /// <summary>Отметить включительно диапазон [from..to].</summary>
    public void MarkRange(int from, int to, StepMark m)
    {
        for (int i = Math.Max(0, from); i <= Math.Min(N - 1, to); i++) _m[i] = m;
    }

    public void Swap(int i, int j) => (A[i], A[j]) = (A[j], A[i]);
}

/// <summary>Элемент столбика визуализации.</summary>
public sealed record SortBar(int Value, StepMark Mark, bool IsCompared = false);

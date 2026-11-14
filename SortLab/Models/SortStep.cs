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
}

/// <summary>Один шаг алгоритма сортировки: снимок массива, подсветка и пояснение.</summary>
public sealed record SortStep(
    SortOpType Op,
    int[] Values,
    StepMark[] Marks,
    int IndexA,
    int IndexB,
    string Explanation);

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

    /// <summary>Сформировать шаг с копией текущего состояния массива и подсветки.</summary>
    public SortStep S(SortOpType op, int ia, int ib, string explanation) =>
        new(op, (int[])A.Clone(), (StepMark[])_m.Clone(), ia, ib, explanation);

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
public sealed record SortBar(int Value, StepMark Mark);

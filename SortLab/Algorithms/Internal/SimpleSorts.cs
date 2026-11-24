using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Сортировка вставками: каждый новый элемент «втыкается» в уже упорядоченную часть.</summary>
public sealed class InsertionSort : ISortAlgorithm
{
    public string Name => "Вставками (Insertion Sort)";
    public string Reference =>
        """
        Ключ: число, сохранённое до сдвигов.

        Проверяем предыдущую позицию. Число больше ключа сдвигаем на одну позицию. При равенстве вставляем ключ после него.

        Упорядоченный участок растёт на одну позицию за вставку.
        """;
    public string Description =>
        """
        Представьте ряд карт, уже разложенных по возрастанию. Берём следующее число и запоминаем его: это ключ. Идём от него влево. Пока сосед больше ключа, копируем соседа на одну позицию вправо. Вставляем ключ в освободившуюся клетку. Упорядоченная часть выросла на один элемент.

        Пример: в ряду [2, 7, 9, 5] первые три числа уже по порядку. Запоминаем 5, сдвигаем 9 и 7, останавливаемся у 2. Получаем [2, 5, 7, 9]. Здесь два сдвига, хотя ни одна пара не обменивалась местами.

        Сложность по времени (O большое, n — число элементов). Лучший случай O(n), массив уже упорядочен. Средний и худший случаи O(n²).
        """;

    public IEnumerable<SortStep> GetSteps(int[] input, bool snapshots)
    {
        var c = new SortCtx(input) { Snapshots = snapshots };
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, "Первое число само образует упорядоченный участок. По очереди вставляем в него остальные числа.");
        for (int i = 1; i < c.N; i++)
        {
            int key = c.A[i];
            c.Mark(i, StepMark.Swap);
            yield return c.S(SortOpType.Assign, i, -1, $"Сохраняем ключ {key} с позиции {i}. Ищем место для него среди первых {i} элементов.");
            int j = i - 1;
            while (j >= 0)
            {
                c.Mark(j, StepMark.Compare);
                yield return c.S(SortOpType.Compare, j, i, $"Проверяем, больше ли число {c.A[j]} на позиции {j}, чем сохранённый ключ {key}.") with { SavedKey = key };
                if (c.A[j] > key)
                {
                    c.A[j + 1] = c.A[j];
                    c.Mark(j, StepMark.Normal);
                    c.Mark(j + 1, StepMark.Swap);
                    yield return c.S(SortOpType.Assign, j + 1, j, $"{c.A[j + 1]} больше {key}, сдвигаем {c.A[j + 1]} с позиции {j} на позицию {j + 1}.");
                    c.Mark(j + 1, StepMark.Normal);
                    j--;
                }
                else
                {
                    yield return c.S(SortOpType.Info, j, -1, $"{key} не меньше {c.A[j]}, значит, ключ займёт следующую позицию {j + 1}.");
                    break;
                }
            }
            c.A[j + 1] = key;
            c.MarkRange(0, i, StepMark.Sorted);
            yield return c.S(SortOpType.Assign, j + 1, -1, $"{(j < 0 ? "Достигли начала массива. " : "")}Вставляем ключ {key} на позицию {j + 1}. Первые {i + 1} элементов теперь по порядку.");
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Сортировка Шелла: сравниваем числа на уменьшающемся расстоянии.</summary>
public sealed class ShellSort : ISortAlgorithm
{
    public string Name => "Шелла (Shell Sort)";
    public string Reference =>
        """
        Цепочка — ряд элементов, соседние индексы в котором отличаются на один и тот же шаг. При шаге 4 индексы 0, 4, 8 образуют одну цепочку, а 1, 5, 9 — другую. Первый шаг равен целой части (длина массива / 2). Затем так же делим пополам предыдущий шаг.

        Ключ — сохранённое число. Если предыдущее число в цепочке больше ключа, сдвигаем его вправо и ищем место для того же ключа ещё на шаг левее. Если предыдущее число не больше ключа или цепочка закончилась, вставляем ключ и берём следующий элемент массива.
        """;
    public string Description =>
        """
        Цепочка — элементы массива, взятые через одинаковое число позиций. Например, при шаге 4 в одной цепочке индексы 0, 4, 8, в другой — 1, 5, 9. В каждой цепочке выполняем сортировку вставками.

        Для восьми чисел шаги равны 4, 2 и 1: каждый раз делим шаг пополам и отбрасываем остаток. Если на позициях 0 и 4 стоят 9 и 2, число 2 сразу проходит четыре позиции влево. При шаге 1 все числа входят в одну цепочку — последний проход упорядочивает весь массив.

        Идём назад, пока предыдущее число в цепочке больше сохранённого ключа. Сдвигаем это число вправо на шаг, затем сравниваем тот же ключ с числом ещё на шаг левее, если такое число есть. Если число не больше ключа или мы дошли до начала цепочки, вставляем ключ и берём ключ с позиции сразу после той, с которой взяли предыдущий ключ. Первая пара для нового ключа тоже сдвигается вправо на одну позицию. Расстояние между сравниваемыми позициями остаётся тем же. Сам шаг уменьшается только после прохода по всему массиву.

        Сложность по времени (O большое, n — число элементов). Для шагов с делением пополам лучший случай O(n log n), худший O(n²).
        """;

    public IEnumerable<SortStep> GetSteps(int[] input, bool snapshots)
    {
        var c = new SortCtx(input) { Snapshots = snapshots };
        c.MarkAll(StepMark.Normal);
        int previousGap = c.N;
        int gap = previousGap / 2;
        while (gap > 0)
        {
            yield return c.S(SortOpType.Info, 0, gap, $"{(previousGap == c.N ? "Длина массива" : "Предыдущий шаг")}: {previousGap}. Новый шаг: целая часть ({previousGap} / 2), то есть {gap}.") with { Gap = gap };
            for (int i = gap; i < c.N; i++)
            {
                int key = c.A[i];
                int j = i;
                while (j >= gap)
                {
                    c.Mark(j - gap, StepMark.Compare);
                    c.Mark(j, StepMark.Swap);
                    string decision = c.A[j - gap] > key ? "Число больше ключа. Сдвинем его вправо на шаг и перенесём место для ключа на его позицию." : "Число не больше ключа. Вставим ключ после него в этой цепочке.";
                    yield return c.S(SortOpType.Compare, j - gap, j, $"Ключ {key} взят с позиции {i}. Проверяем предыдущую позицию цепочки: {j} − {gap} = {j - gap}, там число {c.A[j - gap]}. {decision}") with { SavedKey = key, Gap = gap };
                    if (c.A[j - gap] > key)
                    {
                        c.A[j] = c.A[j - gap];
                        c.Mark(j - gap, StepMark.Normal);
                        c.Mark(j, StepMark.Swap);
                        yield return c.S(SortOpType.Assign, j, j - gap, $"{c.A[j]} больше {key}, сдвигаем {c.A[j]} с позиции {j - gap} на позицию {j}.") with { Gap = gap };
                        c.Mark(j, StepMark.Normal);
                        j -= gap;
                    }
                    else
                    {
                        c.Mark(j - gap, StepMark.Normal);
                        break;
                    }
                }
                c.A[j] = key;
                c.Mark(j, StepMark.Swap);
                string next = i + 1 < c.N ? $"Дальше берём ключ с позиции {i + 1}. Шаг остаётся {gap}."
                    : "Проход с этим шагом закончен.";
                yield return c.S(SortOpType.Assign, j, -1, $"{(j < gap ? "Достигли начала цепочки" : $"Предыдущее число {c.A[j - gap]} не больше ключа {key}")}. Записываем ключ {key} на позицию {j}. {next}") with { Gap = gap };
                c.Mark(j, StepMark.Normal);
            }
            previousGap = gap;
            gap /= 2;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Сортировка выбором: находим минимум и ставим его в начало.</summary>
public sealed class SelectionSort : ISortAlgorithm
{
    public string Name => "Выбором (Selection Sort)";
    public string Reference =>
        """
        Кандидат: наименьшее из уже проверенных чисел участка. При равенстве сохраняем первого кандидата.

        После просмотра участка минимум занимает его первую позицию. Если он уже там, обмен пропускаем.
        """;
    public string Description =>
        """
        Слева накапливаются числа, уже стоящие на окончательных местах. На оставшемся участке считаем первое число кандидатом в минимумы. Сравниваем с ним каждое следующее число и запоминаем позицию нового кандидата, если нашли меньшее.

        В [5, 3, 1, 4] кандидаты сменяются так: 5 → 3 → 1. Только после просмотра всех четырёх чисел меняем 5 и 1 местами. На первом проходе получаем три сравнения и один обмен. Если минимум уже стоит в начале участка, обмен пропускаем.

        Сложность по времени (O большое, n — число элементов). O(n²) сравнений при любом порядке, потому что каждый раз просматриваем весь оставшийся участок.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input, bool snapshots)
    {
        var c = new SortCtx(input) { Snapshots = snapshots };
        c.MarkAll(StepMark.Normal);
        for (int i = 0; i < c.N - 1; i++)
        {
            int min = i;
            c.Mark(min, StepMark.Pivot);
            yield return c.S(SortOpType.Info, i, -1, $"Ищем самое маленькое число на участке от позиции {i} до {c.N - 1}. Пока кандидат: {c.A[min]}.");
            for (int j = i + 1; j < c.N; j++)
            {
                c.Mark(j, StepMark.Compare);
                yield return c.S(SortOpType.Compare, j, min, $"Число {c.A[j]} на позиции {j} сравниваем с кандидатом {c.A[min]} на позиции {min}. {(c.A[j] < c.A[min] ? "Нашли меньшее число." : "Меньшего числа не нашли, кандидат сохраняется.")}");
                if (c.A[j] < c.A[min])
                {
                    c.Mark(min, StepMark.Normal);
                    min = j;
                    c.Mark(min, StepMark.Pivot);
                    yield return c.S(SortOpType.Info, j, -1, $"{c.A[j]} меньше, теперь кандидат на самое маленькое.");
                }
                else
                {
                    c.Mark(j, StepMark.Normal);
                }
            }
            if (min != i)
            {
                c.Swap(i, min);
                c.Mark(i, StepMark.Swap);
                c.Mark(min, StepMark.Swap);
                yield return c.S(SortOpType.Swap, i, min, $"Ставим самое маленькое число на позицию {i}: меняем местами {c.A[i]} и {c.A[min]}.");
                c.Mark(min, StepMark.Normal);
            }
            c.Mark(i, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, i, -1, min == i ? $"Минимум {c.A[i]} уже стоит в начале участка, на позиции {i}. Обмен пропускаем." : $"Позиция {i} занята минимумом участка, числом {c.A[i]}.");
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Улучшенная сортировка пузырьком: после прохода смотрим только до последнего обмена.</summary>
public sealed class ImprovedBubbleSort : ISortAlgorithm
{
    public string Name => "Пузырьковая сортировка";
    public string Reference =>
        """
        Пара: числа на двух соседних позициях. Меняем их, только если первое больше второго.

        Последний обмен задаёт границу следующего прохода: проверяем пары, которые целиком помещаются до первой позиции этого обмена включительно. Проход без обменов завершает сортировку.
        """;
    public string Description =>
        """
        Проход: движение слева направо по ещё не готовой части массива. Сравниваем соседей. Если левое число больше, меняем пару местами. Так большое число может за один проход несколько раз сдвинуться вправо.

        В [3, 2, 1, 4] меняем 3 с 2, затем 3 с 1. Пара 3 и 4 уже по порядку. Получаем [2, 1, 3, 4]. Дальше проверяем только 2 и 1: начиная с правой позиции последнего обмена хвост уже готов. Проход без обменов завершает сортировку.

        Сложность по времени (O большое, n — число элементов). Лучший случай O(n), проход без обменов. Средний и худший случаи O(n²).
        """;

    public IEnumerable<SortStep> GetSteps(int[] input, bool snapshots)
    {
        var c = new SortCtx(input) { Snapshots = snapshots };
        c.MarkAll(StepMark.Normal);
        int last = c.N - 1;
        while (last > 0)
        {
            int newLast = -1;
            for (int i = 0; i < last; i++)
            {
                c.Mark(i, StepMark.Compare);
                c.Mark(i + 1, StepMark.Compare);
                string explanation = c.A[i] > c.A[i + 1]
                    ? $"{c.A[i]} больше {c.A[i + 1]}. На следующем шаге поменяем их местами."
                    : $"Соседи {c.A[i]} и {c.A[i + 1]} уже по порядку. Обмен не нужен.";
                yield return c.S(SortOpType.Compare, i, i + 1, $"Позиции {i} и {i + 1}. {explanation}");
                if (c.A[i] > c.A[i + 1])
                {
                    c.Swap(i, i + 1);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(i + 1, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, i + 1, $"{c.A[i + 1]} больше {c.A[i]}, меняем соседей местами.");
                    newLast = i;
                }
                c.Mark(i, StepMark.Normal);
                c.Mark(i + 1, StepMark.Normal);
            }
            if (newLast < 0)
            {
                yield return c.S(SortOpType.Info, -1, -1, "Ни одного обмена не случилось, значит, всё уже по порядку!");
                break;
            }
            // Всё правее последнего обмена уже точно на своих местах.
            c.MarkRange(newLast + 1, c.N - 1, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, newLast + 1, -1, $"Последний обмен затронул позиции {newLast} и {newLast + 1}. От позиции {newLast + 1} до конца числа уже упорядочены.");
            last = newLast;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Сортировка вставками: каждый новый элемент «втыкается» в уже упорядоченную часть.</summary>
public sealed class InsertionSort : ISortAlgorithm
{
    public string Name => "Вставками (Insertion Sort)";
    public string Description =>
        """
        Представьте ряд карт, уже разложенных по возрастанию. Берём следующее число и запоминаем его: это ключ. Идём от него влево. Пока сосед больше ключа, копируем соседа на одну клетку вправо. Вставляем ключ в освободившуюся клетку. Упорядоченная часть выросла на один элемент.

        Пример: в ряду [2, 7, 9, 5] первые три числа уже по порядку. Запоминаем 5, сдвигаем 9 и 7, останавливаемся у 2. Получаем [2, 5, 7, 9]. Здесь два сдвига, хотя ни одна пара не обменивалась местами.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, "Идём слева направо: слева от «пальца» держим уже упорядоченные числа.");
        for (int i = 1; i < c.N; i++)
        {
            int key = c.A[i];
            c.Mark(i, StepMark.Swap);
            yield return c.S(SortOpType.Assign, i, -1, $"Запоминаем число {key}. Будем искать ему место среди чисел слева.");
            int j = i - 1;
            while (j >= 0)
            {
                c.Mark(j, StepMark.Compare);
                yield return c.S(SortOpType.Compare, j, i, $"Сравниваем {c.A[j]} и {key}: {c.A[j]} больше {key}?") with { SavedKey = key };
                if (c.A[j] > key)
                {
                    c.A[j + 1] = c.A[j];
                    c.Mark(j, StepMark.Normal);
                    c.Mark(j + 1, StepMark.Swap);
                    yield return c.S(SortOpType.Assign, j + 1, j, $"{c.A[j + 1]} больше {key} — сдвигаем {c.A[j + 1]} на одну клетку вправо.");
                    c.Mark(j + 1, StepMark.Normal);
                    j--;
                }
                else
                {
                    yield return c.S(SortOpType.Info, j, -1, $"{key} не меньше {c.A[j]} — значит, место найдено сразу справа от {c.A[j]}.");
                    break;
                }
            }
            c.A[j + 1] = key;
            c.MarkRange(0, i, StepMark.Sorted);
            yield return c.S(SortOpType.Assign, j + 1, -1, $"Вставляем {key} на позицию {j + 2}. Числа слева снова по порядку!");
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Сортировка Шелла: сравниваем числа на уменьшающемся расстоянии.</summary>
public sealed class ShellSort : ISortAlgorithm
{
    public string Name => "Шелла (Shell Sort)";
    public string Description =>
        """
        Шаг — расстояние между позициями. При шаге 4 позиции 1, 5, 9 образуют одну цепочку, а 2, 6, 10 — другую. Внутри каждой цепочки делаем сортировку вставками: запоминаем очередное число и сдвигаем большие числа вправо на целый шаг.

        Для восьми чисел шаги равны 4, 2 и 1: каждый раз делим шаг пополам и отбрасываем остаток. Если на позициях 1 и 5 стоят 9 и 2, число 2 сразу проходит четыре позиции влево. При шаге 1 все числа входят в одну цепочку — последний проход упорядочивает весь массив.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        int gap = c.N / 2;
        while (gap > 0)
        {
            yield return c.S(SortOpType.Info, -1, -1, $"Меняем правило: теперь сравниваем числа, стоящие через {gap} клеток друг от друга.");
            for (int i = gap; i < c.N; i++)
            {
                int key = c.A[i];
                int j = i;
                while (j >= gap)
                {
                    c.Mark(j - gap, StepMark.Compare);
                    c.Mark(j, StepMark.Swap);
                    yield return c.S(SortOpType.Compare, j - gap, j, $"Сравниваем {c.A[j - gap]} и {key}: они стоят через {gap} клеток.") with { SavedKey = key };
                    if (c.A[j - gap] > key)
                    {
                        c.A[j] = c.A[j - gap];
                        c.Mark(j - gap, StepMark.Normal);
                        c.Mark(j, StepMark.Swap);
                        yield return c.S(SortOpType.Assign, j, j - gap, $"{c.A[j]} больше {key} — сдвигаем {c.A[j]} на {gap} клеток вправо.");
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
                yield return c.S(SortOpType.Assign, j, -1, $"Кладём {key} на позицию {j + 1}.");
                c.Mark(j, StepMark.Normal);
            }
            gap /= 2;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Шаг стал равен 1, все числа по порядку.");
    }
}

/// <summary>Сортировка выбором: находим минимум и ставим его в начало.</summary>
public sealed class SelectionSort : ISortAlgorithm
{
    public string Name => "Выбором (Selection Sort)";
    public string Description =>
        """
        Слева накапливаются числа, уже стоящие на окончательных местах. На оставшемся участке считаем первое число кандидатом в минимумы. Сравниваем с ним каждое следующее число и запоминаем позицию нового кандидата, если нашли меньшее.

        В [5, 3, 1, 4] кандидаты сменяются так: 5 → 3 → 1. Только после просмотра всех четырёх чисел меняем 5 и 1 местами. На первом проходе получаем три сравнения и один обмен. Если минимум уже стоит в начале участка, обмен пропускаем.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        for (int i = 0; i < c.N - 1; i++)
        {
            int min = i;
            c.Mark(min, StepMark.Pivot);
            yield return c.S(SortOpType.Info, i, -1, $"Ищем самое маленькое число на участке от позиции {i + 1} до {c.N}. Пока кандидат — {c.A[min]}.");
            for (int j = i + 1; j < c.N; j++)
            {
                c.Mark(j, StepMark.Compare);
                yield return c.S(SortOpType.Compare, j, min, $"Сравниваем {c.A[j]} и {c.A[min]}: какое меньше?");
                if (c.A[j] < c.A[min])
                {
                    c.Mark(min, StepMark.Normal);
                    min = j;
                    c.Mark(min, StepMark.Pivot);
                    yield return c.S(SortOpType.Info, j, -1, $"{c.A[j]} меньше — теперь кандидат на самое маленькое.");
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
                yield return c.S(SortOpType.Swap, i, min, $"Ставим самое маленькое число на позицию {i + 1}: меняем местами {c.A[i]} и {c.A[min]}.");
                c.Mark(min, StepMark.Normal);
            }
            c.Mark(i, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, i, -1, $"Позиция {i + 1} занята: {c.A[i]} — самое маленькое из оставшихся.");
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Улучшенная сортировка пузырьком: после прохода смотрим только до последнего обмена.</summary>
public sealed class ImprovedBubbleSort : ISortAlgorithm
{
    public string Name => "Пузырьковая сортировка";
    public string Description =>
        """
        Проход — движение слева направо по ещё не готовой части массива. Сравниваем соседей. Если левое число больше, меняем пару местами. Так большое число может за один проход несколько раз сдвинуться вправо.

        В [3, 2, 1, 4] меняем 3 с 2, затем 3 с 1. Пара 3 и 4 уже по порядку. Получаем [2, 1, 3, 4]. Дальше проверяем только 2 и 1: начиная с правой позиции последнего обмена хвост уже готов. Проход без обменов завершает сортировку.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
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
                yield return c.S(SortOpType.Compare, i, i + 1, explanation);
                if (c.A[i] > c.A[i + 1])
                {
                    c.Swap(i, i + 1);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(i + 1, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, i + 1, $"{c.A[i + 1]} больше {c.A[i]} — меняем соседей местами.");
                    newLast = i;
                }
                c.Mark(i, StepMark.Normal);
                c.Mark(i + 1, StepMark.Normal);
            }
            if (newLast < 0)
            {
                yield return c.S(SortOpType.Info, -1, -1, "Ни одного обмена не случилось — значит, всё уже по порядку!");
                break;
            }
            // Всё правее последнего обмена уже точно на своих местах.
            c.MarkRange(newLast + 1, c.N - 1, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, newLast + 1, -1, $"С позиции {newLast + 2} до конца числа уже на своих местах.");
            last = newLast;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

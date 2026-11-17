namespace SortLab.Services;

/// <summary>Ближайшие шаги остаются в кольцевом буфере. Для дальнего возврата
/// восстанавливаем поток из тех же исходных данных без хранения всего прогона.</summary>
internal sealed class StepCursor<T>(IEnumerable<T> steps, int capacity = 64) : IDisposable where T : class
{
    private readonly T?[] _recent = new T[capacity];
    private IEnumerator<T> _source = steps.GetEnumerator();
    private int _read;
    private int _position;

    public T Current => _recent[(_position - 1) % capacity]!;

    public bool MoveNext()
    {
        if (_position == _read)
        {
            if (!_source.MoveNext()) return false;
            _recent[_read % capacity] = _source.Current;
            _read++;
        }
        _position++;
        return true;
    }

    public void MoveBack()
    {
        int target = _position - 1;
        if (target > 0 && target <= _read - capacity)
        {
            _source.Dispose();
            _source = steps.GetEnumerator();
            _position = _read = 0;
            while (_position < target)
                if (!MoveNext()) throw new InvalidOperationException("Не удалось восстановить шаг.");
        }
        _position = target;
    }

    public void Dispose() => _source.Dispose();
}

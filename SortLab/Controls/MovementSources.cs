namespace SortLab.Controls;

internal static class MovementSources
{
    // Keep unchanged duplicates in place and match displaced copies in their original order.
    public static int[]? Match<T, TKey>(IReadOnlyList<T> previous, IReadOnlyList<T> next,
        Func<T, TKey> key) where TKey : notnull
    {
        var comparer = EqualityComparer<TKey>.Default;
        Dictionary<TKey, Queue<int>>? displaced = null;
        for (int i = 0; i < previous.Count; i++)
        {
            var value = key(previous[i]);
            if (comparer.Equals(value, key(next[i]))) continue;
            displaced ??= new();
            if (!displaced.TryGetValue(value, out var slots))
                displaced[value] = slots = new();
            slots.Enqueue(i);
        }
        if (displaced is null) return null;

        // Assignment steps can copy a value whose original slot is still occupied.
        var copies = new Dictionary<TKey, int>();
        for (int i = 0; i < previous.Count; i++)
            copies.TryAdd(key(previous[i]), i);

        var sources = new int[next.Count];
        for (int i = 0; i < next.Count; i++)
        {
            var value = key(next[i]);
            sources[i] = i;
            if (comparer.Equals(value, key(previous[i]))) continue;
            if (displaced.TryGetValue(value, out var slots) && slots.TryDequeue(out int source))
                sources[i] = source;
            else if (copies.TryGetValue(value, out source))
                sources[i] = source;
        }
        return sources;
    }
}

namespace SortLab.Models;

/// <summary>Одна запись журнала операций (универсальная для всех вкладок).</summary>
public sealed class LogEntry
{
    public int Step { get; init; }
    public string Operation { get; init; } = "";
    public string Indices { get; init; } = "";
    public string Values { get; init; } = "";
    public string Explanation { get; init; } = "";
}

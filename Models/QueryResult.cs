namespace Models;

public sealed class QueryResult
{
    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public int RowCount { get; init; }
    public bool Truncated { get; init; }
}

public sealed class TableDescription
{
    public required string Table { get; set; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Columns { get; set; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Indexes { get; set; }
}
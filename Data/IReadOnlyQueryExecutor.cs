using Models;

namespace Data;

public interface IReadOnlyQueryExecutor
{
    Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int? maxRows = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken = default);

    Task<TableDescription> DescribeTableAsync(string tableName, CancellationToken cancellationToken = default);
}

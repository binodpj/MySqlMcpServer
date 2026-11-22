using Configuration;
using Microsoft.Extensions.Options;
using Models;
using MySqlConnector;

namespace Data;

public sealed class MySqlQueryExecutor : IReadOnlyQueryExecutor
{
    private readonly MySqlOptions _mySqlOptions;
    private readonly SecurityOptions _securityOptions;

    public MySqlQueryExecutor(
        IOptions<MySqlOptions> mySqlOptions,
        IOptions<SecurityOptions> securityOptions)
    {
        _mySqlOptions = mySqlOptions.Value;
        _securityOptions = securityOptions.Value;
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int? maxRows = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(sql, connection)
        {
            CommandTimeout = _securityOptions.QueryTimeoutSeconds
        };

        AddParameters(command, parameters);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadQueryResultAsync(reader, maxRows ?? _securityOptions.MaxRows, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME
            """;

        var result = await ExecuteQueryAsync(
            sql,
            new Dictionary<string, object?> { ["schema"] = _mySqlOptions.Database },
            maxRows: int.MaxValue,
            cancellationToken);

        return result.Rows
            .Select(row => row.TryGetValue("TABLE_NAME", out var value) ? value?.ToString() : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
    }

    public async Task<TableDescription> DescribeTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        const string columnsSql = """
            SELECT
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                COLUMN_KEY,
                COLUMN_DEFAULT,
                EXTRA
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;

        const string indexesSql = """
            SELECT
                INDEX_NAME,
                COLUMN_NAME,
                NON_UNIQUE,
                SEQ_IN_INDEX
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
            ORDER BY INDEX_NAME, SEQ_IN_INDEX
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["schema"] = _mySqlOptions.Database,
            ["table"] = tableName
        };

        var columnsResult = await ExecuteQueryAsync(
            columnsSql,
            parameters,
            maxRows: int.MaxValue,
            cancellationToken);

        var indexesResult = await ExecuteQueryAsync(
            indexesSql,
            parameters,
            maxRows: int.MaxValue,
            cancellationToken);

        return new TableDescription
        {
            Table = tableName,
            Columns = columnsResult.Rows,
            Indexes = indexesResult.Rows
        };
    }

    private MySqlConnection CreateConnection()
    {
        var connectionString = _mySqlOptions.BuildConnectionString(_securityOptions.SslMode);
        return new MySqlConnection(connectionString);
    }

    private static void AddParameters(MySqlCommand command, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue($"@{name.TrimStart('@')}", value ?? DBNull.Value);
        }
    }

    private static async Task<QueryResult> ReadQueryResultAsync(
        MySqlDataReader reader,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var truncated = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }

            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[columns[i]] = ConvertValue(reader.GetValue(i));
            }

            rows.Add(row);
        }

        return new QueryResult
        {
            Columns = columns,
            Rows = rows,
            RowCount = rows.Count,
            Truncated = truncated
        };
    }

    private static object? ConvertValue(object value)
    {
        if (value is DBNull)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dateTime => dateTime.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            TimeSpan timeSpan => timeSpan.ToString(),
            Guid guid => guid.ToString(),
            _ => value
        };
    }
}

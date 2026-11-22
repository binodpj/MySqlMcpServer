using System.ComponentModel;
using Configuration;
using Data;
using Helpers;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Security;

namespace Tools;

[McpServerToolType]
public sealed class SchemaTools(
    IReadOnlyQueryExecutor queryExecutor,
    DenyListFilter denyListFilter,
    IOptions<MySqlOptions> mySqlOptions,
    IOptions<SecurityOptions> securityOptions)
{
    [McpServerTool]
    [Description("Lists all tables in the configured MySQL database. Denied tables are excluded from the result.")]
    public async Task<string> ListTables(CancellationToken cancellationToken = default)
    {
        var tables = await queryExecutor.ListTablesAsync(cancellationToken);
        var allowedTables = denyListFilter.FilterTables(tables);

        return JsonResponse.Serialize(new
        {
            database = mySqlOptions.Value.Database,
            tables = allowedTables,
            count = allowedTables.Count
        });
    }

    [McpServerTool]
    [Description("Describes a table schema including columns and indexes. Access is denied for blocked tables.")]
    public async Task<string> DescribeTable(
        [Description("The table name to describe.")] string table,
        CancellationToken cancellationToken = default)
    {
        TableNameValidator.EnsureValid(table);
        denyListFilter.EnsureTableAllowed(table);

        var description = await queryExecutor.DescribeTableAsync(table, cancellationToken);
        var filtered = denyListFilter.RemoveDeniedColumns(description);

        return JsonResponse.Serialize(filtered);
    }

    [McpServerTool]
    [Description("Returns a sample of rows from a table. Denied tables are rejected and denied columns are removed.")]
    public async Task<string> SampleTable(
        [Description("The table name to sample.")] string table,
        [Description("Optional row limit. Capped by the configured MaxRows setting.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        TableNameValidator.EnsureValid(table);
        denyListFilter.EnsureTableAllowed(table);

        var effectiveLimit = Math.Clamp(limit ?? 10, 1, securityOptions.Value.MaxRows);
        var sql = $"SELECT * FROM `{table}` LIMIT @limit";

        denyListFilter.EnsureSqlAllowed(sql);

        var result = await queryExecutor.ExecuteQueryAsync(
            sql,
            new Dictionary<string, object?> { ["limit"] = effectiveLimit },
            maxRows: effectiveLimit,
            cancellationToken);

        var filtered = denyListFilter.RemoveDeniedColumns(result);
        return JsonResponse.Serialize(filtered);
    }
}

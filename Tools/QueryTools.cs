using System.ComponentModel;
using Data;
using Helpers;
using ModelContextProtocol.Server;
using Security;

namespace Tools;

[McpServerToolType]
public sealed class QueryTools(
    IReadOnlyQueryExecutor queryExecutor,
    QueryValidator queryValidator,
    DenyListFilter denyListFilter)
{
    [McpServerTool]
    [Description("Executes a read-only SQL query (SELECT, SHOW, DESCRIBE, EXPLAIN). Non-read statements and denied tables/columns are rejected.")]
    public async Task<string> RunQuery(
        [Description("The read-only SQL query to execute.")] string sql,
        CancellationToken cancellationToken = default)
    {
        var validation = queryValidator.ValidateAndPrepare(sql);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ErrorMessage);
        }

        denyListFilter.EnsureSqlAllowed(validation.Sql!);

        var result = await queryExecutor.ExecuteQueryAsync(
            validation.Sql!,
            cancellationToken: cancellationToken);

        var filtered = denyListFilter.RemoveDeniedColumns(result);
        return JsonResponse.Serialize(filtered);
    }
}

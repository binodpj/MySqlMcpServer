using System.Text.RegularExpressions;
using Configuration;
using Microsoft.Extensions.Options;
using Models;

namespace Security;

public sealed class DenyListFilter
{
    private readonly HashSet<string> _deniedTables;
    private readonly HashSet<string> _deniedColumns;

    public DenyListFilter(IOptions<SecurityOptions> securityOptions)
    {
        var options = securityOptions.Value;

        _deniedTables = new HashSet<string>(
            options.DeniedTables.Where(static table => !string.IsNullOrWhiteSpace(table)),
            StringComparer.OrdinalIgnoreCase);

        _deniedColumns = new HashSet<string>(
            options.DeniedColumns
                .Where(static column => !string.IsNullOrWhiteSpace(column))
                .Select(static column => column.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsTableDenied(string tableName) =>
        _deniedTables.Contains(tableName);

    public void EnsureTableAllowed(string tableName)
    {
        if (IsTableDenied(tableName))
        {
            throw new InvalidOperationException($"Access to table '{tableName}' is denied.");
        }
    }

    public void EnsureSqlAllowed(string sql)
    {
        if (ContainsDeniedReference(sql))
        {
            throw new InvalidOperationException("The query references a denied table or column.");
        }
    }

    public bool ContainsDeniedReference(string sql)
    {
        foreach (var table in _deniedTables)
        {
            if (ContainsTableReference(sql, table))
            {
                return true;
            }
        }

        foreach (var deniedColumn in _deniedColumns)
        {
            if (ContainsColumnReference(sql, deniedColumn))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<string> FilterTables(IEnumerable<string> tables) =>
        tables.Where(table => !IsTableDenied(table)).ToList();

    public QueryResult RemoveDeniedColumns(QueryResult result)
    {
        var deniedColumnNames = GetDeniedColumnNamesForResult(result.Columns);
        if (deniedColumnNames.Count == 0)
        {
            return result;
        }

        var allowedColumns = result.Columns
            .Where(column => !deniedColumnNames.Contains(column))
            .ToList();

        var filteredRows = result.Rows
            .Select(row => (IReadOnlyDictionary<string, object?>)row
                .Where(pair => allowedColumns.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new QueryResult
        {
            Columns = allowedColumns,
            Rows = filteredRows,
            RowCount = filteredRows.Count,
            Truncated = result.Truncated
        };
    }

    public TableDescription RemoveDeniedColumns(TableDescription description)
    {
        var deniedColumnNames = description.Columns
            .Select(column => column.TryGetValue("COLUMN_NAME", out var value) ? value?.ToString() : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => IsColumnDenied(description.Table, name!))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (deniedColumnNames.Count == 0)
        {
            return description;
        }

        var allowedColumns = description.Columns
            .Where(column =>
            {
                var columnName = column.TryGetValue("COLUMN_NAME", out var value) ? value?.ToString() : null;
                return columnName is null || !deniedColumnNames.Contains(columnName);
            })
            .ToList();

        var allowedIndexes = description.Indexes
            .Where(index =>
            {
                var columnName = index.TryGetValue("COLUMN_NAME", out var value) ? value?.ToString() : null;
                return columnName is null || !deniedColumnNames.Contains(columnName);
            })
            .ToList();

        return new TableDescription
        {
            Table = description.Table,
            Columns = allowedColumns,
            Indexes = allowedIndexes
        };
    }

    private HashSet<string> GetDeniedColumnNamesForResult(IReadOnlyList<string> columns)
    {
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
            if (_deniedColumns.Contains(column))
            {
                denied.Add(column);
            }
        }

        return denied;
    }

    private bool IsColumnDenied(string tableName, string columnName)
    {
        return _deniedColumns.Contains(columnName) ||
               _deniedColumns.Contains($"{tableName}.{columnName}");
    }

    private static bool ContainsTableReference(string sql, string tableName)
    {
        var pattern = $@"(?<![A-Za-z0-9_])`?{Regex.Escape(tableName)}`?(?![A-Za-z0-9_])";
        return Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private bool ContainsColumnReference(string sql, string deniedColumn)
    {
        if (deniedColumn.Contains('.'))
        {
            var parts = deniedColumn.Split('.', 2);
            var table = parts[0];
            var column = parts[1];
            var qualifiedPattern =
                $@"(?<![A-Za-z0-9_])`?{Regex.Escape(table)}`?\.`?{Regex.Escape(column)}`?(?![A-Za-z0-9_])";
            return Regex.IsMatch(sql, qualifiedPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        var unqualifiedPattern = $@"(?<![A-Za-z0-9_\.])`?{Regex.Escape(deniedColumn)}`?(?![A-Za-z0-9_])";
        return Regex.IsMatch(sql, unqualifiedPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

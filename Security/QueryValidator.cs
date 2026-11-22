using System.Text;
using System.Text.RegularExpressions;
using Configuration;
using Microsoft.Extensions.Options;

namespace Security;

public sealed partial class QueryValidator
{
    private static readonly string[] AllowedPrefixes =
    [
        "SELECT",
        "SHOW",
        "DESCRIBE",
        "DESC",
        "EXPLAIN"
    ];

    private static readonly string[] BlockedKeywords =
    [
        "INSERT",
        "UPDATE",
        "DELETE",
        "REPLACE",
        "MERGE",
        "DROP",
        "ALTER",
        "CREATE",
        "TRUNCATE",
        "GRANT",
        "REVOKE",
        "CALL",
        "SET",
        "LOCK",
        "UNLOCK",
        "RENAME",
        "HANDLER",
        "PREPARE",
        "EXECUTE",
        "DEALLOCATE"
    ];

    private static readonly string[] BlockedConstructs =
    [
        "INTO OUTFILE",
        "INTO DUMPFILE",
        "LOAD_FILE",
        "LOAD DATA"
    ];

    private readonly SecurityOptions _securityOptions;

    public QueryValidator(IOptions<SecurityOptions> securityOptions)
    {
        _securityOptions = securityOptions.Value;
    }

    public ValidationResult ValidateAndPrepare(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return ValidationResult.Fail("SQL query must not be empty.");
        }

        var trimmed = sql.Trim();
        if (ContainsMultipleStatements(trimmed))
        {
            return ValidationResult.Fail("Multiple SQL statements are not allowed.");
        }

        trimmed = trimmed.TrimEnd(';').Trim();
        var normalized = NormalizeSql(trimmed);

        if (!StartsWithAllowedPrefix(normalized))
        {
            return ValidationResult.Fail("Only read-only SELECT, SHOW, DESCRIBE, and EXPLAIN statements are allowed.");
        }

        if (ContainsBlockedConstruct(normalized))
        {
            return ValidationResult.Fail("The query contains blocked SQL constructs.");
        }

        if (ContainsBlockedKeyword(normalized))
        {
            return ValidationResult.Fail("The query contains blocked SQL keywords.");
        }

        var preparedSql = PrepareLimit(trimmed, normalized);
        return ValidationResult.Success(preparedSql);
    }

    private string PrepareLimit(string originalSql, string normalizedSql)
    {
        if (!normalizedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return originalSql;
        }

        var limitMatch = LimitRegex().Match(originalSql);
        if (!limitMatch.Success)
        {
            return $"{originalSql} LIMIT {_securityOptions.MaxRows}";
        }

        if (!int.TryParse(limitMatch.Groups["count"].Value, out var limitValue))
        {
            return originalSql;
        }

        if (limitValue <= _securityOptions.MaxRows)
        {
            return originalSql;
        }

        return LimitRegex().Replace(originalSql, $"LIMIT {_securityOptions.MaxRows}", 1);
    }

    private static bool ContainsMultipleStatements(string sql)
    {
        var withoutTrailingSemicolon = sql.TrimEnd().TrimEnd(';');
        return withoutTrailingSemicolon.Contains(';');
    }

    private static bool StartsWithAllowedPrefix(string normalizedSql)
    {
        return AllowedPrefixes.Any(prefix =>
            normalizedSql.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsBlockedKeyword(string normalizedSql)
    {
        return BlockedKeywords.Any(keyword =>
            Regex.IsMatch(normalizedSql, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));
    }

    private static bool ContainsBlockedConstruct(string normalizedSql)
    {
        return BlockedConstructs.Any(construct =>
            normalizedSql.Contains(construct, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSql(string sql)
    {
        var withoutComments = StripComments(sql);
        return Regex.Replace(withoutComments, @"\s+", " ").Trim();
    }

    private static string StripComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBacktick = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var current = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (current is '\r' or '\n')
                {
                    inLineComment = false;
                    builder.Append(' ');
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                    builder.Append(' ');
                }

                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && !inBacktick)
            {
                if (current == '-' && next == '-')
                {
                    inLineComment = true;
                    i++;
                    builder.Append(' ');
                    continue;
                }

                if (current == '#')
                {
                    inLineComment = true;
                    builder.Append(' ');
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    builder.Append(' ');
                    continue;
                }
            }

            if (current == '\'' && !inDoubleQuote && !inBacktick)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (current == '"' && !inSingleQuote && !inBacktick)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (current == '`' && !inSingleQuote && !inDoubleQuote)
            {
                inBacktick = !inBacktick;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\bLIMIT\s+(?<count>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LimitRegex();
}

public sealed class ValidationResult
{
    private ValidationResult(bool isValid, string? sql, string? errorMessage)
    {
        IsValid = isValid;
        Sql = sql;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }

    public string? Sql { get; }

    public string? ErrorMessage { get; }

    public static ValidationResult Success(string sql) => new(true, sql, null);

    public static ValidationResult Fail(string errorMessage) => new(false, null, errorMessage);
}

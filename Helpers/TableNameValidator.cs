using System.Text.RegularExpressions;

namespace Helpers;

internal static partial class TableNameValidator
{
    [GeneratedRegex("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidTableNamePattern();

    public static bool IsValid(string? tableName) =>
        !string.IsNullOrWhiteSpace(tableName) && ValidTableNamePattern().IsMatch(tableName);

    public static void EnsureValid(string tableName)
    {
        if (!IsValid(tableName))
        {
            throw new ArgumentException(
                "Invalid table name. Only letters, numbers, and underscores are allowed.",
                nameof(tableName));
        }
    }
}

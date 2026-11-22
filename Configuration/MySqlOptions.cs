namespace Configuration;

public sealed class MySqlOptions
{
    public const string SectionName = "MySql";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3306;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;

    public string BuildConnectionString(string sslMode)
    {
        var builder = new MySqlConnector.MySqlConnectionStringBuilder
        {
            Server = Host,
            Port = (uint)Port,
            UserID = User,
            Password = Password,
            Database = Database,
            SslMode = Enum.TryParse<MySqlConnector.MySqlSslMode>(sslMode, ignoreCase: true, out var mode)
                ? mode
                : MySqlConnector.MySqlSslMode.Preferred
        };

        return builder.ConnectionString;
    }
}
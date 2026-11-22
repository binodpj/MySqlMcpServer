namespace Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public string SslMode { get; set; } = "Preferred";
    public int MaxRows { get; set; } = 100;
    public int QueryTimeoutSeconds { get; set; } = 30;
    public List<string> DeniedTables { get; set; } = ["Users"];
    public List<string> DeniedColumns { get; set; } = [];
}
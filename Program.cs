using Configuration;
using Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Security;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddOptions<MySqlOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.Host = GetRequired(configuration, "MYSQL_HOST");
        options.User = GetRequired(configuration, "MYSQL_USER");
        options.Password = GetRequired(configuration, "MYSQL_PASSWORD");
        options.Database = GetRequired(configuration, "MYSQL_DATABASE");
        options.Port = int.TryParse(configuration["MYSQL_PORT"], out var port) ? port : 3306;
    });

builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .PostConfigure(options =>
    {
        options.DeniedTables ??= ["USER"];
        options.DeniedColumns ??= [];

        if (options.MaxRows <= 0)
        {
            options.MaxRows = 100;
        }

        if (options.QueryTimeoutSeconds <= 0)
        {
            options.QueryTimeoutSeconds = 30;
        }

        if (string.IsNullOrWhiteSpace(options.SslMode))
        {
            options.SslMode = "Preferred";
        }
    });

builder.Services.AddSingleton<IReadOnlyQueryExecutor, MySqlQueryExecutor>();
builder.Services.AddSingleton<QueryValidator>();
builder.Services.AddSingleton<DenyListFilter>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string GetRequired(IConfiguration configuration, string key) =>
    configuration[key] ?? throw new InvalidOperationException($"Required environment variable '{key}' is not set.");


# MySQL Read-Only MCP Server

A .NET 10 stdio MCP server that exposes **read-only** access to a single MySQL database for use with Cursor and other MCP clients.

## Features

- Read-only database access using an existing MySQL read-only user
- In-code SELECT-only query validation (defense in depth)
- Configurable table/column deny-list (default denies `USER` table)
- MCP tools for schema introspection, custom queries.
- Structured JSON responses

## Requirements

- .NET 10 SDK
- MySQL database with a read-only user
- Cursor (or another MCP client supporting stdio transport)

## Build

```bash
dotnet build MySqlMcpServer.csproj
```

## Configuration

### Cursor `mcp.json` (connection only)

Only these environment variables belong in your MCP client config:

| Variable | Description |
|---|---|
| `MYSQL_HOST` | MySQL server host |
| `MYSQL_PORT` | MySQL server port (default `3306` if omitted) |
| `MYSQL_USER` | Read-only MySQL username |
| `MYSQL_PASSWORD` | Read-only MySQL password |
| `MYSQL_DATABASE` | Target database name |

Example Cursor MCP config:

```json
{
  "mcpServers": {
    "mysql": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/MySqlMcp.csproj"
      ],
      "env": {
        "MYSQL_HOST": "your-mysql-host",
        "MYSQL_PORT": "3306",
        "MYSQL_USER": "readonly_user",
        "MYSQL_PASSWORD": "your-password",
        "MYSQL_DATABASE": "your_database"
      }
    }
  }
}
```

Replace the project path and credentials with your values.

### In-code settings (`appsettings.json`)

All other settings live in [`appsettings.json`](MySqlMcpServer/appsettings.json) and are **not** placed in `mcp.json`:

```json
{
  "Security": {
    "SslMode": "Preferred",
    "MaxRows": 100,
    "QueryTimeoutSeconds": 30,
    "DeniedTables": [ "USER" ],
    "DeniedColumns": []
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `SslMode` | `Preferred` | MySQL SSL mode (`Preferred`, `Required`, `Disabled`, etc.) |
| `MaxRows` | `100` | Max rows for custom queries; auto-injected LIMIT when missing |
| `QueryTimeoutSeconds` | `30` | Query timeout |
| `DeniedTables` | `[ "USER" ]` | Tables fully blocked from access and hidden from listings |
| `DeniedColumns` | `[]` | Columns blocked by name (`column`) or qualified (`table.column`) |

## MCP Tools

| Tool | Description |
|---|---|
| `list_tables` | Lists tables in the configured database (denied tables excluded) |
| `describe_table` | Returns column and index metadata for a table |
| `sample_table` | Returns sample rows from a table |
| `run_query` | Executes a validated read-only SQL query |

## Read-only enforcement

1. **Database grants** — connect with a read-only MySQL user
2. **Query validation** — only `SELECT`, `SHOW`, `DESCRIBE`, `EXPLAIN` allowed; DML/DDL and dangerous constructs blocked
3. **Deny-list** — configured tables/columns are rejected or removed from results

Custom queries (`run_query`):
- Single statement only
- Auto-injects `LIMIT` when missing (up to `MaxRows`)
- Clamps explicit `LIMIT` values above `MaxRows`

## Extending the server

### Add a denied table or column

Edit `appsettings.json`:

```json
"DeniedTables": [ "USER", "audit_log" ],
"DeniedColumns": [ "accounts.ssn", "email" ]
```

Restart the MCP server after changes.

## Project structure

```
MySqlMcpServer/
├── Program.cs
├── appsettings.json
├── Configuration/
│   ├── MySqlOptions.cs
│   └── SecurityOptions.cs
├── Data/
│   ├── IReadOnlyQueryExecutor.cs
│   └── MySqlQueryExecutor.cs
├── Security/
│   ├── QueryValidator.cs
│   └── DenyListFilter.cs
├── Tools/
│   ├── SchemaTools.cs
│   ├── QueryTools.cs
│   └── AccountTools.cs
└── Models/
    └── QueryResult.cs
```

## Notes

- Logging goes to **stderr** only; stdout is reserved for MCP JSON-RPC
- One configured target database per server instance
- HTTP/SSE transport can be added later without changing tool logic

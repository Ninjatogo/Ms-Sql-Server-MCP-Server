# Ms-Sql-Server-MCP-Server

The Ms-Sql-Server-MCP-Server is a .NET 7 project that provides a web API for interacting with a Microsoft SQL Server database. It is designed to be used with the Model Context Protocol (MCP) and provides a set of tools for database operations, performance analysis, and schema discovery.

## Getting Started

### Prerequisites

- .NET 7 SDK
- A running instance of Microsoft SQL Server

### Configuration

1. Clone the repository.
2. Open the `appsettings.json` file in the `ServerWebApi` project.
3. Update the `DefaultConnection` connection string to point to your SQL Server instance.

### Running the Server

1. Navigate to the `ServerWebApi` directory.
2. Run the command `dotnet run`.

## MCP Tools

The server exposes a set of tools that can be called through the MCP. These tools are organized into the following categories:

### 1. Database Tools

These tools provide basic and advanced database operations.

- `GetDatabases()`: Get a list of all databases on the SQL Server instance.
- `GetTables(string? database = null)`: Get a list of tables in a specific database.
- `GetTableSchema(string tableName, string? database = null)`: Get the schema/structure of a specific table with sensitivity indicators.
- `ExecuteQuery(string query, string? database = null, int maxRows = 1000)`: Execute a SQL query and return PII-filtered results.
- `ExecuteCommand(string command, string? database = null)`: Execute a SQL command (INSERT, UPDATE, DELETE, etc.) and return the number of affected rows.
- `GetDatabaseInfo(string database)`: Get basic information about a database including table count and size.
- `CheckColumnSensitivity(string columnName)`: Check if a column name is considered sensitive for PII.
- `TestPiiDetection(string testValue, string? columnName = null)`: Test PII detection on a sample value.
- `ExplainQuery(string query, string? database = null)`: Generates an estimated execution plan for a SQL query without executing it.
- `ValidateQuery(string query, string? database = null)`: Validates the syntax of a SQL query without executing it.
- `EstimateQueryCost(string query, string? database = null)`: Estimates the cost of a SQL query without executing it.
- `ExecuteQueryWithStats(string query, string? database = null, int maxRows = 1000)`: Executes a SQL query and returns the results along with execution statistics.

### 2. Performance Analysis Tools

These tools provide performance analysis capabilities.

- `AnalyzeSlowQueries(string? database = null, int topCount = 50, int minimumExecutionTimeMs = 1000)`: Analyzes slow queries from the query store.
- `GetIndexUsage(string? database = null, string? tableName = null)`: Gets index usage statistics for a database or a specific table.
- `FindMissingIndexes(string? database = null, int topCount = 25)`: Finds potentially missing indexes for a database.
- `GetWaitStats(string? database = null, int topCount = 20)`: Gets wait statistics for the database server.

### 3. Schema Discovery Tools

These tools provide schema discovery capabilities.

- `GetEnhancedTables(string? database = null, string? schemaFilter = null, string? namePattern = null)`: Gets an enhanced list of tables with details like row count and description.
- `GetTableStats(string tableName, string? database = null, string? schemaName = null)`: Gets statistics for a table, such as row count and size.
- `SearchSchema(string searchTerm, string? database = null, string? objectType = null)`: Searches the database schema for objects matching a search term.

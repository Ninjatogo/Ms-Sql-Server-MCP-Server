using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using ServerCore.Interfaces;
using ServerCore.Models;
using System.ComponentModel;

namespace ServerWebApi.McpTools;

// JSON Source Generator Context for MCP Tools
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<TableColumn>))]
[JsonSerializable(typeof(TableColumn))]
[JsonSerializable(typeof(DatabaseQueryResult))]
[JsonSerializable(typeof(ColumnSensitivityResult))]
[JsonSerializable(typeof(PiiDetectionResult))]
[JsonSerializable(typeof(DatabaseInfoResult))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(List<Dictionary<string, object?>>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(byte[]))]
internal partial class McpJsonContext : JsonSerializerContext
{
}

// Result types for better serialization and type safety
public record ColumnSensitivityResult(string ColumnName, bool IsSensitive, string Message);
public record PiiDetectionResult(string OriginalValue, bool ContainsPii, object? MaskedValue, bool WasMasked);
public record DatabaseInfoResult(string DatabaseName, int TableCount, List<string> Tables, Dictionary<string, object?>? SizeInfo, string Note);
public record ErrorResult(string Error, string? Database = null);

// MCP Tools for Database Operations
[McpServerToolType]
public static class DatabaseTools
{
    [McpServerTool, Description("Get a list of all databases on the SQL Server instance")]
    public static async Task<string> GetDatabases(IDatabaseService databaseService)
    {
        try
        {
            var databases = await databaseService.GetDatabasesAsync();
            return JsonSerializer.Serialize(databases, McpJsonContext.Default.ListString);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get databases: {ex.Message}");
            return JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Get a list of tables in a specific database")]
    public static async Task<string> GetTables(
        IDatabaseService databaseService,
        [Description("The database name (optional, uses default if not specified)")]
        string? database = null)
    {
        try
        {
            var tables = await databaseService.GetTablesAsync(database);
            return JsonSerializer.Serialize(tables, McpJsonContext.Default.ListString);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get tables: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Get the schema/structure of a specific table with sensitivity indicators")]
    public static async Task<string> GetTableSchema(
        IDatabaseService databaseService,
        [Description("The name of the table")] string tableName,
        [Description("The database name (optional, uses default if not specified)")]
        string? database = null)
    {
        try
        {
            var schema = await databaseService.GetTableSchemaAsync(tableName, database);
            return JsonSerializer.Serialize(schema, McpJsonContext.Default.ListTableColumn);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get table schema for '{tableName}': {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Execute a SQL query and return PII-filtered results")]
    public static async Task<string> ExecuteQuery(
        IDatabaseService databaseService,
        [Description("The SQL query to execute")] string query,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("Maximum number of rows to return (default: 1000)")] int maxRows = 1000)
    {
        try
        {
            var result = await databaseService.ExecuteQueryAsync(query, database, maxRows);
            return JsonSerializer.Serialize(result, McpJsonContext.Default.DatabaseQueryResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to execute query: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Execute a SQL command (INSERT, UPDATE, DELETE, etc.) and return the number of affected rows")]
    public static async Task<string> ExecuteCommand(
        IDatabaseService databaseService,
        [Description("The SQL command to execute")] string command,
        [Description("The database name (optional, uses default if not specified)")] string? database = null)
    {
        try
        {
            var result = await databaseService.ExecuteNonQueryAsync(command, database);
            return JsonSerializer.Serialize(result, McpJsonContext.Default.DatabaseQueryResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to execute command: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Get basic information about a database including table count and size")]
    public static async Task<string> GetDatabaseInfo(
        IDatabaseService databaseService,
        [Description("The database name")] string database)
    {
        try
        {
            var tables = await databaseService.GetTablesAsync(database);
            var tableCount = tables.Count;

            const string sizeQuery = """
                SELECT 
                    DB_NAME() as DatabaseName,
                    SUM(size * 8.0 / 1024) as SizeMB
                FROM sys.database_files
                WHERE type_desc = 'ROWS'
            """;

            var sizeResult = await databaseService.ExecuteQueryAsync(sizeQuery, database, 1);
            var info = new DatabaseInfoResult(
                database,
                tableCount,
                tables,
                sizeResult.Rows.FirstOrDefault(),
                "Query results are automatically filtered for PII protection"
            );

            return JsonSerializer.Serialize(info, McpJsonContext.Default.DatabaseInfoResult);
        }
        catch (Exception ex)
        {
            var errorResult = new ErrorResult($"Failed to get database info: {ex.Message}", database);
            return JsonSerializer.Serialize(errorResult, McpJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Check if a column name is considered sensitive for PII")]
    public static Task<string> CheckColumnSensitivity(
        IPiiFilterService piiFilterService,
        [Description("The column name to check")] string columnName)
    {
        try
        {
            var isSensitive = piiFilterService.IsSensitiveColumn(columnName);
            var result = new ColumnSensitivityResult(
                columnName,
                isSensitive,
                isSensitive ? "This column is flagged as potentially containing PII" : "This column is not flagged as sensitive"
            );
            
            return Task.FromResult(JsonSerializer.Serialize(result, McpJsonContext.Default.ColumnSensitivityResult));
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to check column sensitivity: {ex.Message}");
            return Task.FromResult(JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult));
        }
    }

    [McpServerTool, Description("Test PII detection on a sample value")]
    public static Task<string> TestPiiDetection(
        IPiiFilterService piiFilterService,
        [Description("The value to test for PII")] string testValue,
        [Description("Optional column name context")] string? columnName = null)
    {
        try
        {
            var containsPii = piiFilterService.ContainsPii(testValue);
            var maskedValue = piiFilterService.MaskSensitiveValue(testValue, columnName ?? "test_column");
            
            var result = new PiiDetectionResult(
                testValue,
                containsPii,
                maskedValue,
                !testValue.Equals(maskedValue?.ToString())
            );
            
            return Task.FromResult(JsonSerializer.Serialize(result, McpJsonContext.Default.PiiDetectionResult));
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to test PII detection: {ex.Message}");
            return Task.FromResult(JsonSerializer.Serialize(error, McpJsonContext.Default.ErrorResult));
        }
    }
}
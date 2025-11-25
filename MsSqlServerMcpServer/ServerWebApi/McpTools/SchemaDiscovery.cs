using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using ServerCore.Interfaces;
using ServerCore.Models;

namespace ServerWebApi.McpTools;

// JSON Source Generator Context for Schema Discovery Tools
[JsonSerializable(typeof(List<TableInfo>))]
[JsonSerializable(typeof(List<TableRelationship>))]
[JsonSerializable(typeof(TableStats))]
[JsonSerializable(typeof(List<SchemaSearchResult>))]
[JsonSerializable(typeof(ErrorResult))]
internal partial class SchemaDiscoveryJsonContext : JsonSerializerContext
{
}

[McpServerToolType]
public static class SchemaDiscoveryTools
{
    [McpServerTool, Description("Gets an enhanced list of tables with details like row count and description.")]
    public static async Task<string> GetEnhancedTables(
        IDatabaseService databaseService,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("Optional schema name to filter tables")] string? schemaFilter = null,
        [Description("Optional name pattern to filter tables (e.g., '%customer%')")] string? namePattern = null)
    {
        try
        {
            var result = await databaseService.GetEnhancedTablesAsync(database, schemaFilter, namePattern);
            return JsonSerializer.Serialize(result, SchemaDiscoveryJsonContext.Default.ListTableInfo);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get enhanced tables: {ex.Message}", database);
            return JsonSerializer.Serialize(error, SchemaDiscoveryJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Gets statistics for a table, such as row count and size.")]
    public static async Task<string> GetTableStats(
        IDatabaseService databaseService,
        [Description("The name of the table")] string tableName,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("The schema name of the table (optional)")] string? schemaName = null)
    {
        try
        {
            var result = await databaseService.GetTableStatsAsync(tableName, database, schemaName);
            return JsonSerializer.Serialize(result, SchemaDiscoveryJsonContext.Default.TableStats);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get table stats for '{tableName}': {ex.Message}", database);
            return JsonSerializer.Serialize(error, SchemaDiscoveryJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Searches the database schema for objects matching a search term.")]
    public static async Task<string> SearchSchema(
        IDatabaseService databaseService,
        [Description("The term to search for in the schema (e.g., column name, table name)")] string searchTerm,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("The type of object to search for (e.g., 'TABLE', 'COLUMN', 'INDEX')")] string? objectType = null)
    {
        try
        {
            var result = await databaseService.SearchSchemaAsync(searchTerm, database, objectType);
            return JsonSerializer.Serialize(result, SchemaDiscoveryJsonContext.Default.ListSchemaSearchResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to search schema: {ex.Message}", database);
            return JsonSerializer.Serialize(error, SchemaDiscoveryJsonContext.Default.ErrorResult);
        }
    }
}

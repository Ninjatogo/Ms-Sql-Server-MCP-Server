
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using ServerCore.Interfaces;
using ServerCore.Models;

namespace ServerWebApi.McpTools;

// JSON Source Generator Context for MCP Tools
[JsonSerializable(typeof(QueryPlanResult))]
[JsonSerializable(typeof(QueryValidationResult))]
[JsonSerializable(typeof(QueryCostResult))]
[JsonSerializable(typeof(EnhancedQueryResult))]
[JsonSerializable(typeof(ErrorResult))]
internal partial class McpJsonContextNew : JsonSerializerContext
{
}

[McpServerToolType]
public static class DatabaseToolsNew
{
    [McpServerTool, Description("Generates an estimated execution plan for a SQL query without executing it.")]
    public static async Task<string> ExplainQuery(
        IDatabaseService databaseService,
        [Description("The SQL query to explain")] string query,
        [Description("The database name (optional, uses default if not specified)")] string? database = null)
    {
        try
        {
            var result = await databaseService.ExplainQueryAsync(query, database);
            return JsonSerializer.Serialize(result, McpJsonContextNew.Default.QueryPlanResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to explain query: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContextNew.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Validates the syntax of a SQL query without executing it.")]
    public static async Task<string> ValidateQuery(
        IDatabaseService databaseService,
        [Description("The SQL query to validate")] string query,
        [Description("The database name (optional, uses default if not specified)")] string? database = null)
    {
        try
        {
            var result = await databaseService.ValidateQueryAsync(query, database);
            return JsonSerializer.Serialize(result, McpJsonContextNew.Default.QueryValidationResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to validate query: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContextNew.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Estimates the cost of a SQL query without executing it.")]
    public static async Task<string> EstimateQueryCost(
        IDatabaseService databaseService,
        [Description("The SQL query to estimate cost for")] string query,
        [Description("The database name (optional, uses default if not specified)")] string? database = null)
    {
        try
        {
            var result = await databaseService.EstimateQueryCostAsync(query, database);
            return JsonSerializer.Serialize(result, McpJsonContextNew.Default.QueryCostResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to estimate query cost: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContextNew.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Executes a SQL query and returns the results along with execution statistics.")]
    public static async Task<string> ExecuteQueryWithStats(
        IDatabaseService databaseService,
        [Description("The SQL query to execute")] string query,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("Maximum number of rows to return (default: 1000)")] int maxRows = 1000)
    {
        try
        {
            var result = await databaseService.ExecuteQueryWithStatsAsync(query, database, maxRows);
            return JsonSerializer.Serialize(result, McpJsonContextNew.Default.EnhancedQueryResult);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to execute query with stats: {ex.Message}", database);
            return JsonSerializer.Serialize(error, McpJsonContextNew.Default.ErrorResult);
        }
    }
}

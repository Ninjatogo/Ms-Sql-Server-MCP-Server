using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using ServerCore.Interfaces;
using ServerCore.Models;

namespace ServerWebApi.McpTools;

// JSON Source Generator Context for Performance Analysis Tools
[JsonSerializable(typeof(SlowQueryAnalysis))]
[JsonSerializable(typeof(IndexUsageAnalysis))]
[JsonSerializable(typeof(MissingIndexAnalysis))]
[JsonSerializable(typeof(WaitStatsAnalysis))]
[JsonSerializable(typeof(ErrorResult))]
internal partial class PerformanceAnalysisJsonContext : JsonSerializerContext
{
}

[McpServerToolType]
public static class PerformanceAnalysisTools
{
    [McpServerTool, Description("Analyzes slow queries from the query store.")]
    public static async Task<string> AnalyzeSlowQueries(
        IDatabaseService databaseService,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("The number of top slow queries to return (default: 50)")] int topCount = 50,
        [Description("The minimum execution time in milliseconds to be considered a slow query (default: 1000)")] int minimumExecutionTimeMs = 1000)
    {
        try
        {
            var result = await databaseService.AnalyzeSlowQueriesAsync(database, topCount, minimumExecutionTimeMs);
            return JsonSerializer.Serialize(result, PerformanceAnalysisJsonContext.Default.SlowQueryAnalysis);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to analyze slow queries: {ex.Message}", database);
            return JsonSerializer.Serialize(error, PerformanceAnalysisJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Gets index usage statistics for a database or a specific table.")]
    public static async Task<string> GetIndexUsage(
        IDatabaseService databaseService,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("The name of the table to get index usage for (optional, returns for all tables if null)")] string? tableName = null)
    {
        try
        {
            var result = await databaseService.GetIndexUsageAsync(database, tableName);
            return JsonSerializer.Serialize(result, PerformanceAnalysisJsonContext.Default.IndexUsageAnalysis);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get index usage: {ex.Message}", database);
            return JsonSerializer.Serialize(error, PerformanceAnalysisJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Finds potentially missing indexes for a database.")]
    public static async Task<string> FindMissingIndexes(
        IDatabaseService databaseService,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("The number of top missing indexes to return (default: 25)")] int topCount = 25)
    {
        try
        {
            var result = await databaseService.FindMissingIndexesAsync(database, topCount);
            return JsonSerializer.Serialize(result, PerformanceAnalysisJsonContext.Default.MissingIndexAnalysis);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to find missing indexes: {ex.Message}", database);
            return JsonSerializer.Serialize(error, PerformanceAnalysisJsonContext.Default.ErrorResult);
        }
    }

    [McpServerTool, Description("Gets wait statistics for the database server.")]
    public static async Task<string> GetWaitStats(
        IDatabaseService databaseService,
        [Description("The database name (optional, uses default if not specified)")] string? database = null,
        [Description("The number of top wait stats to return (default: 20)")] int topCount = 20)
    {
        try
        {
            var result = await databaseService.GetWaitStatsAsync(database, topCount);
            return JsonSerializer.Serialize(result, PerformanceAnalysisJsonContext.Default.WaitStatsAnalysis);
        }
        catch (Exception ex)
        {
            var error = new ErrorResult($"Failed to get wait stats: {ex.Message}", database);
            return JsonSerializer.Serialize(error, PerformanceAnalysisJsonContext.Default.ErrorResult);
        }
    }
}

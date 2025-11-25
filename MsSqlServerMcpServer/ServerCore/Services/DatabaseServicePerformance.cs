using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ServerCore.Models;

namespace ServerCore.Services;

/// <summary>
///     Partial class implementing Performance Analysis Tools for the DatabaseService
///     These methods provide comprehensive SQL Server performance analysis capabilities
///     including slow query detection, index usage analysis, missing index suggestions, and wait statistics
/// </summary>
public partial class DatabaseServiceBase
{
    /// <summary>
    ///     Analyze slow queries to identify performance bottlenecks
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="topCount">Number of top slow queries to return</param>
    /// <param name="minimumExecutionTimeMs">Minimum execution time threshold in milliseconds</param>
    /// <returns>Analysis of slow queries with performance metrics</returns>
    public async Task<SlowQueryAnalysis> AnalyzeSlowQueriesAsync(string? database = null, int topCount = 50,
        int minimumExecutionTimeMs = 1000)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var slowQueries = (await connection.QueryAsync<SlowQueryInfo>(
                SqlQueries.AnalyzeSlowQueries,
                new { TopCount = topCount, MinExecutionTime = minimumExecutionTimeMs },
                commandTimeout: 60)).ToList();

            // Calculate summary statistics
            var summary = new QueryPerformanceSummary
            {
                TotalQueries = slowQueries.Count,
                SlowQueries = slowQueries.Count(q => q.AverageElapsedTimeMs >= minimumExecutionTimeMs),
                AverageExecutionTimeMs = slowQueries.Any() ? slowQueries.Average(q => q.AverageElapsedTimeMs) : 0,
                TotalCpuTimeSeconds = slowQueries.Sum(q => q.TotalCpuTimeMs) / 1000,
                TotalLogicalReads = slowQueries.Sum(q => q.TotalLogicalReads),
                TotalPhysicalReads = slowQueries.Sum(q => q.TotalPhysicalReads),
                AnalysisTimestamp = DateTime.UtcNow
            };

            var result = new SlowQueryAnalysis
            {
                Success = true,
                Message =
                    $"Found {slowQueries.Count} slow queries with average execution time >= {minimumExecutionTimeMs}ms",
                SlowQueries = slowQueries,
                Summary = summary
            };

            logger.LogInformation("Analyzed slow queries for database {Database}: found {Count} queries",
                database ?? "default", slowQueries.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing slow queries for database {Database}", database);
            return new SlowQueryAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    ///     Analyze index usage statistics to identify unused or underutilized indexes
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="tableName">Specific table to analyze (optional)</param>
    /// <returns>Comprehensive index usage analysis</returns>
    public async Task<IndexUsageAnalysis> GetIndexUsageAsync(string? database = null, string? tableName = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var parameters = new DynamicParameters();
            var whereClause = "1=1";

            if (!string.IsNullOrEmpty(tableName))
            {
                whereClause = "t.name = @TableName";
                parameters.Add("TableName", tableName);
            }

            var query = string.Format(SqlQueries.GetIndexUsage, $"AND {whereClause}");

            var indexUsageStats = (await connection.QueryAsync<IndexUsageInfo>(
                query, parameters, commandTimeout: 60)).ToList();

            // Calculate summary
            var summary = new IndexUsageSummary
            {
                TotalIndexes = indexUsageStats.Count,
                UnusedIndexes = indexUsageStats.Count(i => i.UsageCategory == "Unused"),
                HeavilyUsedIndexes = indexUsageStats.Count(i => i.UsageCategory == "Heavy"),
                LightlyUsedIndexes = indexUsageStats.Count(i => i.UsageCategory == "Light"),
                TotalIndexSizeKb = indexUsageStats.Sum(i => i.SizeKb),
                UnusedIndexSizeKb = indexUsageStats.Where(i => i.UsageCategory == "Unused").Sum(i => i.SizeKb),
                AnalysisTimestamp = DateTime.UtcNow
            };

            var result = new IndexUsageAnalysis
            {
                Success = true,
                Message =
                    $"Analyzed {indexUsageStats.Count} indexes. Found {summary.UnusedIndexes} unused indexes consuming {summary.UnusedIndexSizeKb:N0} KB",
                IndexUsageStats = indexUsageStats,
                Summary = summary
            };

            logger.LogInformation("Analyzed index usage for database {Database}, table {Table}: {Count} indexes",
                database ?? "default", tableName ?? "all", indexUsageStats.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing index usage for database {Database}, table {Table}", database,
                tableName);
            return new IndexUsageAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    ///     Find missing index suggestions to improve query performance
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="topCount">Number of top suggestions to return</param>
    /// <returns>Analysis of missing index opportunities</returns>
    public async Task<MissingIndexAnalysis> FindMissingIndexesAsync(string? database = null, int topCount = 25)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var missingIndexes = (await connection.QueryAsync<MissingIndexInfo>(
                SqlQueries.FindMissingIndexes, new { TopCount = topCount }, commandTimeout: 60)).ToList();

            // Calculate summary
            var summary = new MissingIndexSummary
            {
                TotalSuggestions = missingIndexes.Count,
                HighPrioritySuggestions = missingIndexes.Count(i => i.Priority == "High"),
                MediumPrioritySuggestions = missingIndexes.Count(i => i.Priority == "Medium"),
                LowPrioritySuggestions = missingIndexes.Count(i => i.Priority == "Low"),
                TotalPotentialImprovement = missingIndexes.Sum(i => i.ImprovementMeasure),
                AnalysisTimestamp = DateTime.UtcNow
            };

            var result = new MissingIndexAnalysis
            {
                Success = true,
                Message =
                    $"Found {missingIndexes.Count} missing index suggestions. {summary.HighPrioritySuggestions} high priority, {summary.MediumPrioritySuggestions} medium priority",
                MissingIndexes = missingIndexes,
                Summary = summary
            };

            logger.LogInformation("Analyzed missing indexes for database {Database}: found {Count} suggestions",
                database ?? "default", missingIndexes.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing missing indexes for database {Database}", database);
            return new MissingIndexAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    ///     Analyze wait statistics to identify performance bottlenecks
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="topCount">Number of top wait types to return</param>
    /// <returns>Analysis of wait statistics and performance bottlenecks</returns>
    public async Task<WaitStatsAnalysis> GetWaitStatsAsync(string? database = null, int topCount = 20)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var waitStats = (await connection.QueryAsync<WaitStatsInfo>(
                SqlQueries.GetWaitStats, new { TopCount = topCount }, commandTimeout: 60)).ToList();

            // Calculate summary and identify main bottlenecks
            var summary = new WaitStatsSummary
            {
                TotalWaitTimeMs = waitStats.Sum(w => w.WaitTimeMs),
                UniqueWaitTypes = waitStats.Count,
                TopWaitType = waitStats.FirstOrDefault()?.WaitType ?? "None",
                TopWaitPercentage = waitStats.FirstOrDefault()?.PercentageOfTotal ?? 0,
                AnalysisTimestamp = DateTime.UtcNow
            };

            // Identify main bottlenecks based on wait categories
            var categoryGroups = waitStats.GroupBy(w => w.WaitCategory)
                .Select(g => new { Category = g.Key, TotalPercentage = g.Sum(w => w.PercentageOfTotal) })
                .Where(g => g.TotalPercentage > 5) // Categories representing more than 5% of waits
                .OrderByDescending(g => g.TotalPercentage)
                .Take(3);

            summary.MainBottlenecks = categoryGroups.Select(g =>
                $"{g.Category} ({g.TotalPercentage:F1}% of total waits)").ToList();

            var result = new WaitStatsAnalysis
            {
                Success = true,
                Message =
                    $"Analyzed {waitStats.Count} wait types. Top wait: {summary.TopWaitType} ({summary.TopWaitPercentage:F1}%)",
                WaitStats = waitStats,
                Summary = summary
            };

            logger.LogInformation("Analyzed wait statistics for database {Database}: {Count} wait types",
                database ?? "default", waitStats.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing wait statistics for database {Database}", database);
            return new WaitStatsAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
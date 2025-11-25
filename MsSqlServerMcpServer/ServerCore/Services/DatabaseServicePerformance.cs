using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ServerCore.Models;

namespace ServerCore.Services;

/// <summary>
/// Partial class implementing Performance Analysis Tools for the DatabaseService
/// These methods provide comprehensive SQL Server performance analysis capabilities
/// including slow query detection, index usage analysis, missing index suggestions, and wait statistics
/// </summary>
public partial class DatabaseServiceBase
{
    /// <summary>
    /// Analyze slow queries to identify performance bottlenecks
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
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            const string query = """
                                 WITH QueryStats AS (
                                     SELECT 
                                         qs.query_hash,
                                         qs.execution_count,
                                         CAST(qs.total_elapsed_time / 1000.0 AS DECIMAL(18,3)) as total_elapsed_time_ms,
                                         CAST(qs.total_elapsed_time / qs.execution_count / 1000.0 AS DECIMAL(18,3)) as avg_elapsed_time_ms,
                                         qs.max_elapsed_time / 1000.0 as max_elapsed_time_ms,
                                         qs.total_worker_time / 1000.0 as total_cpu_time_ms,
                                         qs.total_worker_time / qs.execution_count / 1000.0 as avg_cpu_time_ms,
                                         qs.total_logical_reads,
                                         qs.total_logical_reads / qs.execution_count as avg_logical_reads,
                                         qs.total_physical_reads,
                                         qs.total_physical_reads / qs.execution_count as avg_physical_reads,
                                         qs.last_execution_time,
                                         qs.creation_time,
                                         qs.total_worker_time / 1000.0 as total_worker_time_ms,
                                         DB_NAME() as database_name,
                                         st.text as query_text
                                     FROM sys.dm_exec_query_stats qs
                                     CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
                                     WHERE qs.total_elapsed_time / qs.execution_count / 1000.0 >= @MinExecutionTime
                                 )
                                 SELECT TOP (@TopCount)
                                     CONVERT(VARCHAR(64), query_hash, 1) as QueryHash,
                                     LEFT(REPLACE(REPLACE(REPLACE(query_text, CHAR(13), ' '), CHAR(10), ' '), CHAR(9), ' '), 1000) as QueryText,
                                     execution_count as ExecutionCount,
                                     total_elapsed_time_ms as TotalElapsedTimeMs,
                                     avg_elapsed_time_ms as AverageElapsedTimeMs,
                                     max_elapsed_time_ms as MaxElapsedTimeMs,
                                     total_cpu_time_ms as TotalCpuTimeMs,
                                     avg_cpu_time_ms as AverageCpuTimeMs,
                                     total_logical_reads as TotalLogicalReads,
                                     avg_logical_reads as AverageLogicalReads,
                                     total_physical_reads as TotalPhysicalReads,
                                     avg_physical_reads as AveragePhysicalReads,
                                     last_execution_time as LastExecutionTime,
                                     creation_time as CreationTime,
                                     total_worker_time_ms as TotalWorkerTimeMs,
                                     database_name as DatabaseName
                                 FROM QueryStats
                                 ORDER BY avg_elapsed_time_ms DESC
                                 """;

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@TopCount", SqlDbType.Int).Value = topCount;
            command.Parameters.Add("@MinExecutionTime", SqlDbType.Int).Value = minimumExecutionTimeMs;
            command.CommandTimeout = 60;

            using var reader = await command.ExecuteReaderAsync();

            var slowQueries = new List<SlowQueryInfo>();
            while (await reader.ReadAsync())
            {
                slowQueries.Add(new SlowQueryInfo
                {
                    QueryHash = reader.GetString("QueryHash"),
                    QueryText = reader.GetString("QueryText"),
                    ExecutionCount = GetSafeInt32(reader, "ExecutionCount"),
                    TotalElapsedTimeMs = GetSafeDecimal(reader, "TotalElapsedTimeMs"),
                    AverageElapsedTimeMs = GetSafeDecimal(reader, "AverageElapsedTimeMs"),
                    MaxElapsedTimeMs = GetSafeDecimal(reader, "MaxElapsedTimeMs"),
                    TotalCpuTimeMs = GetSafeDecimal(reader, "TotalCpuTimeMs"),
                    AverageCpuTimeMs = GetSafeDecimal(reader, "AverageCpuTimeMs"),
                    TotalLogicalReads = reader.GetInt64("TotalLogicalReads"),
                    AverageLogicalReads = reader.GetInt64("AverageLogicalReads"),
                    TotalPhysicalReads = reader.GetInt64("TotalPhysicalReads"),
                    AveragePhysicalReads = reader.GetInt64("AveragePhysicalReads"),
                    LastExecutionTime = reader.GetDateTime("LastExecutionTime"),
                    CreationTime = reader.GetDateTime("CreationTime"),
                    TotalWorkerTimeMs = GetSafeDecimal(reader, "TotalWorkerTimeMs"),
                    DatabaseName = reader.IsDBNull("DatabaseName") ? null : reader.GetString("DatabaseName")
                });
            }

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

            _logger.LogInformation("Analyzed slow queries for database {Database}: found {Count} queries",
                database ?? "default", slowQueries.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing slow queries for database {Database}", database);
            return new SlowQueryAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Analyze index usage statistics to identify unused or underutilized indexes
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="tableName">Specific table to analyze (optional)</param>
    /// <returns>Comprehensive index usage analysis</returns>
    public async Task<IndexUsageAnalysis> GetIndexUsageAsync(string? database = null, string? tableName = null)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            var whereClause = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(tableName))
            {
                whereClause.Add("t.name = @TableName");
                parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = tableName });
            }

            var whereFilter = whereClause.Count > 0 ? $"AND {string.Join(" AND ", whereClause)}" : "";

            var query = $"""
                         SELECT 
                             DB_NAME() as DatabaseName,
                             SCHEMA_NAME(t.schema_id) as SchemaName,
                             t.name as TableName,
                             i.name as IndexName,
                             CASE i.type 
                                 WHEN 0 THEN 'Heap'
                                 WHEN 1 THEN 'Clustered'
                                 WHEN 2 THEN 'Nonclustered'
                                 WHEN 3 THEN 'XML'
                                 WHEN 4 THEN 'Spatial'
                                 WHEN 5 THEN 'Clustered Columnstore'
                                 WHEN 6 THEN 'Nonclustered Columnstore'
                                 WHEN 7 THEN 'Nonclustered Hash'
                                 ELSE 'Unknown'
                             END as IndexType,
                             i.is_unique as IsUnique,
                             i.is_primary_key as IsPrimaryKey,
                             ISNULL(ius.user_seeks, 0) as UserSeeks,
                             ISNULL(ius.user_scans, 0) as UserScans,
                             ISNULL(ius.user_lookups, 0) as UserLookups,
                             ISNULL(ius.user_updates, 0) as UserUpdates,
                             ius.last_user_seek as LastUserSeek,
                             ius.last_user_scan as LastUserScan,
                             ius.last_user_lookup as LastUserLookup,
                             ius.last_user_update as LastUserUpdate,
                             CAST(ISNULL(ps.used_page_count * 8, 0) AS BIGINT) as SizeKB,
                             -- Calculate usage score (0-100)
                             CASE 
                                 WHEN ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) + ISNULL(ius.user_lookups, 0) = 0 THEN 0
                                 WHEN ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) + ISNULL(ius.user_lookups, 0) >= 10000 THEN 100
                                 ELSE CAST((ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) + ISNULL(ius.user_lookups, 0)) / 100.0 AS DECIMAL(5,2))
                             END as UsageScore,
                             -- Categorize usage
                             CASE 
                                 WHEN ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) + ISNULL(ius.user_lookups, 0) = 0 THEN 'Unused'
                                 WHEN ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) + ISNULL(ius.user_lookups, 0) < 100 THEN 'Light'
                                 WHEN ISNULL(ius.user_seeks, 0) + ISNULL(ius.user_scans, 0) + ISNULL(ius.user_lookups, 0) < 1000 THEN 'Moderate'
                                 ELSE 'Heavy'
                             END as UsageCategory
                         FROM sys.indexes i
                         INNER JOIN sys.tables t ON i.object_id = t.object_id
                         LEFT JOIN sys.dm_db_index_usage_stats ius ON i.object_id = ius.object_id AND i.index_id = ius.index_id AND ius.database_id = DB_ID()
                         LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                         WHERE i.type >= 0 {whereFilter}
                         ORDER BY UsageScore DESC, t.name, i.name
                         """;

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddRange(parameters.ToArray());
            command.CommandTimeout = 60;

            using var reader = await command.ExecuteReaderAsync();

            var indexUsageStats = new List<IndexUsageInfo>();
            while (await reader.ReadAsync())
            {
                indexUsageStats.Add(new IndexUsageInfo
                {
                    DatabaseName = reader.IsDBNull("DatabaseName") ? "" : reader.GetString("DatabaseName"),
                    SchemaName = reader.GetString("SchemaName"),
                    TableName = reader.GetString("TableName"),
                    IndexName = reader.IsDBNull("IndexName") ? "HEAP" : reader.GetString("IndexName"),
                    IndexType = reader.GetString("IndexType"),
                    IsUnique = reader.GetBoolean("IsUnique"),
                    IsPrimaryKey = reader.GetBoolean("IsPrimaryKey"),
                    UserSeeks = reader.GetInt64("UserSeeks"),
                    UserScans = reader.GetInt64("UserScans"),
                    UserLookups = reader.GetInt64("UserLookups"),
                    UserUpdates = reader.GetInt64("UserUpdates"),
                    LastUserSeek = reader.IsDBNull("LastUserSeek") ? null : reader.GetDateTime("LastUserSeek"),
                    LastUserScan = reader.IsDBNull("LastUserScan") ? null : reader.GetDateTime("LastUserScan"),
                    LastUserLookup = reader.IsDBNull("LastUserLookup") ? null : reader.GetDateTime("LastUserLookup"),
                    LastUserUpdate = reader.IsDBNull("LastUserUpdate") ? null : reader.GetDateTime("LastUserUpdate"),
                    SizeKb = reader.GetInt64("SizeKB"),
                    UsageScore = GetSafeDecimal(reader, "UsageScore"),
                    UsageCategory = reader.GetString("UsageCategory")
                });
            }

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

            _logger.LogInformation("Analyzed index usage for database {Database}, table {Table}: {Count} indexes",
                database ?? "default", tableName ?? "all", indexUsageStats.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing index usage for database {Database}, table {Table}", database,
                tableName);
            return new IndexUsageAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Find missing index suggestions to improve query performance
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="topCount">Number of top suggestions to return</param>
    /// <returns>Analysis of missing index opportunities</returns>
    public async Task<MissingIndexAnalysis> FindMissingIndexesAsync(string? database = null, int topCount = 25)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            const string query = """
                                 WITH MissingIndexes AS (
                                     SELECT 
                                         DB_NAME(mid.database_id) as DatabaseName,
                                         OBJECT_SCHEMA_NAME(mid.object_id, mid.database_id) as SchemaName,
                                         OBJECT_NAME(mid.object_id, mid.database_id) as TableName,
                                         ISNULL(mid.equality_columns, '') as EqualityColumns,
                                         ISNULL(mid.inequality_columns, '') as InequalityColumns,
                                         ISNULL(mid.included_columns, '') as IncludedColumns,
                                         migs.user_seeks,
                                         migs.user_scans,
                                         migs.avg_total_user_cost,
                                         migs.avg_user_impact,
                                         migs.last_user_seek,
                                         migs.last_user_scan,
                                         -- Calculate improvement measure
                                         CAST((migs.avg_total_user_cost * migs.avg_user_impact) * (migs.user_seeks + migs.user_scans) AS DECIMAL(18,2)) as improvement_measure,
                                     FROM sys.dm_db_missing_index_details mid
                                     INNER JOIN sys.dm_db_missing_index_groups mig ON mid.index_handle = mig.index_handle
                                     INNER JOIN sys.dm_db_missing_index_group_stats migs ON mig.index_group_handle = migs.group_handle
                                     WHERE mid.database_id = DB_ID()
                                         AND OBJECT_NAME(mid.object_id, mid.database_id) IS NOT NULL
                                 )
                                 SELECT TOP (@TopCount)
                                     DatabaseName,
                                     SchemaName,
                                     TableName,
                                     EqualityColumns,
                                     InequalityColumns,
                                     IncludedColumns,
                                     improvement_measure as ImprovementMeasure,
                                     user_seeks as UserSeeks,
                                     user_scans as UserScans,
                                     CAST(avg_total_user_cost AS DECIMAL(18,6)) as AvgTotalUserCost,
                                     CAST(avg_user_impact AS DECIMAL(18,6)) as AvgUserImpact,
                                     last_user_seek as LastUserSeek,
                                     last_user_scan as LastUserScan,
                                     -- Generate suggested index name
                                     'IX_' + TableName + '_' + 
                                     CASE 
                                         WHEN LEN(EqualityColumns) > 0 THEN REPLACE(REPLACE(EqualityColumns, '[', ''), ']', '')
                                         WHEN LEN(InequalityColumns) > 0 THEN REPLACE(REPLACE(InequalityColumns, '[', ''), ']', '')
                                         ELSE 'Missing'
                                     END as SuggestedIndexName,
                                     -- Generate CREATE INDEX statement
                                     'CREATE NONCLUSTERED INDEX [IX_' + TableName + '_' + 
                                     CASE 
                                         WHEN LEN(EqualityColumns) > 0 THEN REPLACE(REPLACE(EqualityColumns, '[', ''), ']', '')
                                         WHEN LEN(InequalityColumns) > 0 THEN REPLACE(REPLACE(InequalityColumns, '[', ''), ']', '')
                                         ELSE 'Missing'
                                     END + '] ON [' + SchemaName + '].[' + TableName + '] (' +
                                     CASE 
                                         WHEN LEN(EqualityColumns) > 0 AND LEN(InequalityColumns) > 0 THEN EqualityColumns + ', ' + InequalityColumns
                                         WHEN LEN(EqualityColumns) > 0 THEN EqualityColumns
                                         WHEN LEN(InequalityColumns) > 0 THEN InequalityColumns
                                         ELSE '[Unknown]'
                                     END + ')' +
                                     CASE 
                                         WHEN LEN(IncludedColumns) > 0 THEN ' INCLUDE (' + IncludedColumns + ')'
                                         ELSE ''
                                     END as CreateIndexStatement,
                                     -- Determine priority
                                     CASE 
                                         WHEN improvement_measure > 100000 THEN 'High'
                                         WHEN improvement_measure > 10000 THEN 'Medium'
                                         ELSE 'Low'
                                     END as Priority
                                 FROM MissingIndexes
                                 ORDER BY improvement_measure DESC
                                 """;

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@TopCount", SqlDbType.Int).Value = topCount;
            command.CommandTimeout = 60;

            using var reader = await command.ExecuteReaderAsync();

            var missingIndexes = new List<MissingIndexInfo>();
            while (await reader.ReadAsync())
            {
                missingIndexes.Add(new MissingIndexInfo
                {
                    DatabaseName = reader.GetString("DatabaseName"),
                    SchemaName = reader.GetString("SchemaName"),
                    TableName = reader.GetString("TableName"),
                    EqualityColumns = reader.GetString("EqualityColumns"),
                    InequalityColumns = reader.GetString("InequalityColumns"),
                    IncludedColumns = reader.GetString("IncludedColumns"),
                    ImprovementMeasure = GetSafeDecimal(reader, "ImprovementMeasure"),
                    UserSeeks = reader.GetInt64("UserSeeks"),
                    UserScans = reader.GetInt64("UserScans"),
                    AvgTotalUserCost = GetSafeDecimal(reader, "AvgTotalUserCost"),
                    AvgUserImpact = GetSafeDecimal(reader, "AvgUserImpact"),
                    LastUserSeek = reader.GetDateTime("LastUserSeek"),
                    LastUserScan = reader.GetDateTime("LastUserScan"),
                    SuggestedIndexName = reader.GetString("SuggestedIndexName"),
                    CreateIndexStatement = reader.GetString("CreateIndexStatement"),
                    Priority = reader.GetString("Priority")
                });
            }

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

            _logger.LogInformation("Analyzed missing indexes for database {Database}: found {Count} suggestions",
                database ?? "default", missingIndexes.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing missing indexes for database {Database}", database);
            return new MissingIndexAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Analyze wait statistics to identify performance bottlenecks
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="topCount">Number of top wait types to return</param>
    /// <returns>Analysis of wait statistics and performance bottlenecks</returns>
    public async Task<WaitStatsAnalysis> GetWaitStatsAsync(string? database = null, int topCount = 20)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            const string query = """
                                 WITH WaitStats AS (
                                     SELECT 
                                         wait_type,
                                         waiting_tasks_count,
                                         wait_time_ms,
                                         max_wait_time_ms,
                                         signal_wait_time_ms,
                                         wait_time_ms - signal_wait_time_ms as resource_wait_time_ms,
                                         CAST(100.0 * wait_time_ms / SUM(wait_time_ms) OVER() AS DECIMAL(5,2)) as percentage_of_total,
                                         CASE 
                                             WHEN waiting_tasks_count > 0 THEN wait_time_ms / waiting_tasks_count 
                                             ELSE 0 
                                         END as avg_wait_time_ms
                                     FROM sys.dm_os_wait_stats
                                     WHERE wait_type NOT IN (
                                         -- Filter out common benign wait types
                                         'CLR_SEMAPHORE', 'LAZYWRITER_SLEEP', 'RESOURCE_QUEUE', 'SLEEP_TASK',
                                         'SLEEP_SYSTEMTASK', 'SQLTRACE_BUFFER_FLUSH', 'WAITFOR', 'LOGMGR_QUEUE',
                                         'CHECKPOINT_QUEUE', 'REQUEST_FOR_DEADLOCK_SEARCH', 'XE_TIMER_EVENT',
                                         'BROKER_TO_FLUSH', 'BROKER_TASK_STOP', 'CLR_MANUAL_EVENT', 
                                         'CLR_AUTO_EVENT', 'DISPATCHER_QUEUE_SEMAPHORE', 'FT_IFTS_SCHEDULER_IDLE_WAIT',
                                         'XE_DISPATCHER_WAIT', 'XE_DISPATCHER_JOIN', 'SQLTRACE_INCREMENTAL_FLUSH_SLEEP'
                                     )
                                     AND wait_time_ms > 0
                                 ),
                                 WaitCategories AS (
                                     SELECT 
                                         *,
                                         CASE 
                                             WHEN wait_type LIKE 'LCK%' THEN 'Locking'
                                             WHEN wait_type LIKE 'PAGEIO%' OR wait_type LIKE 'WRITELOG%' OR wait_type LIKE 'IO_%' THEN 'I/O'
                                             WHEN wait_type LIKE 'PAGELATCH%' OR wait_type LIKE 'PAGEIOLATCH%' THEN 'Latching'
                                             WHEN wait_type LIKE 'RESOURCE_%' OR wait_type LIKE 'CMEMTHREAD%' THEN 'Memory'
                                             WHEN wait_type LIKE 'CXPACKET%' OR wait_type LIKE 'EXCHANGE%' THEN 'Parallelism'
                                             WHEN wait_type LIKE 'ASYNC_%' OR wait_type LIKE 'NETWORK_%' THEN 'Network'
                                             WHEN wait_type LIKE 'SOS_%' OR wait_type LIKE 'THREADPOOL%' THEN 'CPU'
                                             ELSE 'Other'
                                         END as wait_category,
                                         CASE 
                                             WHEN wait_type = 'CXPACKET' THEN 'Parallelism overhead - consider adjusting MAXDOP or Cost Threshold'
                                             WHEN wait_type = 'ASYNC_NETWORK_IO' THEN 'Client not consuming data fast enough - check network or client processing'
                                             WHEN wait_type = 'PAGEIOLATCH_SH' THEN 'Data page reads from disk - consider more memory or faster storage'
                                             WHEN wait_type = 'PAGEIOLATCH_EX' THEN 'Data page writes to disk - check storage performance'
                                             WHEN wait_type = 'WRITELOG' THEN 'Log file writes - consider faster storage for log files'
                                             WHEN wait_type = 'LCK_M_S' THEN 'Shared lock waits - potential blocking or long transactions'
                                             WHEN wait_type = 'LCK_M_X' THEN 'Exclusive lock waits - potential blocking or deadlocks'
                                             WHEN wait_type = 'RESOURCE_SEMAPHORE' THEN 'Memory pressure - queries waiting for memory grants'
                                             WHEN wait_type = 'THREADPOOL' THEN 'Thread pool exhaustion - too many concurrent requests'
                                             ELSE 'Review wait type documentation for specific recommendations'
                                         END as recommendation
                                     FROM WaitStats
                                 )
                                 SELECT TOP (@TopCount)
                                     wait_type as WaitType,
                                     waiting_tasks_count as WaitingTasksCount,
                                     CAST(wait_time_ms AS DECIMAL(18,3)) as WaitTimeMs,
                                     max_wait_time_ms as MaxWaitTimeMs,
                                     signal_wait_time_ms as SignalWaitTimeMs,
                                     percentage_of_total as PercentageOfTotal,
                                     avg_wait_time_ms as AverageWaitTimeMs,
                                     wait_category as WaitCategory,
                                     CASE 
                                         WHEN wait_type = 'CXPACKET' THEN 'Parallelism coordination waits'
                                         WHEN wait_type = 'ASYNC_NETWORK_IO' THEN 'Network I/O waits'
                                         WHEN wait_type LIKE 'PAGEIOLATCH%' THEN 'Data page I/O waits'
                                         WHEN wait_type = 'WRITELOG' THEN 'Transaction log write waits'
                                         WHEN wait_type LIKE 'LCK_%' THEN 'Lock acquisition waits'
                                         WHEN wait_type = 'RESOURCE_SEMAPHORE' THEN 'Memory grant waits'
                                         WHEN wait_type = 'THREADPOOL' THEN 'Worker thread waits'
                                         ELSE 'Various SQL Server operations'
                                     END as Description,
                                     recommendation as Recommendation
                                 FROM WaitCategories
                                 ORDER BY percentage_of_total DESC
                                 """;

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@TopCount", SqlDbType.Int).Value = topCount;
            command.CommandTimeout = 60;

            using var reader = await command.ExecuteReaderAsync();

            var waitStats = new List<WaitStatsInfo>();
            while (await reader.ReadAsync())
            {
                waitStats.Add(new WaitStatsInfo
                {
                    WaitType = reader.GetString("WaitType"),
                    WaitingTasksCount = reader.GetInt64("WaitingTasksCount"),
                    WaitTimeMs = GetSafeDecimal(reader, "WaitTimeMs"),
                    MaxWaitTimeMs = GetSafeDecimal(reader, "MaxWaitTimeMs"),
                    SignalWaitTimeMs = GetSafeDecimal(reader, "SignalWaitTimeMs"),
                    PercentageOfTotal = GetSafeDecimal(reader, "PercentageOfTotal"),
                    AverageWaitTimeMs = GetSafeDecimal(reader, "AverageWaitTimeMs"),
                    WaitCategory = reader.GetString("WaitCategory"),
                    Description = reader.GetString("Description"),
                    Recommendation = reader.GetString("Recommendation")
                });
            }

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

            _logger.LogInformation("Analyzed wait statistics for database {Database}: {Count} wait types",
                database ?? "default", waitStats.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing wait statistics for database {Database}", database);
            return new WaitStatsAnalysis
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
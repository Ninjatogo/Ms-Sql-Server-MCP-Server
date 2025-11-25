namespace ServerCore.Services;

internal static class SqlQueries
{
    public const string GetDatabases = """
                                      SELECT name
                                      FROM sys.databases
                                      WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')
                                      ORDER BY name
                                  """;

    public const string GetTables = """
                                    SELECT TABLE_NAME
                                    FROM INFORMATION_SCHEMA.TABLES
                                    WHERE TABLE_TYPE = 'BASE TABLE'
                                    ORDER BY TABLE_NAME
                                """;

    public const string GetTableSchema = """
                                         SELECT
                                             COLUMN_NAME,
                                             DATA_TYPE,
                                             IS_NULLABLE,
                                             CHARACTER_MAXIMUM_LENGTH,
                                             NUMERIC_PRECISION,
                                             NUMERIC_SCALE,
                                             COLUMN_DEFAULT
                                         FROM INFORMATION_SCHEMA.COLUMNS
                                         WHERE TABLE_NAME = @TableName
                                         ORDER BY ORDINAL_POSITION
                                     """;

    public const string AnalyzeSlowQueries = """
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

    public const string GetIndexUsage = """
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
                                        WHERE i.type >= 0 {0}
                                        ORDER BY UsageScore DESC, t.name, i.name
                                        """;

    public const string FindMissingIndexes = """
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

    public const string GetWaitStats = """
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

    public const string GetEnhancedTables = """
                                            SELECT
                                                t.TABLE_NAME,
                                                t.TABLE_SCHEMA,
                                                t.TABLE_TYPE,
                                                o.create_date,
                                                o.modify_date,
                                                ISNULL(p.row_count, 0) as row_count,
                                                CAST(ISNULL(a.total_pages * 8, 0) / 1024.0 AS DECIMAL(18,2)) as data_size_mb,
                                                CAST(ISNULL(a.used_pages * 8, 0) / 1024.0 AS DECIMAL(18,2)) as index_size_mb,
                                                ep.value as table_description,
                                                CASE WHEN EXISTS(
                                                    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                                    WHERE tc.TABLE_NAME = t.TABLE_NAME
                                                    AND tc.TABLE_SCHEMA = t.TABLE_SCHEMA
                                                    AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                                                ) THEN 1 ELSE 0 END as has_primary_key,
                                                CASE WHEN EXISTS(
                                                    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                                    WHERE tc.TABLE_NAME = t.TABLE_NAME
                                                    AND tc.TABLE_SCHEMA = t.TABLE_SCHEMA
                                                    AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                                                ) THEN 1 ELSE 0 END as has_foreign_keys,
                                                CASE WHEN EXISTS(
                                                    SELECT 1 FROM sys.indexes i
                                                    INNER JOIN sys.objects so ON i.object_id = so.object_id
                                                    WHERE so.name = t.TABLE_NAME
                                                    AND so.schema_id = SCHEMA_ID(t.TABLE_SCHEMA)
                                                    AND i.type > 0
                                                ) THEN 1 ELSE 0 END as has_indexes,
                                                (
                                                    SELECT COUNT(*)
                                                    FROM INFORMATION_SCHEMA.COLUMNS c
                                                    WHERE c.TABLE_NAME = t.TABLE_NAME
                                                    AND c.TABLE_SCHEMA = t.TABLE_SCHEMA
                                                ) as column_count
                                            FROM INFORMATION_SCHEMA.TABLES t
                                            LEFT JOIN sys.objects o ON o.name = t.TABLE_NAME AND o.schema_id = SCHEMA_ID(t.TABLE_SCHEMA)
                                            LEFT JOIN (
                                                SELECT object_id, SUM(rows) as row_count
                                                FROM sys.partitions
                                                WHERE index_id IN (0, 1)
                                                GROUP BY object_id
                                            ) p ON p.object_id = o.object_id
                                            LEFT JOIN (
                                                SELECT
                                                    pa.object_id,
                                                    SUM(au.total_pages) as total_pages,
                                                    SUM(au.used_pages) as used_pages
                                                FROM sys.allocation_units au
                                                INNER JOIN sys.partitions pa ON au.container_id = pa.partition_id
                                                GROUP BY pa.object_id
                                            ) a ON a.object_id = o.object_id
                                            LEFT JOIN sys.extended_properties ep ON ep.major_id = o.object_id
                                                AND ep.minor_id = 0
                                                AND ep.name = 'MS_Description'
                                            WHERE {0}
                                            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME
                                            """;

    public const string GetTableStats = """
                                        SELECT
                                            t.TABLE_NAME as TableName,
                                            t.TABLE_SCHEMA as SchemaName,
                                            CAST(ISNULL(p.rows, 0) AS BIGINT) as [RowCount],
                                            CAST(ISNULL(au.total_pages * 8, 0) AS BIGINT) as ReservedSpaceKB,
                                            CAST(ISNULL(au.data_pages * 8, 0) AS BIGINT) as DataSpaceKB,
                                            CAST(ISNULL((au.used_pages - au.data_pages) * 8, 0) AS BIGINT) as IndexSpaceKB,
                                            CAST(ISNULL((au.total_pages - au.used_pages) * 8, 0) AS BIGINT) as UnusedSpaceKB,
                                            us.last_user_seek as LastUserAccess,
                                            us.last_user_update as LastUserUpdate,
                                            STATS_DATE(o.object_id, s.stats_id) as LastStatsUpdate,
                                            (SELECT COUNT(*) FROM sys.indexes i WHERE i.object_id = o.object_id AND i.type > 0) as IndexCount,
                                            CASE WHEN EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id = o.object_id AND i.type = 1) THEN 1 ELSE 0 END as HasClusteredIndex
                                        FROM INFORMATION_SCHEMA.TABLES t
                                        INNER JOIN sys.objects o ON o.name = t.TABLE_NAME AND o.schema_id = SCHEMA_ID(t.TABLE_SCHEMA)
                                        LEFT JOIN sys.dm_db_partition_stats p ON p.object_id = o.object_id AND p.index_id < 2
                                        LEFT JOIN (
                                            SELECT
                                                object_id,
                                                SUM(total_pages) as total_pages,
                                                SUM(used_pages) as used_pages,
                                                SUM(data_pages) as data_pages
                                            FROM sys.allocation_units au
                                            INNER JOIN sys.partitions pa ON au.container_id = pa.partition_id
                                            GROUP BY object_id
                                        ) au ON au.object_id = o.object_id
                                        LEFT JOIN sys.dm_db_index_usage_stats us ON us.object_id = o.object_id AND us.database_id = DB_ID()
                                        LEFT JOIN sys.stats s ON s.object_id = o.object_id
                                        WHERE t.TABLE_NAME = @TableName AND t.TABLE_SCHEMA = @SchemaName
                                        """;

    public const string GetTableColumnNames = """
                                              SELECT COLUMN_NAME
                                              FROM INFORMATION_SCHEMA.COLUMNS
                                              WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @SchemaName
                                              ORDER BY ORDINAL_POSITION
                                              """;

    public const string SearchTables = """
                                       SELECT
                                           'Table' as ObjectType,
                                           TABLE_NAME as ObjectName,
                                           TABLE_SCHEMA as SchemaName,
                                           NULL as TableName,
                                           NULL as DataType,
                                           ep.value as Description,
                                           CASE
                                               WHEN LOWER(TABLE_NAME) = LOWER(@SearchTerm) THEN 100
                                               WHEN LOWER(TABLE_NAME) LIKE LOWER(@SearchTermStart) THEN 90
                                               WHEN LOWER(TABLE_NAME) LIKE LOWER(@SearchTermContains) THEN 70
                                               ELSE 50
                                           END as MatchScore
                                       FROM INFORMATION_SCHEMA.TABLES t
                                       LEFT JOIN sys.objects o ON o.name = t.TABLE_NAME AND o.schema_id = SCHEMA_ID(t.TABLE_SCHEMA)
                                       LEFT JOIN sys.extended_properties ep ON ep.major_id = o.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
                                       WHERE TABLE_TYPE = 'BASE TABLE'
                                           AND (LOWER(TABLE_NAME) LIKE LOWER(@SearchTermContains)
                                                OR LOWER(ISNULL(ep.value, '')) LIKE LOWER(@SearchTermContains))
                                       """;

    public const string SearchColumns = """
                                        SELECT
                                            'Column' as ObjectType,
                                            COLUMN_NAME as ObjectName,
                                            TABLE_SCHEMA as SchemaName,
                                            TABLE_NAME as TableName,
                                            DATA_TYPE as DataType,
                                            NULL as Description,
                                            CASE
                                                WHEN LOWER(COLUMN_NAME) = LOWER(@SearchTerm) THEN 100
                                                WHEN LOWER(COLUMN_NAME) LIKE LOWER(@SearchTermStart) THEN 90
                                                WHEN LOWER(COLUMN_NAME) LIKE LOWER(@SearchTermContains) THEN 70
                                                ELSE 50
                                            END as MatchScore
                                        FROM INFORMATION_SCHEMA.COLUMNS
                                        WHERE LOWER(COLUMN_NAME) LIKE LOWER(@SearchTermContains)
                                        """;

    public const string SearchIndexes = """
                                        SELECT
                                            'Index' as ObjectType,
                                            i.name as ObjectName,
                                            SCHEMA_NAME(t.schema_id) as SchemaName,
                                            t.name as TableName,
                                            NULL as DataType,
                                            NULL as Description,
                                            CASE
                                                WHEN LOWER(i.name) = LOWER(@SearchTerm) THEN 100
                                                WHEN LOWER(i.name) LIKE LOWER(@SearchTermStart) THEN 90
                                                WHEN LOWER(i.name) LIKE LOWER(@SearchTermContains) THEN 70
                                                ELSE 50
                                            END as MatchScore
                                        FROM sys.indexes i
                                        INNER JOIN sys.tables t ON i.object_id = t.object_id
                                        WHERE i.type > 0
                                            AND LOWER(i.name) LIKE LOWER(@SearchTermContains)
                                        """;
}
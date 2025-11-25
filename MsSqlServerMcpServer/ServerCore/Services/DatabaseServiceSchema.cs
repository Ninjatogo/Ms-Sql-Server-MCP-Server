using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ServerCore.Models;

namespace ServerCore.Services;

/// <summary>
/// Partial class containing Schema Discovery & Documentation functionality
/// </summary>
public partial class DatabaseServiceBase
{
    /// <summary>
    /// Get enhanced table information with metadata, statistics, and relationships
    /// </summary>
    /// <param name="database">Target database (optional)</param>
    /// <param name="schemaFilter">Filter by schema name (optional)</param>
    /// <param name="namePattern">Filter by table name pattern (optional)</param>
    /// <returns>List of enhanced table information</returns>
    public async Task<List<TableInfo>> GetEnhancedTablesAsync(string? database = null, string? schemaFilter = null,
        string? namePattern = null)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            var whereClause = new List<string> { "t.TABLE_TYPE = 'BASE TABLE'" };
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(schemaFilter))
            {
                whereClause.Add("t.TABLE_SCHEMA = @SchemaFilter");
                parameters.Add(new SqlParameter("@SchemaFilter", SqlDbType.NVarChar) { Value = schemaFilter });
            }

            if (!string.IsNullOrEmpty(namePattern))
            {
                whereClause.Add("t.TABLE_NAME LIKE @NamePattern");
                parameters.Add(new SqlParameter("@NamePattern", SqlDbType.NVarChar) { Value = $"%{namePattern}%" });
            }

            // Query updated to support SQL Server 2012 (row count via sys.partitions)
            var query = $@"
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
            WHERE {string.Join(" AND ", whereClause)}
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            // _logger.LogInformation("Enhanced table query called: {tableQuery}", query);

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddRange(parameters.ToArray());

            var tableDataList = new List<(
                string tableName,
                string schemaName,
                string tableType,
                DateTime? createdDate,
                DateTime? modifiedDate,
                long? rowCount,
                long? dataSizeMb,
                long? indexSizeMb,
                string? description,
                bool hasPrimaryKey,
                bool hasForeignKeys,
                bool hasIndexes,
                long columnCount
                )>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tableDataList.Add((
                        reader.GetString("TABLE_NAME"),
                        reader.GetString("TABLE_SCHEMA"),
                        reader.GetString("TABLE_TYPE"),
                        reader.IsDBNull("create_date") ? null : reader.GetDateTime("create_date"),
                        reader.IsDBNull("modify_date") ? null : reader.GetDateTime("modify_date"),
                        reader.IsDBNull("row_count") ? null : reader.GetInt64("row_count"),
                        reader.IsDBNull("data_size_mb") ? null : (long)GetSafeDecimal(reader, "data_size_mb"),
                        reader.IsDBNull("index_size_mb") ? null : (long)GetSafeDecimal(reader, "index_size_mb"),
                        reader.IsDBNull("table_description") ? null : reader.GetString("table_description"),
                        GetSafeInt32(reader, "has_primary_key") == 1,
                        GetSafeInt32(reader, "has_foreign_keys") == 1,
                        GetSafeInt32(reader, "has_indexes") == 1,
                        GetSafeInt32(reader, "column_count")
                    ));
                }
            }

            var tables = new List<TableInfo>();
            foreach (var data in tableDataList)
            {
                var tableInfo = new TableInfo
                {
                    TableName = data.tableName,
                    SchemaName = data.schemaName,
                    TableType = data.tableType,
                    CreatedDate = data.createdDate,
                    ModifiedDate = data.modifiedDate,
                    RowCount = data.rowCount,
                    DataSizeMb = data.dataSizeMb,
                    IndexSizeMb = data.indexSizeMb,
                    Description = data.description,
                    HasPrimaryKey = data.hasPrimaryKey,
                    HasForeignKeys = data.hasForeignKeys,
                    HasIndexes = data.hasIndexes,
                    ColumnCount = data.columnCount
                };

                tableInfo.Columns =
                    await GetTableColumnNamesAsync(connection, tableInfo.TableName, tableInfo.SchemaName);
                tables.Add(tableInfo);
            }

            _logger.LogInformation("Retrieved {Count} enhanced tables from database {Database}", tables.Count,
                database ?? "default");
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving enhanced tables from database {Database}", database);
            throw;
        }
    }


    /// <summary>
    /// Get detailed table statistics including size, row count, and access patterns
    /// </summary>
    /// <param name="tableName">Table name</param>
    /// <param name="database">Target database (optional)</param>
    /// <param name="schemaName">Schema name (optional, defaults to dbo)</param>
    /// <returns>Table statistics</returns>
    public async Task<TableStats> GetTableStatsAsync(string tableName, string? database = null, string? schemaName = null)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            schemaName ??= "dbo";

            const string query = """
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

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@TableName", SqlDbType.NVarChar).Value = tableName;
            command.Parameters.Add("@SchemaName", SqlDbType.NVarChar).Value = schemaName;
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var stats = new TableStats
                {
                    TableName = reader.GetString("TableName"),
                    SchemaName = reader.GetString("SchemaName"),
                    RowCount = reader.IsDBNull("RowCount") ? 0 : reader.GetInt64("RowCount"),
                    ReservedSpaceKb = reader.IsDBNull("ReservedSpaceKB") ? 0 : reader.GetInt64("ReservedSpaceKB"),
                    DataSpaceKb = reader.IsDBNull("DataSpaceKB") ? 0 : reader.GetInt64("DataSpaceKB"),
                    IndexSpaceKb = reader.IsDBNull("IndexSpaceKB") ? 0 : reader.GetInt64("IndexSpaceKB"),
                    UnusedSpaceKb = reader.IsDBNull("UnusedSpaceKB") ? 0 : reader.GetInt64("UnusedSpaceKB"),
                    LastUserAccess = reader.IsDBNull("LastUserAccess") ? null : reader.GetDateTime("LastUserAccess"),
                    LastUserUpdate = reader.IsDBNull("LastUserUpdate") ? null : reader.GetDateTime("LastUserUpdate"),
                    LastStatsUpdate = reader.IsDBNull("LastStatsUpdate") ? null : reader.GetDateTime("LastStatsUpdate"),
                    IndexCount = reader.IsDBNull("IndexCount") ? 0 : GetSafeInt32(reader, "IndexCount"),
                    HasClusteredIndex = GetSafeInt32(reader, "HasClusteredIndex") == 1
                };

                _logger.LogInformation("Retrieved table statistics for {SchemaName}.{TableName} from database {Database}", 
                    schemaName, tableName, database ?? "default");
                return stats;
            }

            throw new InvalidOperationException($"Table {schemaName}.{tableName} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving table statistics for {SchemaName}.{TableName} from database {Database}", 
                schemaName, tableName, database);
            throw;
        }
    }

    /// <summary>
    /// Search database schema objects (tables, columns, indexes) by keyword
    /// </summary>
    /// <param name="searchTerm">Search term</param>
    /// <param name="database">Target database (optional)</param>
    /// <param name="objectType">Object type filter (Table, Column, Index) (optional)</param>
    /// <returns>List of matching schema objects</returns>
    public async Task<List<SchemaSearchResult>> SearchSchemaAsync(string searchTerm, string? database = null, string? objectType = null)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            var results = new List<SchemaSearchResult>();
            
            // Search tables
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Table", StringComparison.OrdinalIgnoreCase))
            {
                results.AddRange(await SearchTablesAsync(connection, searchTerm));
            }

            // Search columns
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Column", StringComparison.OrdinalIgnoreCase))
            {
                results.AddRange(await SearchColumnsAsync(connection, searchTerm));
            }

            // Search indexes
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Index", StringComparison.OrdinalIgnoreCase))
            {
                results.AddRange(await SearchIndexesAsync(connection, searchTerm));
            }

            // Sort by match score descending
            results = results.OrderByDescending(r => r.MatchScore).ToList();

            _logger.LogInformation("Schema search for '{SearchTerm}' returned {Count} results from database {Database}", 
                searchTerm, results.Count, database ?? "default");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching schema for term '{SearchTerm}' in database {Database}", searchTerm, database);
            throw;
        }
    }

    /// <summary>
    /// Get column names for a specific table
    /// </summary>
    private async Task<List<string>> GetTableColumnNamesAsync(SqlConnection connection, string tableName, string schemaName)
    {
        const string query = """
            SELECT COLUMN_NAME 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @SchemaName
            ORDER BY ORDINAL_POSITION
            """;

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@TableName", SqlDbType.NVarChar).Value = tableName;
        command.Parameters.Add("@SchemaName", SqlDbType.NVarChar).Value = schemaName;
        using var reader = await command.ExecuteReaderAsync();

        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    /// <summary>
    /// Search for tables matching the search term
    /// </summary>
    private async Task<List<SchemaSearchResult>> SearchTablesAsync(SqlConnection connection, string searchTerm)
    {
        const string query = """
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

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@SearchTerm", SqlDbType.NVarChar).Value = searchTerm;
        command.Parameters.Add("@SearchTermStart", SqlDbType.NVarChar).Value = $"{searchTerm}%";
        command.Parameters.Add("@SearchTermContains", SqlDbType.NVarChar).Value = $"%{searchTerm}%";
        using var reader = await command.ExecuteReaderAsync();

        var results = new List<SchemaSearchResult>();
        while (await reader.ReadAsync())
        {
            results.Add(new SchemaSearchResult
            {
                ObjectType = reader.GetString("ObjectType"),
                ObjectName = reader.GetString("ObjectName"),
                SchemaName = reader.GetString("SchemaName"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                MatchScore = GetSafeDecimal(reader, "MatchScore")
            });
        }

        return results;
    }

    /// <summary>
    /// Search for columns matching the search term
    /// </summary>
    private async Task<List<SchemaSearchResult>> SearchColumnsAsync(SqlConnection connection, string searchTerm)
    {
        const string query = """
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

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@SearchTerm", SqlDbType.NVarChar).Value = searchTerm;
        command.Parameters.Add("@SearchTermStart", SqlDbType.NVarChar).Value = $"{searchTerm}%";
        command.Parameters.Add("@SearchTermContains", SqlDbType.NVarChar).Value = $"%{searchTerm}%";
        using var reader = await command.ExecuteReaderAsync();

        var results = new List<SchemaSearchResult>();
        while (await reader.ReadAsync())
        {
            results.Add(new SchemaSearchResult
            {
                ObjectType = reader.GetString("ObjectType"),
                ObjectName = reader.GetString("ObjectName"),
                SchemaName = reader.GetString("SchemaName"),
                TableName = reader.GetString("TableName"),
                DataType = reader.GetString("DataType"),
                MatchScore = GetSafeDecimal(reader, "MatchScore")
            });
        }

        return results;
    }

    /// <summary>
    /// Search for indexes matching the search term
    /// </summary>
    private async Task<List<SchemaSearchResult>> SearchIndexesAsync(SqlConnection connection, string searchTerm)
    {
        const string query = """
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

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@SearchTerm", SqlDbType.NVarChar).Value = searchTerm;
        command.Parameters.Add("@SearchTermStart", SqlDbType.NVarChar).Value = $"{searchTerm}%";
        command.Parameters.Add("@SearchTermContains", SqlDbType.NVarChar).Value = $"%{searchTerm}%";
        using var reader = await command.ExecuteReaderAsync();

        var results = new List<SchemaSearchResult>();
        while (await reader.ReadAsync())
        {
            results.Add(new SchemaSearchResult
            {
                ObjectType = reader.GetString("ObjectType"),
                ObjectName = reader.GetString("ObjectName"),
                SchemaName = reader.GetString("SchemaName"),
                TableName = reader.IsDBNull("TableName") ? null : reader.GetString("TableName"),
                MatchScore = GetSafeDecimal(reader, "MatchScore")
            });
        }

        return results;
    }
}
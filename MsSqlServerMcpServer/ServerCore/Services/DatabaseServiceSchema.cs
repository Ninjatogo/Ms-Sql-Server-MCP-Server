using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ServerCore.Models;

namespace ServerCore.Services;

/// <summary>
///     Partial class containing Schema Discovery & Documentation functionality
/// </summary>
public partial class DatabaseServiceBase
{
    /// <summary>
    ///     Get enhanced table information with metadata, statistics, and relationships
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
            await using var connection = new SqlConnection(GetConnectionString(database));
            var whereClauses = new List<string> { "t.TABLE_TYPE = 'BASE TABLE'" };
            var parameters = new DynamicParameters();

            if (!string.IsNullOrEmpty(schemaFilter))
            {
                whereClauses.Add("t.TABLE_SCHEMA = @SchemaFilter");
                parameters.Add("SchemaFilter", schemaFilter);
            }

            if (!string.IsNullOrEmpty(namePattern))
            {
                whereClauses.Add("t.TABLE_NAME LIKE @NamePattern");
                parameters.Add("NamePattern", $"%{namePattern}%");
            }

            var query = string.Format(SqlQueries.GetEnhancedTables, string.Join(" AND ", whereClauses));

            var tables = (await connection.QueryAsync<TableInfo>(query, parameters)).ToList();

            foreach (var table in tables)
                table.Columns =
                    await GetTableColumnNamesAsync(connection, table.TableName, table.SchemaName);

            logger.LogInformation("Retrieved {Count} enhanced tables from database {Database}", tables.Count,
                database ?? "default");
            return tables;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving enhanced tables from database {Database}", database);
            throw;
        }
    }


    /// <summary>
    ///     Get detailed table statistics including size, row count, and access patterns
    /// </summary>
    /// <param name="tableName">Table name</param>
    /// <param name="database">Target database (optional)</param>
    /// <param name="schemaName">Schema name (optional, defaults to dbo)</param>
    /// <returns>Table statistics</returns>
    public async Task<TableStats> GetTableStatsAsync(string tableName, string? database = null,
        string? schemaName = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            schemaName ??= "dbo";

            var stats = await connection.QuerySingleOrDefaultAsync<TableStats>(
                SqlQueries.GetTableStats,
                new { TableName = tableName, SchemaName = schemaName });

            if (stats != null)
            {
                logger.LogInformation(
                    "Retrieved table statistics for {SchemaName}.{TableName} from database {Database}",
                    schemaName, tableName, database ?? "default");
                return stats;
            }

            throw new InvalidOperationException($"Table {schemaName}.{tableName} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error retrieving table statistics for {SchemaName}.{TableName} from database {Database}",
                schemaName, tableName, database);
            throw;
        }
    }

    /// <summary>
    ///     Search database schema objects (tables, columns, indexes) by keyword
    /// </summary>
    /// <param name="searchTerm">Search term</param>
    /// <param name="database">Target database (optional)</param>
    /// <param name="objectType">Object type filter (Table, Column, Index) (optional)</param>
    /// <returns>List of matching schema objects</returns>
    public async Task<List<SchemaSearchResult>> SearchSchemaAsync(string searchTerm, string? database = null,
        string? objectType = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var results = new List<SchemaSearchResult>();

            // Search tables
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Table", StringComparison.OrdinalIgnoreCase))
                results.AddRange(await SearchTablesAsync(connection, searchTerm));

            // Search columns
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Column", StringComparison.OrdinalIgnoreCase))
                results.AddRange(await SearchColumnsAsync(connection, searchTerm));

            // Search indexes
            if (string.IsNullOrEmpty(objectType) || objectType.Equals("Index", StringComparison.OrdinalIgnoreCase))
                results.AddRange(await SearchIndexesAsync(connection, searchTerm));

            // Sort by match score descending
            var schemaSearchResults = results.OrderByDescending(r => r.MatchScore).ToList();
            logger.LogInformation("Schema search for '{SearchTerm}' returned {Count} results from database {Database}",
                searchTerm, results.Count, database ?? "default");
            return schemaSearchResults;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching schema for term '{SearchTerm}' in database {Database}", searchTerm,
                database);
            throw;
        }
    }

    /// <summary>
    ///     Get column names for a specific table
    /// </summary>
    private async Task<List<string>> GetTableColumnNamesAsync(SqlConnection connection, string tableName,
        string schemaName)
    {
        return (await connection.QueryAsync<string>(
            SqlQueries.GetTableColumnNames,
            new { TableName = tableName, SchemaName = schemaName })).ToList();
    }

    /// <summary>
    ///     Search for tables matching the search term
    /// </summary>
    private async Task<List<SchemaSearchResult>> SearchTablesAsync(SqlConnection connection, string searchTerm)
    {
        return (await connection.QueryAsync<SchemaSearchResult>(
            SqlQueries.SearchTables,
            new
            {
                SearchTerm = searchTerm,
                SearchTermStart = $"{searchTerm}%",
                SearchTermContains = $"%{searchTerm}%"
            })).ToList();
    }

    /// <summary>
    ///     Search for columns matching the search term
    /// </summary>
    private async Task<List<SchemaSearchResult>> SearchColumnsAsync(SqlConnection connection, string searchTerm)
    {
        return (await connection.QueryAsync<SchemaSearchResult>(
            SqlQueries.SearchColumns,
            new
            {
                SearchTerm = searchTerm,
                SearchTermStart = $"{searchTerm}%",
                SearchTermContains = $"%{searchTerm}%"
            })).ToList();
    }

    /// <summary>
    ///     Search for indexes matching the search term
    /// </summary>
    private async Task<List<SchemaSearchResult>> SearchIndexesAsync(SqlConnection connection, string searchTerm)
    {
        return (await connection.QueryAsync<SchemaSearchResult>(
            SqlQueries.SearchIndexes,
            new
            {
                SearchTerm = searchTerm,
                SearchTermStart = $"{searchTerm}%",
                SearchTermContains = $"%{searchTerm}%"
            })).ToList();
    }
}
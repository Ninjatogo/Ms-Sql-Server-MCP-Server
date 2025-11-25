using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServerCore.Interfaces;
using ServerCore.Models;

namespace ServerCore.Services;

public partial class DatabaseServiceBase(
    IConfiguration configuration,
    ILogger<DatabaseServiceBase> logger,
    IPiiFilterService piiFilterService) : IDatabaseService
{
    private string GetConnectionString(string? database = null)
    {
        var baseConnectionString = configuration.GetConnectionString("DefaultConnection")
                                   ?? throw new InvalidOperationException("DefaultConnection string not found in configuration");

        if (string.IsNullOrEmpty(database)) return baseConnectionString;

        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = database
        };
        return builder.ConnectionString;
    }

    public async Task<List<string>> GetDatabasesAsync()
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString());
            var databases = (await connection.QueryAsync<string>(SqlQueries.GetDatabases)).ToList();
            logger.LogInformation("Retrieved {Count} databases", databases.Count);
            return databases;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving databases");
            throw;
        }
    }

    public async Task<List<string>> GetTablesAsync(string? database = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var tables = (await connection.QueryAsync<string>(SqlQueries.GetTables)).ToList();
            logger.LogInformation("Retrieved {Count} tables from database {Database}",
                tables.Count, database ?? "default");
            return tables;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving tables from database {Database}", database);
            throw;
        }
    }

    public async Task<List<TableColumn>> GetTableSchemaAsync(string tableName, string? database = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var schemaData = await connection.QueryAsync<dynamic>(
                SqlQueries.GetTableSchema, new { TableName = tableName });

            var columns = schemaData.Select(row =>
            {
                var columnName = (string)row.COLUMN_NAME;
                return new TableColumn
                {
                    ColumnName = columnName,
                    DataType = row.DATA_TYPE,
                    IsNullable = string.Equals(row.IS_NULLABLE, "YES", StringComparison.OrdinalIgnoreCase),
                    MaxLength = (int?)row.CHARACTER_MAXIMUM_LENGTH,
                    NumericPrecision = (byte?)row.NUMERIC_PRECISION,
                    NumericScale = (int?)row.NUMERIC_SCALE,
                    DefaultValue = row.COLUMN_DEFAULT,
                    IsSensitive = piiFilterService.IsSensitiveColumn(columnName)
                };
            }).ToList();

            logger.LogInformation("Retrieved schema for table {TableName} with {Count} columns",
                tableName, columns.Count);

            var sensitiveCount = columns.Count(c => c.IsSensitive);
            if (sensitiveCount > 0)
            {
                logger.LogWarning("Table {TableName} contains {SensitiveCount} potentially sensitive columns",
                    tableName, sensitiveCount);
            }

            return columns;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving schema for table {TableName} in database {Database}",
                tableName, database);
            throw;
        }
    }

    public async Task<DatabaseQueryResult> ExecuteQueryAsync(string query, string? database = null, int maxRows = 1000)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var data = (await connection.QueryAsync(query, commandTimeout: 30)).Take(maxRows).ToList();

            var columns = new List<string>();
            var rows = new List<Dictionary<string, object?>>();

            if (data.Any())
            {
                var firstRow = (IDictionary<string, object?>)data.First();
                columns.AddRange(firstRow.Keys);

                rows = data.Select(r => new Dictionary<string, object?>((IDictionary<string, object?>)r)).ToList();
            }

            var filteredRows = piiFilterService.FilterRows(rows);
            var rowCount = filteredRows.Count;

            var result = new DatabaseQueryResult
            {
                Success = true,
                Columns = columns,
                Rows = filteredRows,
                RowCount = rowCount,
                Message = rowCount >= maxRows
                    ? $"Results limited to {maxRows} rows (PII filtered)"
                    : $"Retrieved {rowCount} rows (PII filtered)"
            };

            logger.LogInformation("Executed query successfully, returned {Count} rows with PII filtering applied", rowCount);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing query: {Query}", query);
            return new DatabaseQueryResult
            {
                Success = false,
                Message = ex.Message,
                Columns = [],
                Rows = [],
                RowCount = 0
            };
        }
    }

    public async Task<DatabaseQueryResult> ExecuteNonQueryAsync(string command, string? database = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var rowsAffected = await connection.ExecuteAsync(command, commandTimeout: 30);

            var result = new DatabaseQueryResult
            {
                Success = true,
                Message = $"Command executed successfully. {rowsAffected} rows affected.",
                Columns = [],
                Rows = [],
                RowCount = rowsAffected
            };

            logger.LogInformation("Executed non-query command successfully, {RowsAffected} rows affected", rowsAffected);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing non-query command: {Command}", command);
            return new DatabaseQueryResult
            {
                Success = false,
                Message = ex.Message,
                Columns = [],
                Rows = [],
                RowCount = 0
            };
        }
    }
}
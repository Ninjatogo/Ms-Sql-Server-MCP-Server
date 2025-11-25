using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServerCore.Interfaces;
using ServerCore.Models;

namespace ServerCore.Services;

public partial class DatabaseServiceBase : IDatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseServiceBase> _logger;
    private readonly IPiiFilterService _piiFilterService;

    /// <summary>
    /// Partial class implementing Core Query Capabilities for the DatabaseService
    /// These methods provide advanced query analysis, validation, and execution features
    /// </summary>
    public DatabaseServiceBase(IConfiguration configuration, 
        ILogger<DatabaseServiceBase> logger,
        IPiiFilterService piiFilterService)
    {
        _configuration = configuration;
        _logger = logger;
        _piiFilterService = piiFilterService;
    }

    private string GetConnectionString(string? database = null)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string not found in configuration");

        if (!string.IsNullOrEmpty(database))
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = database
            };
            return builder.ConnectionString;
        }

        return baseConnectionString;
    }

    public async Task<List<string>> GetDatabasesAsync()
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            const string query = """
                                     SELECT name 
                                     FROM sys.databases 
                                     WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')
                                     ORDER BY name
                                 """;

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var databases = new List<string>();
            while (await reader.ReadAsync())
            {
                databases.Add(reader.GetString(0));
            }

            _logger.LogInformation("Retrieved {Count} databases", databases.Count);
            return databases;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving databases");
            throw;
        }
    }

    public async Task<List<string>> GetTablesAsync(string? database = null)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            const string query = """
                                     SELECT TABLE_NAME 
                                     FROM INFORMATION_SCHEMA.TABLES 
                                     WHERE TABLE_TYPE = 'BASE TABLE'
                                     ORDER BY TABLE_NAME
                                 """;

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            _logger.LogInformation("Retrieved {Count} tables from database {Database}", 
                tables.Count, database ?? "default");
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tables from database {Database}", database);
            throw;
        }
    }

    public async Task<List<TableColumn>> GetTableSchemaAsync(string tableName, string? database = null)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            const string query = """
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

            using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@TableName", SqlDbType.NVarChar).Value = tableName;
            using var reader = await command.ExecuteReaderAsync();

            var columns = new List<TableColumn>();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString("COLUMN_NAME");
                var column = new TableColumn
                {
                    ColumnName = columnName,
                    DataType = reader.GetString("DATA_TYPE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                    MaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? null : GetSafeInt32(reader, "CHARACTER_MAXIMUM_LENGTH"),
                    NumericPrecision = reader.IsDBNull("NUMERIC_PRECISION") ? null : reader.GetByte("NUMERIC_PRECISION"),
                    NumericScale = reader.IsDBNull("NUMERIC_SCALE") ? null : GetSafeInt32(reader, "NUMERIC_SCALE"),
                    DefaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT"),
                    IsSensitive = _piiFilterService.IsSensitiveColumn(columnName) // Mark sensitive columns
                };
                
                columns.Add(column);
            }

            _logger.LogInformation("Retrieved schema for table {TableName} with {Count} columns", 
                tableName, columns.Count);
            
            var sensitiveCount = columns.Count(c => c.IsSensitive);
            if (sensitiveCount > 0)
            {
                _logger.LogWarning("Table {TableName} contains {SensitiveCount} potentially sensitive columns", 
                    tableName, sensitiveCount);
            }
            
            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema for table {TableName} in database {Database}", 
                tableName, database);
            throw;
        }
    }

    public async Task<DatabaseQueryResult> ExecuteQueryAsync(string query, string? database = null, int maxRows = 1000)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30; // 30 seconds timeout

            using var reader = await command.ExecuteReaderAsync();

            var columns = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rows = new List<Dictionary<string, object?>>();
            var rowCount = 0;

            while (await reader.ReadAsync() && rowCount < maxRows)
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
                rowCount++;
            }

            // Apply PII filtering to the results
            var filteredRows = _piiFilterService.FilterRows(rows);

            var result = new DatabaseQueryResult
            {
                Success = true,
                Columns = columns,
                Rows = filteredRows, // Use filtered rows instead of original
                RowCount = rowCount,
                Message = rowCount >= maxRows ? $"Results limited to {maxRows} rows (PII filtered)" : $"Retrieved {rowCount} rows (PII filtered)"
            };

            _logger.LogInformation("Executed query successfully, returned {Count} rows with PII filtering applied", rowCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {Query}", query);
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
            using var connection = new SqlConnection(GetConnectionString(database));
            await connection.OpenAsync();

            using var sqlCommand = new SqlCommand(command, connection);
            sqlCommand.CommandTimeout = 30;

            var rowsAffected = await sqlCommand.ExecuteNonQueryAsync();

            var result = new DatabaseQueryResult
            {
                Success = true,
                Message = $"Command executed successfully. {rowsAffected} rows affected.",
                Columns = [],
                Rows = [],
                RowCount = rowsAffected
            };

            _logger.LogInformation("Executed non-query command successfully, {RowsAffected} rows affected", rowsAffected);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing non-query command: {Command}", command);
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
    
    /// <summary>
    /// Safe method to get decimal value from SqlDataReader with proper type conversion
    /// </summary>
    private static decimal GetSafeDecimal(SqlDataReader reader, string columnName)
    {
        if (reader.IsDBNull(columnName))
            return 0;

        var value = reader[columnName];
        return value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            _ => Convert.ToDecimal(value)
        };
    }

    /// <summary>
    /// Safe method to get int32 value from SqlDataReader with proper type conversion
    /// </summary>
    private static int GetSafeInt32(SqlDataReader reader, string columnName)
    {
        if (reader.IsDBNull(columnName))
            return 0;

        var value = reader[columnName];
        return value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            decimal d => (int)d,
            double dbl => (int)dbl,
            float f => (int)f,
            _ => Convert.ToInt32(value)
        };
    }

    /// <summary>
    /// Safe method to get int64 value from SqlDataReader with proper type conversion
    /// </summary>
    private static long GetSafeInt64(SqlDataReader reader, string columnName)
    {
        if (reader.IsDBNull(columnName))
            return 0;

        var value = reader[columnName];
        return value switch
        {
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            decimal d => (long)d,
            double dbl => (long)dbl,
            float f => (long)f,
            _ => Convert.ToInt64(value)
        };
    }
}
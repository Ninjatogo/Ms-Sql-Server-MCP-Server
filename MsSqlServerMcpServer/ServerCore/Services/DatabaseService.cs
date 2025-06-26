using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServerCore.Interfaces;
using ServerCore.Models;

namespace ServerCore.Services;

public class DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    : IDatabaseService
{
    private string GetConnectionString(string? database = null)
    {
        var baseConnectionString = configuration.GetConnectionString("DefaultConnection") 
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
                columns.Add(new TableColumn
                {
                    ColumnName = reader.GetString("COLUMN_NAME"),
                    DataType = reader.GetString("DATA_TYPE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                    MaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                    NumericPrecision = reader.IsDBNull("NUMERIC_PRECISION") ? null : reader.GetByte("NUMERIC_PRECISION"),
                    NumericScale = reader.IsDBNull("NUMERIC_SCALE") ? null : reader.GetInt32("NUMERIC_SCALE"),
                    DefaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT")
                });
            }

            logger.LogInformation("Retrieved schema for table {TableName} with {Count} columns", 
                tableName, columns.Count);
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

            var result = new DatabaseQueryResult
            {
                Success = true,
                Columns = columns,
                Rows = rows,
                RowCount = rowCount,
                Message = rowCount >= maxRows ? $"Results limited to {maxRows} rows" : $"Retrieved {rowCount} rows"
            };

            logger.LogInformation("Executed query successfully, returned {Count} rows", rowCount);
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
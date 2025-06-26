using ServerCore.Models;

namespace ServerCore.Interfaces;

public interface IDatabaseService
{
    Task<List<string>> GetDatabasesAsync();
    Task<List<string>> GetTablesAsync(string? database = null);
    Task<List<TableColumn>> GetTableSchemaAsync(string tableName, string? database = null);
    Task<DatabaseQueryResult> ExecuteQueryAsync(string query, string? database = null, int maxRows = 1000);
    Task<DatabaseQueryResult> ExecuteNonQueryAsync(string command, string? database = null);
}
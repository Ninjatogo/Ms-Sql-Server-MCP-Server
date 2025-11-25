// using ServerCore.Models;
//
// namespace ServerCore.Interfaces;
//
// public interface IDatabaseService
// {
//     Task<List<string>> GetDatabasesAsync();
//     Task<List<string>> GetTablesAsync(string? database = null);
//     Task<List<TableColumn>> GetTableSchemaAsync(string tableName, string? database = null);
//     Task<DatabaseQueryResult> ExecuteQueryAsync(string query, string? database = null, int maxRows = 1000);
//     Task<DatabaseQueryResult> ExecuteNonQueryAsync(string command, string? database = null);
//     
//     // New method for executing queries without PII filtering (admin access)
//     Task<DatabaseQueryResult> ExecuteQueryWithoutFilteringAsync(string query, string? database = null, int maxRows = 1000);
// }

// Enhanced Database Service Interface
// Update ServerCore\Interfaces\IDatabaseService.cs

using ServerCore.Models;

namespace ServerCore.Interfaces;

public interface IDatabaseService
{
    // Basic Query Methods
    Task<List<string>> GetDatabasesAsync();
    Task<List<string>> GetTablesAsync(string? database = null);
    Task<List<TableColumn>> GetTableSchemaAsync(string tableName, string? database = null);
    Task<DatabaseQueryResult> ExecuteQueryAsync(string query, string? database = null, int maxRows = 1000);
    Task<DatabaseQueryResult> ExecuteNonQueryAsync(string command, string? database = null);

    // Core Query Capabilities
    Task<QueryPlanResult> ExplainQueryAsync(string query, string? database = null);
    Task<QueryValidationResult> ValidateQueryAsync(string query, string? database = null);
    Task<QueryCostResult> EstimateQueryCostAsync(string query, string? database = null);
    Task<EnhancedQueryResult> ExecuteQueryWithStatsAsync(string query, string? database = null, int maxRows = 1000);

    // Schema Discovery & Documentation
    Task<List<TableInfo>> GetEnhancedTablesAsync(string? database = null, string? schemaFilter = null, string? namePattern = null);
    Task<TableStats> GetTableStatsAsync(string tableName, string? database = null, string? schemaName = null);
    Task<List<SchemaSearchResult>> SearchSchemaAsync(string searchTerm, string? database = null, string? objectType = null);

    // Performance Analysis Tools
    Task<SlowQueryAnalysis> AnalyzeSlowQueriesAsync(string? database = null, int topCount = 50, int minimumExecutionTimeMs = 1000);
    Task<IndexUsageAnalysis> GetIndexUsageAsync(string? database = null, string? tableName = null);
    Task<MissingIndexAnalysis> FindMissingIndexesAsync(string? database = null, int topCount = 25);
    Task<WaitStatsAnalysis> GetWaitStatsAsync(string? database = null, int topCount = 20);
}
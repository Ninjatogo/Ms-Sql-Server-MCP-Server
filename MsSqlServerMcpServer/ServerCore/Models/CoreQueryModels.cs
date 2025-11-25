namespace ServerCore.Models;

// Query Plan Result
public class QueryPlanResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string ExecutionPlan { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public List<QueryPlanOperation> Operations { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public DateTime GeneratedAt { get; set; }
    public string? PlanType { get; set; } // Estimated or Actual
    public List<PlanOperator> Operators { get; set; } = [];
}

public class PlanOperator
{
    public string OperatorType { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedRows { get; set; }
    public string? Details { get; set; }
}

public class QueryComplexity
{
    public int JoinCount { get; set; }
    public int SubqueryCount { get; set; }
    public int AggregateCount { get; set; }
    public bool HasWindowFunctions { get; set; }
    public bool HasRecursiveCTE { get; set; }
    public string ComplexityLevel { get; set; } = "Simple"; // Simple, Medium, Complex, VeryComplex
}

public class QuerySafetyResult
{
    public bool IsSafe { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Violations { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool ContainsWriteOperations { get; set; }
    public bool ContainsDangerousFunctions { get; set; }
}

public class QueryPlanOperation
{
    public string OperationType { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal CostPercentage { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

// Query Validation Result
public class QueryValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
    public List<string> ValidationWarnings { get; set; } = [];
    public List<string> SyntaxErrors { get; set; } = [];
    public TimeSpan EstimatedExecutionTime { get; set; }
    public DateTime ValidatedAt { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public QueryComplexity Complexity { get; set; } = new();
}

// Query Cost Result
public class QueryCostResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedRows { get; set; }
    public decimal EstimatedExecutionTimeMs { get; set; }
    public string CostRating { get; set; } = string.Empty; // Low, Medium, High, Very High
    public List<string> CostDrivers { get; set; } = [];
    public List<string> OptimizationSuggestions { get; set; } = [];
    public DateTime AnalyzedAt { get; set; }
    
    public decimal EstimatedCpuCost { get; set; }
    public decimal EstimatedIoCost { get; set; }
    public string? ResourceUsage { get; set; }
    public List<string> Recommendations { get; set; } = [];
}

// Enhanced Query Result
public class EnhancedQueryResult : DatabaseQueryResult
{
    public TimeSpan ActualExecutionTime { get; set; }
    public long MemoryUsedKB { get; set; }
    public int LogicalReads { get; set; }
    public int PhysicalReads { get; set; }
    public QueryStatistics Statistics { get; set; } = new();
    public List<string> PerformanceNotes { get; set; } = [];
    public TimeSpan ExecutionTime { get; set; }
    public bool WasTruncated { get; set; }
    public string? PerformanceWarnings { get; set; }
}

public class QueryStatistics
{
    public decimal CpuTimeMs { get; set; }
    public decimal ElapsedTimeMs { get; set; }
    public long LogicalReads { get; set; }
    public long PhysicalReads { get; set; }
    public long ScanCount { get; set; }
    public long RowsReturned { get; set; }
    public string QueryPlan { get; set; } = string.Empty;
}

// Table Info (Enhanced)
// public class TableInfo
// {
//     public string TableName { get; set; } = string.Empty;
//     public string SchemaName { get; set; } = string.Empty;
//     public string FullName => $"{SchemaName}.{TableName}";
//     public long RowCount { get; set; }
//     public long SizeKB { get; set; }
//     public int ColumnCount { get; set; }
//     public DateTime? LastModified { get; set; }
//     public string TableType { get; set; } = string.Empty; // BASE TABLE, VIEW
//     public List<string> Indexes { get; set; } = new();
//     public List<string> ForeignKeys { get; set; } = new();
//     public bool HasPrimaryKey { get; set; }
//     public string Description { get; set; } = string.Empty;
// }
//
// // Table Relationships
// public class TableRelationship
// {
//     public string ParentTable { get; set; } = string.Empty;
//     public string ParentColumn { get; set; } = string.Empty;
//     public string ChildTable { get; set; } = string.Empty;
//     public string ChildColumn { get; set; } = string.Empty;
//     public string ConstraintName { get; set; } = string.Empty;
//     public string RelationshipType { get; set; } = string.Empty; // FK, PK, etc.
//     public bool IsEnforced { get; set; }
// }
//
// // Table Stats
// public class TableStats
// {
//     public string TableName { get; set; } = string.Empty;
//     public string SchemaName { get; set; } = string.Empty;
//     public long RowCount { get; set; }
//     public long ReservedSizeKB { get; set; }
//     public long DataSizeKB { get; set; }
//     public long IndexSizeKB { get; set; }
//     public long UnusedSizeKB { get; set; }
//     public DateTime? LastUpdated { get; set; }
//     public DateTime? LastAnalyzed { get; set; }
//     public List<IndexInfo> Indexes { get; set; } = new();
//     public Dictionary<string, object> ExtendedProperties { get; set; } = new();
// }

// public class IndexInfo
// {
//     public string IndexName { get; set; } = string.Empty;
//     public string IndexType { get; set; } = string.Empty;
//     public List<string> Columns { get; set; } = new();
//     public List<string> IncludedColumns { get; set; } = new();
//     public bool IsUnique { get; set; }
//     public bool IsPrimaryKey { get; set; }
//     public long SizeKB { get; set; }
//     public decimal FragmentationPercent { get; set; }
//     public long PageCount { get; set; }
// }
//
// // Schema Search Result
// public class SchemaSearchResult
// {
//     public string ObjectType { get; set; } = string.Empty; // Table, View, Column, Procedure
//     public string ObjectName { get; set; } = string.Empty;
//     public string SchemaName { get; set; } = string.Empty;
//     public string Description { get; set; } = string.Empty;
//     public string MatchType { get; set; } = string.Empty; // Exact, Partial, Fuzzy
//     public decimal RelevanceScore { get; set; }
//     public Dictionary<string, object> Properties { get; set; } = new();
// }

// Performance Analysis Models
// public class SlowQueryAnalysis
// {
//     public bool Success { get; set; }
//     public string Message { get; set; } = string.Empty;
//     public List<SlowQuery> SlowQueries { get; set; } = new();
//     public SlowQuerySummary Summary { get; set; } = new();
//     public DateTime AnalyzedAt { get; set; }
// }

public class SlowQuery
{
    public string QueryText { get; set; } = string.Empty;
    public string QueryHash { get; set; } = string.Empty;
    public decimal AverageExecutionTimeMs { get; set; }
    public decimal MaxExecutionTimeMs { get; set; }
    public long ExecutionCount { get; set; }
    public decimal TotalCpuTimeMs { get; set; }
    public long TotalLogicalReads { get; set; }
    public DateTime LastExecuted { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
}

public class SlowQuerySummary
{
    public int TotalSlowQueries { get; set; }
    public decimal AverageExecutionTime { get; set; }
    public long TotalExecutions { get; set; }
    public decimal TotalCpuTime { get; set; }
    public string MostExpensiveQuery { get; set; } = string.Empty;
}

// Index Usage Analysis
// public class IndexUsageAnalysis
// {
//     public bool Success { get; set; }
//     public string Message { get; set; } = string.Empty;
//     public List<IndexUsageInfo> IndexUsage { get; set; } = new();
//     public IndexUsageSummary Summary { get; set; } = new();
//     public DateTime AnalyzedAt { get; set; }
// }
//
// public class IndexUsageInfo
// {
//     public string TableName { get; set; } = string.Empty;
//     public string IndexName { get; set; } = string.Empty;
//     public long UserSeeks { get; set; }
//     public long UserScans { get; set; }
//     public long UserLookups { get; set; }
//     public long UserUpdates { get; set; }
//     public DateTime? LastUserSeek { get; set; }
//     public DateTime? LastUserScan { get; set; }
//     public DateTime? LastUserLookup { get; set; }
//     public decimal UsageScore { get; set; }
//     public string UsageRating { get; set; } = string.Empty;
// }
//
// public class IndexUsageSummary
// {
//     public int TotalIndexes { get; set; }
//     public int UnusedIndexes { get; set; }
//     public int HighlyUsedIndexes { get; set; }
//     public int LowUsageIndexes { get; set; }
//     public List<string> RecommendationsForRemoval { get; set; } = new();
// }
//
// // Missing Index Analysis
// public class MissingIndexAnalysis
// {
//     public bool Success { get; set; }
//     public string Message { get; set; } = string.Empty;
//     public List<MissingIndexInfo> MissingIndexes { get; set; } = new();
//     public MissingIndexSummary Summary { get; set; } = new();
//     public DateTime AnalyzedAt { get; set; }
// }
//
// public class MissingIndexInfo
// {
//     public string TableName { get; set; } = string.Empty;
//     public string SchemaName { get; set; } = string.Empty;
//     public List<string> EqualityColumns { get; set; } = new();
//     public List<string> InequalityColumns { get; set; } = new();
//     public List<string> IncludedColumns { get; set; } = new();
//     public decimal ImprovementPercent { get; set; }
//     public long UserSeeks { get; set; }
//     public long UserScans { get; set; }
//     public decimal AvgTotalUserCost { get; set; }
//     public string SuggestedIndexName { get; set; } = string.Empty;
//     public string CreateIndexScript { get; set; } = string.Empty;
// }
//
// public class MissingIndexSummary
// {
//     public int TotalMissingIndexes { get; set; }
//     public decimal AverageImprovementPercent { get; set; }
//     public int HighImpactIndexes { get; set; }
//     public List<string> TopTables { get; set; } = new();
// }
//
// // Wait Stats Analysis
// public class WaitStatsAnalysis
// {
//     public bool Success { get; set; }
//     public string Message { get; set; } = string.Empty;
//     public List<WaitStatInfo> WaitStats { get; set; } = new();
//     public WaitStatsSummary Summary { get; set; } = new();
//     public DateTime AnalyzedAt { get; set; }
// }
//
// public class WaitStatInfo
// {
//     public string WaitType { get; set; } = string.Empty;
//     public long WaitingTasksCount { get; set; }
//     public decimal WaitTimeMs { get; set; }
//     public decimal MaxWaitTimeMs { get; set; }
//     public decimal SignalWaitTimeMs { get; set; }
//     public decimal WaitTimePercentage { get; set; }
//     public string Description { get; set; } = string.Empty;
//     public string Category { get; set; } = string.Empty; // CPU, IO, Lock, Network, etc.
//     public List<string> Recommendations { get; set; } = new();
// }
//
// public class WaitStatsSummary
// {
//     public int TotalWaitTypes { get; set; }
//     public decimal TotalWaitTimeMs { get; set; }
//     public string TopWaitType { get; set; } = string.Empty;
//     public Dictionary<string, decimal> WaitsByCategory { get; set; } = new();
//     public List<string> CriticalWaits { get; set; } = new();
// }
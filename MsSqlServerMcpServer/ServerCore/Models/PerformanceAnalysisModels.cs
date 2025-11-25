namespace ServerCore.Models;

// Slow query analysis result
public class SlowQueryAnalysis
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SlowQueryInfo> SlowQueries { get; set; } = [];
    public QueryPerformanceSummary Summary { get; set; } = new();
}

public class SlowQueryInfo
{
    public string QueryHash { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public decimal TotalElapsedTimeMs { get; set; }
    public decimal AverageElapsedTimeMs { get; set; }
    public decimal MaxElapsedTimeMs { get; set; }
    public decimal TotalCpuTimeMs { get; set; }
    public decimal AverageCpuTimeMs { get; set; }
    public long TotalLogicalReads { get; set; }
    public long AverageLogicalReads { get; set; }
    public long TotalPhysicalReads { get; set; }
    public long AveragePhysicalReads { get; set; }
    public DateTime LastExecutionTime { get; set; }
    public DateTime CreationTime { get; set; }
    public decimal TotalWorkerTimeMs { get; set; }
    public string? DatabaseName { get; set; }
}

public class QueryPerformanceSummary
{
    public int TotalQueries { get; set; }
    public int SlowQueries { get; set; }
    public decimal AverageExecutionTimeMs { get; set; }
    public decimal TotalCpuTimeSeconds { get; set; }
    public long TotalLogicalReads { get; set; }
    public long TotalPhysicalReads { get; set; }
    public DateTime AnalysisTimestamp { get; set; }
}

// Index usage statistics
public class IndexUsageAnalysis
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<IndexUsageInfo> IndexUsageStats { get; set; } = [];
    public IndexUsageSummary Summary { get; set; } = new();
}

public class IndexUsageInfo
{
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public long UserUpdates { get; set; }
    public DateTime? LastUserSeek { get; set; }
    public DateTime? LastUserScan { get; set; }
    public DateTime? LastUserLookup { get; set; }
    public DateTime? LastUserUpdate { get; set; }
    public long SizeKb { get; set; }
    public decimal UsageScore { get; set; } // Calculated usage score
    public string UsageCategory { get; set; } = string.Empty; // Heavy, Moderate, Light, Unused
}

public class IndexUsageSummary
{
    public int TotalIndexes { get; set; }
    public int UnusedIndexes { get; set; }
    public int HeavilyUsedIndexes { get; set; }
    public int LightlyUsedIndexes { get; set; }
    public long TotalIndexSizeKb { get; set; }
    public long UnusedIndexSizeKb { get; set; }
    public DateTime AnalysisTimestamp { get; set; }
}

// Missing index suggestions
public class MissingIndexAnalysis
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MissingIndexInfo> MissingIndexes { get; set; } = [];
    public MissingIndexSummary Summary { get; set; } = new();
}

public class MissingIndexInfo
{
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string EqualityColumns { get; set; } = string.Empty;
    public string InequalityColumns { get; set; } = string.Empty;
    public string IncludedColumns { get; set; } = string.Empty;
    public decimal ImprovementMeasure { get; set; }
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public decimal AvgTotalUserCost { get; set; }
    public decimal AvgUserImpact { get; set; }
    public DateTime LastUserSeek { get; set; }
    public DateTime LastUserScan { get; set; }
    public string SuggestedIndexName { get; set; } = string.Empty;
    public string CreateIndexStatement { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty; // High, Medium, Low
}

public class MissingIndexSummary
{
    public int TotalSuggestions { get; set; }
    public int HighPrioritySuggestions { get; set; }
    public int MediumPrioritySuggestions { get; set; }
    public int LowPrioritySuggestions { get; set; }
    public decimal TotalPotentialImprovement { get; set; }
    public DateTime AnalysisTimestamp { get; set; }
}

// Wait statistics
public class WaitStatsAnalysis
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<WaitStatsInfo> WaitStats { get; set; } = [];
    public WaitStatsSummary Summary { get; set; } = new();
}

public class WaitStatsInfo
{
    public string WaitType { get; set; } = string.Empty;
    public long WaitingTasksCount { get; set; }
    public decimal WaitTimeMs { get; set; }
    public decimal MaxWaitTimeMs { get; set; }
    public decimal SignalWaitTimeMs { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public decimal AverageWaitTimeMs { get; set; }
    public string WaitCategory { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class WaitStatsSummary
{
    public decimal TotalWaitTimeMs { get; set; }
    public int UniqueWaitTypes { get; set; }
    public string TopWaitType { get; set; } = string.Empty;
    public decimal TopWaitPercentage { get; set; }
    public List<string> MainBottlenecks { get; set; } = [];
    public DateTime AnalysisTimestamp { get; set; }
}

// Query performance comparison
public class QueryPerformanceComparison
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<QueryVariant> QueryVariants { get; set; } = [];
    public string RecommendedVariant { get; set; } = string.Empty;
    public string ComparisonSummary { get; set; } = string.Empty;
}

public class QueryVariant
{
    public string VariantName { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public decimal ExecutionTimeMs { get; set; }
    public long LogicalReads { get; set; }
    public long PhysicalReads { get; set; }
    public decimal CpuTimeMs { get; set; }
    public int PlanOperatorCount { get; set; }
    public bool HasWarnings { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public decimal PerformanceScore { get; set; } // Calculated overall score
}

// Performance monitoring result
public class PerformanceMonitoringResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime MonitoringStart { get; set; }
    public DateTime MonitoringEnd { get; set; }
    public SystemPerformanceMetrics SystemMetrics { get; set; } = new();
    public List<DatabasePerformanceMetrics> DatabaseMetrics { get; set; } = [];
    public List<string> Alerts { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}

public class SystemPerformanceMetrics
{
    public decimal CpuUsagePercentage { get; set; }
    public decimal MemoryUsagePercentage { get; set; }
    public decimal IoUsagePercentage { get; set; }
    public long TotalConnections { get; set; }
    public long ActiveConnections { get; set; }
    public decimal TransactionsPerSecond { get; set; }
    public decimal BatchRequestsPerSecond { get; set; }
    public long PageLifeExpectancy { get; set; }
    public decimal BufferCacheHitRatio { get; set; }
}

public class DatabasePerformanceMetrics
{
    public string DatabaseName { get; set; } = string.Empty;
    public long ActiveTransactions { get; set; }
    public decimal LogGrowthMb { get; set; }
    public decimal DataGrowthMb { get; set; }
    public long WaitingTasks { get; set; }
    public decimal AverageQueryExecutionTimeMs { get; set; }
    public long QueriesPerSecond { get; set; }
    public decimal BlockingPercentage { get; set; }
}
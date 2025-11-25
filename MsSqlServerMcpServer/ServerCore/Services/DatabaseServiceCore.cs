using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ServerCore.Models;

namespace ServerCore.Services;

/// <summary>
///     Partial class implementing Core Query Capabilities for the DatabaseService
///     These methods provide advanced query analysis, validation, and execution features
/// </summary>
public partial class DatabaseServiceBase
{
    /// <summary>
    ///     Get the execution plan for a query to help with performance analysis
    /// </summary>
    /// <param name="query">The SQL query to analyze</param>
    /// <param name="database">Optional database name</param>
    /// <returns>Query execution plan details</returns>
    public async Task<QueryPlanResult> ExplainQueryAsync(string query, string? database = null)
    {
        try
        {
            // Validate that this is a SELECT query for safety
            var safetyCheck = await ValidateQuerySafetyAsync(query);
            if (!safetyCheck.IsSafe)
                return new QueryPlanResult
                {
                    Success = false,
                    Message = $"Query safety validation failed: {safetyCheck.Message}",
                    Query = query
                };

            await using var connection = new SqlConnection(GetConnectionString(database));
            // Get estimated execution plan
            var planResult = new QueryPlanResult
            {
                Query = query,
                PlanType = "Estimated",
                Success = true
            };

            // Enable SHOWPLAN_XML to get detailed execution plan
            await connection.ExecuteAsync("SET SHOWPLAN_XML ON");

            try
            {
                planResult.ExecutionPlan =
                    await connection.QuerySingleOrDefaultAsync<string>(query, commandTimeout: 30);
            }
            finally
            {
                // Always turn off SHOWPLAN_XML
                await connection.ExecuteAsync("SET SHOWPLAN_XML OFF");
            }

            // Parse basic plan information
            if (!string.IsNullOrEmpty(planResult.ExecutionPlan))
            {
                planResult.EstimatedCost = ExtractEstimatedCostFromPlan(planResult.ExecutionPlan);
                planResult.Operators = ExtractOperatorsFromPlan(planResult.ExecutionPlan);
            }

            planResult.Message = "Execution plan retrieved successfully";
            logger.LogInformation("Generated execution plan for query in database {Database}", database ?? "default");

            return planResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating execution plan for query in database {Database}", database);
            return new QueryPlanResult
            {
                Success = false,
                Message = ex.Message,
                Query = query
            };
        }
    }

    /// <summary>
    ///     Validate SQL syntax and check for potential issues without execution
    /// </summary>
    /// <param name="query">The SQL query to validate</param>
    /// <param name="database">Optional database name</param>
    /// <returns>Validation result with errors and warnings</returns>
    public async Task<QueryValidationResult> ValidateQueryAsync(string query, string? database = null)
    {
        try
        {
            await using var connection = new SqlConnection(GetConnectionString(database));
            var result = new QueryValidationResult();

            // Basic safety check
            var safetyCheck = await ValidateQuerySafetyAsync(query);
            if (!safetyCheck.IsSafe)
            {
                result.ValidationErrors.AddRange(safetyCheck.Violations);
                result.ValidationWarnings.AddRange(safetyCheck.Warnings);
            }

            // Syntax validation using SET PARSEONLY
            await connection.ExecuteAsync("SET PARSEONLY ON");

            try
            {
                await connection.ExecuteAsync(query, commandTimeout: 10);
                // If we get here, syntax is valid
                result.IsValid = true;
                result.Message = "Query syntax is valid";
            }
            catch (SqlException sqlEx)
            {
                result.IsValid = false;
                result.SyntaxErrors.Add($"SQL Syntax Error: {sqlEx.Message}");
                result.Message = "Query contains syntax errors";
            }
            finally
            {
                // Always turn off PARSEONLY
                await connection.ExecuteAsync("SET PARSEONLY OFF");
            }

            // Analyze query complexity
            result.Complexity = AnalyzeQueryComplexity(query);

            // Add complexity-based warnings
            if (result.Complexity.ComplexityLevel == "VeryComplex")
                result.Warnings.Add("Query is very complex and may have performance implications");

            if (result.Complexity.JoinCount > 5)
                result.Warnings.Add($"Query contains {result.Complexity.JoinCount} joins which may impact performance");

            logger.LogInformation("Validated query syntax for database {Database}, IsValid: {IsValid}",
                database ?? "default", result.IsValid);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating query for database {Database}", database);
            return new QueryValidationResult
            {
                IsValid = false,
                Message = ex.Message,
                Errors = [ex.Message]
            };
        }
    }

    /// <summary>
    ///     Estimate query execution cost and resource usage
    /// </summary>
    /// <param name="query">The SQL query to analyze</param>
    /// <param name="database">Optional database name</param>
    /// <returns>Cost estimation details</returns>
    public async Task<QueryCostResult> EstimateQueryCostAsync(string query, string? database = null)
    {
        try
        {
            // First validate the query is safe
            var safetyCheck = await ValidateQuerySafetyAsync(query);
            if (!safetyCheck.IsSafe)
                return new QueryCostResult
                {
                    Success = false,
                    Message = $"Query safety validation failed: {safetyCheck.Message}"
                };

            await using var connection = new SqlConnection(GetConnectionString(database));
            var result = new QueryCostResult { Success = true };

            // Enable statistics to get cost information
            await connection.ExecuteAsync("SET STATISTICS IO ON; SET STATISTICS TIME ON;");

            // Get execution plan for cost estimation
            var planResult = await ExplainQueryAsync(query, database);
            if (planResult.Success && planResult.EstimatedCost is not null)
            {
                result.EstimatedCost = planResult.EstimatedCost.Value;

                // Estimate resource usage based on cost
                if (result.EstimatedCost < 1)
                    result.ResourceUsage = "Low";
                else if (result.EstimatedCost < 10)
                    result.ResourceUsage = "Medium";
                else if (result.EstimatedCost < 100)
                    result.ResourceUsage = "High";
                else
                {
                    result.ResourceUsage = "Very High";
                    result.Recommendations.Add("Consider optimizing this query as it has a very high estimated cost");
                }
            }

            // Analyze query for performance recommendations
            var complexity = AnalyzeQueryComplexity(query);

            if (complexity.JoinCount > 3)
                result.Recommendations.Add("Consider adding appropriate indexes for join operations");

            if (complexity.SubqueryCount > 2)
                result.Recommendations.Add("Consider rewriting subqueries as CTEs or joins for better performance");

            if (query.ToUpperInvariant().Contains("SELECT *"))
                result.Recommendations.Add("Avoid SELECT * and specify only required columns");

            result.Message = $"Cost estimation completed. Estimated cost: {result.EstimatedCost}";

            logger.LogInformation("Estimated query cost: {Cost} for database {Database}",
                result.EstimatedCost, database ?? "default");

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error estimating query cost for database {Database}", database);
            return new QueryCostResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    ///     Execute a query with detailed statistics and performance metrics
    /// </summary>
    /// <param name="query">The SQL query to execute</param>
    /// <param name="database">Optional database name</param>
    /// <param name="maxRows">Maximum number of rows to return</param>
    /// <returns>Enhanced query result with performance statistics</returns>
    public async Task<EnhancedQueryResult> ExecuteQueryWithStatsAsync(string query, string? database = null,
        int maxRows = 1000)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // First validate the query is safe
            var safetyCheck = await ValidateQuerySafetyAsync(query);
            if (!safetyCheck.IsSafe)
                return new EnhancedQueryResult
                {
                    Success = false,
                    Message = $"Query safety validation failed: {safetyCheck.Message}",
                    ActualExecutionTime = stopwatch.Elapsed
                };

            await using var connection = new SqlConnection(GetConnectionString(database));
            var result = new EnhancedQueryResult();

            // Enable statistics collection
            await connection.ExecuteAsync("SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS PROFILE ON;");

            try
            {
                var data = (await connection.QueryAsync(query, commandTimeout: 60))
                    .Take(maxRows)
                    .ToList();
                var columns = new List<string>();
                var rows = new List<Dictionary<string, object?>>();

                if (data.Any())
                {
                    var firstRow = (IDictionary<string, object?>)data.First();
                    columns.AddRange(firstRow.Keys);

                    rows = data.Select(r => new Dictionary<string, object?>((IDictionary<string, object?>)r)).ToList();
                }

                // Apply PII filtering
                var filteredRows = piiFilterService.FilterRows(rows);
                var rowCount = filteredRows.Count;

                // Populate result
                result.Success = true;
                result.Columns = columns;
                result.Rows = filteredRows;
                result.RowCount = rowCount;
                result.WasTruncated = rowCount >= maxRows;
                result.ExecutionTime = stopwatch.Elapsed;

                // Add performance warnings
                if (result.ExecutionTime.TotalSeconds > 5)
                    result.PerformanceWarnings = "Query took longer than 5 seconds to execute";
                else if (result.ExecutionTime.TotalSeconds > 1)
                    result.PerformanceWarnings = "Query execution time is elevated";

                result.Message = $"Retrieved {rowCount} rows in {result.ExecutionTime.TotalMilliseconds:F0}ms" +
                                 (result.WasTruncated ? $" (limited to {maxRows} rows)" : "") + " (PII filtered)";

                logger.LogInformation("Executed query with stats: {RowCount} rows in {ExecutionTime}ms",
                    rowCount, result.ExecutionTime.TotalMilliseconds);
            }
            finally
            {
                // Turn off statistics
                try
                {
                    await connection.ExecuteAsync(
                        "SET STATISTICS IO OFF; SET STATISTICS TIME OFF; SET STATISTICS PROFILE OFF;");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to turn off statistics collection");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error executing query with stats for database {Database}", database);
            return new EnhancedQueryResult
            {
                Success = false,
                Message = ex.Message,
                Columns = [],
                Rows = [],
                RowCount = 0,
                ActualExecutionTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    ///     Validate query safety by checking for dangerous operations
    /// </summary>
    private Task<QuerySafetyResult> ValidateQuerySafetyAsync(string query)
    {
        var result = new QuerySafetyResult { IsSafe = true };
        var normalizedQuery = query.Trim().ToUpperInvariant();

        // Check for write operations
        string[] writeOperations =
            ["INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", "MERGE"];
        foreach (var operation in writeOperations)
            if (normalizedQuery.StartsWith($"{operation} ") || normalizedQuery.Contains($" {operation} "))
            {
                result.IsSafe = false;
                result.ContainsWriteOperations = true;
                result.Violations.Add($"Query contains write operation: {operation}");
            }

        // Check for dangerous functions
        string[] dangerousFunctions = ["xp_cmdshell", "sp_configure", "OPENROWSET", "OPENDATASOURCE"];
        foreach (var function in dangerousFunctions)
            if (normalizedQuery.Contains(function.ToUpperInvariant()))
            {
                result.IsSafe = false;
                result.ContainsDangerousFunctions = true;
                result.Violations.Add($"Query contains dangerous function: {function}");
            }

        // Check for potential SQL injection patterns
        string[] injectionPatterns =
        [
            @";\s*(DROP|DELETE|INSERT|UPDATE)",
            @"UNION\s+SELECT",
            @"--\s*$",
            @"\/\*.*\*\/"
        ];

        foreach (var pattern in injectionPatterns)
            if (Regex.IsMatch(normalizedQuery, pattern, RegexOptions.IgnoreCase))
                result.Warnings.Add($"Potential SQL injection pattern detected: {pattern}");

        if (!result.IsSafe)
            result.Message = "Query contains unsafe operations";
        else if (result.Warnings.Any())
            result.Message = "Query passed safety check but has warnings";
        else
            result.Message = "Query is safe to execute";

        return Task.FromResult(result);
    }

    /// <summary>
    ///     Analyze query complexity for warnings and recommendations
    /// </summary>
    private QueryComplexity AnalyzeQueryComplexity(string query)
    {
        var normalizedQuery = query.ToUpperInvariant();
        var complexity = new QueryComplexity();

        // Count joins
        complexity.JoinCount = Regex.Matches(normalizedQuery, @"\bJOIN\b").Count;

        // Count subqueries
        complexity.SubqueryCount = Regex.Matches(normalizedQuery, @"\(\s*SELECT\b").Count;

        // Count aggregates
        string[] aggregateFunctions = ["COUNT", "SUM", "AVG", "MIN", "MAX", "GROUP_CONCAT"];
        foreach (var func in aggregateFunctions)
            complexity.AggregateCount += Regex.Matches(normalizedQuery, $@"\b{func}\s*\(").Count;

        // Check for window functions
        complexity.HasWindowFunctions = Regex.IsMatch(normalizedQuery, @"\bOVER\s*\(");

        // Check for recursive CTEs
        complexity.HasRecursiveCTE = normalizedQuery.Contains("WITH") && normalizedQuery.Contains("UNION");

        // Determine complexity level
        var complexityScore = complexity.JoinCount * 2 +
                              complexity.SubqueryCount * 3 +
                              complexity.AggregateCount +
                              (complexity.HasWindowFunctions ? 5 : 0) +
                              (complexity.HasRecursiveCTE ? 10 : 0);

        complexity.ComplexityLevel = complexityScore switch
        {
            <= 3 => "Simple",
            <= 8 => "Medium",
            <= 15 => "Complex",
            _ => "VeryComplex"
        };

        return complexity;
    }

    /// <summary>
    ///     Extract estimated cost from XML execution plan
    /// </summary>
    private decimal ExtractEstimatedCostFromPlan(string xmlPlan)
    {
        try
        {
            // Simple regex to extract StatementSubTreeCost from XML plan
            var match = Regex.Match(xmlPlan, @"StatementSubTreeCost=""([^""]+)""");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var cost)) return cost;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract cost from execution plan");
        }

        return 0;
    }

    /// <summary>
    ///     Extract key operators from XML execution plan
    /// </summary>
    private List<PlanOperator> ExtractOperatorsFromPlan(string xmlPlan)
    {
        var operators = new List<PlanOperator>();

        try
        {
            // Simple regex-based extraction of operator information
            var operatorMatches = Regex.Matches(xmlPlan,
                @"<RelOp.*?PhysicalOp=""([^""]+)"".*?EstimateCPU=""([^""]+)"".*?EstimateRows=""([^""]+)""");

            foreach (Match match in operatorMatches)
                if (match.Groups.Count >= 4)
                {
                    var op = new PlanOperator
                    {
                        OperatorType = match.Groups[1].Value,
                        EstimatedCost = decimal.TryParse(match.Groups[2].Value, out var cost) ? cost : 0,
                        EstimatedRows = decimal.TryParse(match.Groups[3].Value, out var rows) ? rows : 0
                    };
                    operators.Add(op);
                }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract operators from execution plan");
        }

        return operators;
    }
}
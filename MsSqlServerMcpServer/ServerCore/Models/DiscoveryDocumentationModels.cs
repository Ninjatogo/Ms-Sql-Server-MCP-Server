namespace ServerCore.Models;

// Enhanced table information
public class TableInfo
{
    public string TableName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableType { get; set; } = string.Empty; // BASE TABLE, VIEW, etc.
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public long? RowCount { get; set; }
    public long? DataSizeMb { get; set; }
    public long? IndexSizeMb { get; set; }
    public string? Description { get; set; }
    public bool HasPrimaryKey { get; set; }
    public bool HasForeignKeys { get; set; }
    public bool HasIndexes { get; set; }
    public List<string> Columns { get; set; } = [];
    public long? ColumnCount { get; set; }
}

// Foreign key relationship information
public class TableRelationship
{
    public string ParentTable { get; set; } = string.Empty;
    public string ParentSchema { get; set; } = string.Empty;
    public string ParentColumn { get; set; } = string.Empty;
    public string ChildTable { get; set; } = string.Empty;
    public string ChildSchema { get; set; } = string.Empty;
    public string ChildColumn { get; set; } = string.Empty;
    public string ConstraintName { get; set; } = string.Empty;
    public string? DeleteAction { get; set; }
    public string? UpdateAction { get; set; }
}

// Table statistics
public class TableStats
{
    public string TableName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public long ReservedSpaceKb { get; set; }
    public long DataSpaceKb { get; set; }
    public long IndexSpaceKb { get; set; }
    public long UnusedSpaceKb { get; set; }
    public DateTime? LastUserAccess { get; set; }
    public DateTime? LastUserUpdate { get; set; }
    public DateTime? LastStatsUpdate { get; set; }
    public int IndexCount { get; set; }
    public bool HasClusteredIndex { get; set; }
}

// Schema search result
public class SchemaSearchResult
{
    public string ObjectType { get; set; } = string.Empty; // Table, Column, Index, etc.
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string? TableName { get; set; } // For columns and indexes
    public string? DataType { get; set; } // For columns
    public string? Description { get; set; }
    public decimal MatchScore { get; set; } // Relevance score
}
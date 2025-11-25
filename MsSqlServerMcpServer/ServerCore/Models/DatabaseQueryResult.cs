namespace ServerCore.Models;

public class DatabaseQueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
    public int RowCount { get; set; }
}
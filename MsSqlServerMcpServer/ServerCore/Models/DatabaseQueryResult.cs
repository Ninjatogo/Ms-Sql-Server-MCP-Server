namespace ServerCore.Models;

public class DatabaseQueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
}
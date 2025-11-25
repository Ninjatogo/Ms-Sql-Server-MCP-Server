namespace ServerCore.Interfaces;

public interface IPiiFilterService
{
    /// <summary>
    /// Determines if a column name indicates it contains sensitive information
    /// </summary>
    bool IsSensitiveColumn(string columnName);
    
    /// <summary>
    /// Checks if a value contains PII patterns (email, phone, SSN, etc.)
    /// </summary>
    bool ContainsPii(object? value);
    
    /// <summary>
    /// Masks sensitive values in a single field
    /// </summary>
    object? MaskSensitiveValue(object? value, string columnName);
    
    /// <summary>
    /// Filters and masks sensitive data in a single row
    /// </summary>
    Dictionary<string, object?> FilterRow(Dictionary<string, object?> row);
    
    /// <summary>
    /// Filters and masks sensitive data in multiple rows
    /// </summary>
    List<Dictionary<string, object?>> FilterRows(List<Dictionary<string, object?>> rows);
}
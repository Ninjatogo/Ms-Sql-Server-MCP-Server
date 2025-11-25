using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ServerCore.Interfaces;

namespace ServerCore.Services;

public class PiiFilterService(ILogger<PiiFilterService> logger) : IPiiFilterService
{
    // Regex patterns for detecting PII
    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex PhoneRegex = new(
        @"(\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})",
        RegexOptions.Compiled);
    
    private static readonly Regex SsnRegex = new(
        @"\b\d{3}-\d{2}-\d{4}\b|\b\d{9}\b",
        RegexOptions.Compiled);
    
    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d{4}[-\s]?){3}\d{4}\b",
        RegexOptions.Compiled);
    
    // Bank account number patterns
    private static readonly Regex BankAccountRegex = new(
        @"\b\d{8,17}\b", // Most bank accounts are 8-17 digits
        RegexOptions.Compiled);
    
    // Formatted account number patterns (with dashes, dots, spaces)
    private static readonly Regex FormattedAccountRegex = new(
        @"\b\d{2,4}[-.\s]\d{2,8}(?:[-.\s]\d{1,8})*\b", // Patterns like 014-00066, 01-01-01-99-1082000
        RegexOptions.Compiled);
    
    // Specific patterns for common formatted account types
    private static readonly Regex DashedAccountRegex = new(
        @"\b\d{3}-\d{5,8}\b|\b\d{2}(?:-\d{2}){2,5}-\d{4,10}\b", // 014-00066 or 01-01-01-99-1082000 style
        RegexOptions.Compiled);
    
    // US routing number pattern (9 digits, specific validation)
    private static readonly Regex RoutingNumberRegex = new(
        @"\b\d{9}\b",
        RegexOptions.Compiled);
    
    // IBAN pattern (International Bank Account Number)
    private static readonly Regex IbanRegex = new(
        @"\b[A-Z]{2}\d{2}[A-Z0-9]{4,30}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex MonetaryRegex = new(
        @"^\$?\d{1,3}(?:,\d{3})*(?:\.\d{2})?$|^\d+\.\d{2}$|^\d+$",
        RegexOptions.Compiled);
    
    // Common PII column name patterns
    private static readonly HashSet<string> SensitiveColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "emailaddress", "email_address", "e_mail",
        "phone", "phonenumber", "phone_number", "mobile", "cellphone", "telephone",
        "ssn", "socialsecuritynumber", "social_security_number",
        "creditcard", "credit_card", "cardnumber", "card_number",
        "password", "pwd", "passcode", "pin",
        "dob", "dateofbirth", "date_of_birth", "birthdate",
        "address", "street", "streetaddress", "street_address",
        "zipcode", "zip_code", "postalcode", "postal_code",
        "driverlicense", "driver_license", "license_number",
        // Bank account related fields
        "accountnumber", "account_number", "bankaccount", "bank_account",
        "routingnumber", "routing_number", "aba", "aba_number",
        "iban", "swift", "sortcode", "sort_code", "bsb",
        "accountno", "acct_number", "acct_no", "bank_acc"
    };
    
    private static readonly HashSet<string> BalanceColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "balance", "currentbalance", "available_balance", "availablebalance",
        "amount", "total", "subtotal", "price", "cost", "value", "worth",
        "balance_amount", "account_balance", "ending_balance", "endingbalance",
        "beginning_balance", "beginningbalance", "current_amount", "currentamount",
        "balance_due", "balancedue", "outstanding_balance", "outstandingbalance",
        "ledger_balance", "ledgerbalance", "cleared_balance", "clearedbalance",
        "pending_balance", "pendingbalance", "hold_amount", "holdamount",
        "credit_balance", "creditbalance", "debit_balance", "debitbalance"
    };

    public bool IsSensitiveColumn(string columnName)
    {
        // First check if it's a known balance/monetary column - these should NOT be treated as sensitive
        if (IsBalanceColumn(columnName))
        {
            return false;
        }

        // Check if column name matches known sensitive patterns
        return SensitiveColumnNames.Contains(columnName) ||
               columnName.Contains("email", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("ssn", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("address", StringComparison.OrdinalIgnoreCase) ||
               (columnName.Contains("account", StringComparison.OrdinalIgnoreCase) && 
                !columnName.Contains("name", StringComparison.OrdinalIgnoreCase) && 
                !columnName.Contains("type", StringComparison.OrdinalIgnoreCase) &&
                !IsBalanceColumn(columnName)) ||
               columnName.Contains("routing", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("iban", StringComparison.OrdinalIgnoreCase) ||
               (columnName.Contains("bank", StringComparison.OrdinalIgnoreCase) && !IsBalanceColumn(columnName));
    }

    public bool ContainsPii(object? value)
    {
        if (value == null) return false;
        
        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue)) return false;

        return EmailRegex.IsMatch(stringValue) ||
               PhoneRegex.IsMatch(stringValue) ||
               SsnRegex.IsMatch(stringValue) ||
               CreditCardRegex.IsMatch(stringValue) ||
               ContainsBankingInfo(stringValue);
    }

    public object? MaskSensitiveValue(object? value, string columnName)
    {
        if (value == null) return null;
    
        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue)) return value;

        // If it's a known balance/monetary column, don't mask it
        if (IsBalanceColumn(columnName))
        {
            logger.LogDebug("Skipping PII detection for balance column: {ColumnName}", columnName);
            return value;
        }

        // If the value looks like monetary data, don't mask it
        if (IsLikelyMonetaryValue(stringValue, columnName))
        {
            logger.LogDebug("Skipping PII detection for monetary value in column: {ColumnName}, value: {Value}", 
                columnName, stringValue);
            return value;
        }

        // If it's a sensitive column, mask it regardless of content
        if (IsSensitiveColumn(columnName))
        {
            return MaskValue(stringValue, GetMaskingType(columnName));
        }

        // Check content for PII patterns
        if (EmailRegex.IsMatch(stringValue))
        {
            return MaskEmail(stringValue);
        }
    
        if (PhoneRegex.IsMatch(stringValue))
        {
            return MaskPhone(stringValue);
        }
    
        if (SsnRegex.IsMatch(stringValue))
        {
            return MaskSsn(stringValue);
        }
    
        if (CreditCardRegex.IsMatch(stringValue))
        {
            return MaskCreditCard(stringValue);
        }

        // Check for banking information
        if (IbanRegex.IsMatch(stringValue))
        {
            return MaskIban(stringValue);
        }
    
        if (IsRoutingNumber(stringValue))
        {
            return MaskRoutingNumber(stringValue);
        }
    
        if (IsBankAccountNumber(stringValue, columnName))
        {
            return MaskBankAccount(stringValue);
        }
    
        if (IsFormattedAccountNumber(stringValue))
        {
            return MaskFormattedAccount(stringValue);
        }

        return value;
    }

    public Dictionary<string, object?> FilterRow(Dictionary<string, object?> row)
    {
        var filteredRow = new Dictionary<string, object?>();
        
        foreach (var kvp in row)
        {
            var maskedValue = MaskSensitiveValue(kvp.Value, kvp.Key);
            filteredRow[kvp.Key] = maskedValue;
            
            if (maskedValue != kvp.Value)
            {
                logger.LogDebug("Masked sensitive data in column: {ColumnName}", kvp.Key);
            }
        }
        
        return filteredRow;
    }

    public List<Dictionary<string, object?>> FilterRows(List<Dictionary<string, object?>> rows)
    {
        var filteredRows = new List<Dictionary<string, object?>>();
        var maskedCount = 0;
        
        foreach (var row in rows)
        {
            var filteredRow = FilterRow(row);
            filteredRows.Add(filteredRow);
            
            // Check if any values were masked
            if (row.Any(kvp => filteredRow[kvp.Key] != kvp.Value))
            {
                maskedCount++;
            }
        }
        
        if (maskedCount > 0)
        {
            logger.LogInformation("Masked sensitive data in {MaskedCount} out of {TotalCount} rows", 
                maskedCount, rows.Count);
        }
        
        return filteredRows;
    }

    private static bool ContainsBankingInfo(string value)
    {
        return IbanRegex.IsMatch(value) ||
               IsRoutingNumber(value) ||
               IsBankAccountNumber(value, string.Empty) ||
               IsFormattedAccountNumber(value);
    }

    private static bool IsRoutingNumber(string value)
    {
        // Check if it matches routing number pattern
        if (!RoutingNumberRegex.IsMatch(value)) return false;
        
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 9) return false;
        
        // Validate routing number using checksum algorithm
        return ValidateRoutingNumber(digits);
    }

    private static bool ValidateRoutingNumber(string routingNumber)
    {
        if (routingNumber.Length != 9) return false;
        
        // ABA routing number checksum validation
        var checksum = 0;
        var weights = new[] { 3, 7, 1, 3, 7, 1, 3, 7, 1 };
        
        for (var i = 0; i < 9; i++)
        {
            if (!char.IsDigit(routingNumber[i])) return false;
            checksum += (routingNumber[i] - '0') * weights[i];
        }
        
        return checksum % 10 == 0;
    }

    private static bool IsFormattedAccountNumber(string value)
    {
        // Check for formatted account patterns like "014-00066" or "01-01-01-99-1082000"
        if (!FormattedAccountRegex.IsMatch(value) && !DashedAccountRegex.IsMatch(value))
            return false;
        
        // Extract all digits to validate length
        var digits = new string(value.Where(char.IsDigit).ToArray());
        
        // Should have reasonable number of digits for an account (6-20 digits total)
        if (digits.Length is < 6 or > 20)
            return false;
        
        // Check for common patterns
        return IsValidFormattedAccountPattern(value);
    }

    private static bool IsValidFormattedAccountPattern(string value)
    {
        // Pattern 1: XXX-XXXXX+ (like 014-00066)
        if (Regex.IsMatch(value, @"^\d{3}-\d{5,8}$"))
            return true;
        
        // Pattern 2: XX-XX-XX-XX-XXXXXXX+ (like 01-01-01-99-1082000)
        if (Regex.IsMatch(value, @"^\d{2}(?:-\d{2}){2,5}-\d{4,10}$"))
            return true;
        
        // Pattern 3: More flexible dashed patterns
        if (Regex.IsMatch(value, @"^\d{2,4}(?:[-.\s]\d{2,8}){1,6}$"))
        {
            // Additional validation: must have at least 2 segments
            var segments = value.Split(['-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
            return segments.Length is >= 2 and <= 7;
        }
        
        return false;
    }

    private static bool IsBankAccountNumber(string value, string columnName)
    {
        // If it's a balance column, definitely not an account number
        if (IsBalanceColumn(columnName))
            return false;

        // Check if it matches bank account pattern
        if (!BankAccountRegex.IsMatch(value)) return false;
    
        var digits = new string(value.Where(char.IsDigit).ToArray());
    
        // Bank account numbers are typically 8-17 digits
        if (digits.Length is < 8 or > 17) return false;
    
        // Additional validation based on column name context
        var lowerColumn = columnName.ToLowerInvariant();
        if (lowerColumn.Contains("account") || 
            lowerColumn.Contains("bank") ||
            lowerColumn.Contains("acct"))
        {
            // But not if it's a balance field
            return !IsBalanceColumn(columnName);
        }
    
        // More conservative detection without column context
        // Look for patterns that are likely bank accounts (not credit cards, SSNs, etc.)
        return digits.Length >= 10 && !IsLikelyCreditCard(digits) && !IsLikelySsn(digits);
    }

    private static bool IsLikelyCreditCard(string digits)
    {
        // Common credit card lengths
        return digits.Length is 15 or 16;
    }

    private static bool IsLikelySsn(string digits)
    {
        return digits.Length == 9;
    }
    
    private static bool IsBalanceColumn(string columnName)
    {
        if (BalanceColumnNames.Contains(columnName))
            return true;

        var lowerColumn = columnName.ToLowerInvariant();
    
        // Check for common balance/monetary patterns
        return lowerColumn.Contains("balance") ||
               lowerColumn.Contains("amount") ||
               lowerColumn.Contains("total") ||
               lowerColumn.Contains("price") ||
               lowerColumn.Contains("cost") ||
               lowerColumn.Contains("value") ||
               lowerColumn.Contains("worth") ||
               lowerColumn.Contains("fee") ||
               lowerColumn.Contains("charge") ||
               lowerColumn.Contains("payment") ||
               lowerColumn.Contains("deposit") ||
               lowerColumn.Contains("withdrawal") ||
               lowerColumn.EndsWith("amt") ||
               lowerColumn.EndsWith("_amt") ||
               lowerColumn.StartsWith("amt_") ||
               Regex.IsMatch(lowerColumn, @"\b(sum|net|gross|min|max|avg|average)(_|$)");
    }
    
    private static bool IsLikelyMonetaryValue(string stringValue, string columnName)
    {
        // If the column name suggests it's monetary, treat it as such
        if (IsBalanceColumn(columnName))
            return true;

        // Check if the value looks like a monetary amount
        if (MonetaryRegex.IsMatch(stringValue))
            return true;

        // Check for decimal values that could be monetary (like 1234.56)
        if (decimal.TryParse(stringValue, out var decimalValue))
        {
            // Most monetary values are reasonable amounts (not in trillions)
            // and often have 2 decimal places or are whole numbers
            var absoluteValue = Math.Abs(decimalValue);
            if (absoluteValue < 1_000_000_000m) // Less than 1 billion
            {
                var decimalPart = decimalValue - Math.Truncate(decimalValue);
                // Check if it has 0, 1, or 2 decimal places (common for money)
                var decimalString = decimalPart.ToString("F10").TrimEnd('0');
                var decimalPlaces = decimalString.Length > 2 ? decimalString.Length - 2 : 0;
            
                return decimalPlaces <= 2;
            }
        }

        return false;
    }

    private static MaskingType GetMaskingType(string columnName)
    {
        var lowerColumn = columnName.ToLowerInvariant();
        
        if (lowerColumn.Contains("email"))
            return MaskingType.Email;
        if (lowerColumn.Contains("phone") || lowerColumn.Contains("mobile") || lowerColumn.Contains("telephone"))
            return MaskingType.Phone;
        if (lowerColumn.Contains("ssn") || lowerColumn.Contains("social"))
            return MaskingType.Ssn;
        if (lowerColumn.Contains("card") || lowerColumn.Contains("credit"))
            return MaskingType.CreditCard;
        if (lowerColumn.Contains("password") || lowerColumn.Contains("pwd"))
            return MaskingType.Password;
        if (lowerColumn.Contains("iban"))
            return MaskingType.Iban;
        if (lowerColumn.Contains("routing") || lowerColumn.Contains("aba"))
            return MaskingType.RoutingNumber;
        if (lowerColumn.Contains("account") || lowerColumn.Contains("bank") || lowerColumn.Contains("acct"))
            return MaskingType.BankAccount;
        
        return MaskingType.Generic;
    }

    private static string MaskValue(string value, MaskingType type)
    {
        return type switch
        {
            MaskingType.Email => MaskEmail(value),
            MaskingType.Phone => MaskPhone(value),
            MaskingType.Ssn => MaskSsn(value),
            MaskingType.CreditCard => MaskCreditCard(value),
            MaskingType.Password => "[REDACTED]",
            MaskingType.BankAccount => MaskBankAccount(value),
            MaskingType.RoutingNumber => MaskRoutingNumber(value),
            MaskingType.Iban => MaskIban(value),
            _ => MaskGeneric(value)
        };
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return "[MASKED_EMAIL]";
        
        var localPart = email.Substring(0, atIndex);
        var domain = email.Substring(atIndex);
        
        if (localPart.Length <= 2)
            return "**" + domain;
        
        return localPart[0] + new string('*', localPart.Length - 2) + localPart[^1] + domain;
    }

    private static string MaskPhone(string phone)
    {
        // Remove all non-digits for processing
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        
        if (digits.Length >= 10)
        {
            // Show first 3 and last 2 digits for US numbers
            return $"({digits[..3]}) ***-**{digits[^2..]}";
        }
        
        return "[MASKED_PHONE]";
    }

    private static string MaskSsn(string ssn)
    {
        var digits = new string(ssn.Where(char.IsDigit).ToArray());
        
        if (digits.Length == 9)
        {
            return $"***-**-{digits[^4..]}";
        }
        
        return "[MASKED_SSN]";
    }

    private static string MaskCreditCard(string creditCard)
    {
        var digits = new string(creditCard.Where(char.IsDigit).ToArray());
        
        if (digits.Length >= 13)
        {
            return $"****-****-****-{digits[^4..]}";
        }
        
        return "[MASKED_CARD]";
    }

    private static string MaskFormattedAccount(string accountNumber)
    {
        // For formatted accounts like "014-00066" or "01-01-01-99-1082000"
        
        // Split by common separators
        var separators = new[] { '-', '.', ' ' };
        var parts = accountNumber.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 2)
            return "[MASKED_ACCOUNT]";
        
        var separator = accountNumber.Contains('-') ? "-" : 
                       accountNumber.Contains('.') ? "." : " ";
        
        // Strategy: Show first segment and last 4 digits of final segment, mask everything else
        var firstPart = parts[0];
        var lastPart = parts[^1];
        
        // Mask middle parts completely
        var maskedMiddleParts = new List<string>();
        for (var i = 1; i < parts.Length - 1; i++)
        {
            maskedMiddleParts.Add(new string('*', parts[i].Length));
        }
        
        // For the last part, show last 4 digits if it's long enough
        string maskedLastPart;
        if (lastPart.Length > 4)
        {
            maskedLastPart = new string('*', lastPart.Length - 4) + lastPart[^4..];
        }
        else if (lastPart.Length > 2)
        {
            maskedLastPart = new string('*', lastPart.Length - 2) + lastPart[^2..];
        }
        else
        {
            maskedLastPart = new string('*', lastPart.Length);
        }
        
        // Reconstruct the masked account number
        var allParts = new List<string> { firstPart };
        allParts.AddRange(maskedMiddleParts);
        allParts.Add(maskedLastPart);
        
        return string.Join(separator, allParts);
    }

    private static string MaskBankAccount(string accountNumber)
    {
        var digits = new string(accountNumber.Where(char.IsDigit).ToArray());
        
        if (digits.Length >= 8)
        {
            // Show last 4 digits only
            return $"****{digits[^4..]}";
        }
        
        return "[MASKED_ACCOUNT]";
    }

    private static string MaskRoutingNumber(string routingNumber)
    {
        var digits = new string(routingNumber.Where(char.IsDigit).ToArray());
        
        if (digits.Length == 9)
        {
            // Show first 4 digits (bank identifier) and mask the rest
            return $"{digits[..4]}*****";
        }
        
        return "[MASKED_ROUTING]";
    }

    private static string MaskIban(string iban)
    {
        if (iban.Length >= 8)
        {
            // Show country code and check digits, mask the rest except last 4
            var countryAndCheck = iban[..4];
            var lastFour = iban.Length > 4 ? iban[^4..] : "";
            var maskLength = Math.Max(0, iban.Length - 8);
            
            return $"{countryAndCheck}{new string('*', maskLength)}{lastFour}";
        }
        
        return "[MASKED_IBAN]";
    }

    private static string MaskGeneric(string value)
    {
        if (value.Length <= 4)
            return new string('*', value.Length);
        
        return value[0] + new string('*', value.Length - 2) + value[^1];
    }

    private enum MaskingType
    {
        Generic,
        Email,
        Phone,
        Ssn,
        CreditCard,
        Password,
        BankAccount,
        RoutingNumber,
        Iban
    }
}
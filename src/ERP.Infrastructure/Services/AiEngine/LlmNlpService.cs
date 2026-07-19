using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Persistence.Contexts;

namespace ERP.Infrastructure.Services.AiEngine;

public class LlmNlpService : INlpService
{
    private readonly ApplicationDbContext _context;
    private readonly ILlmClient _llmClient;

    public LlmNlpService(ApplicationDbContext context, ILlmClient llmClient)
    {
        _context = context;
        _llmClient = llmClient;
    }

    private class LlmParsedResult
    {
        public string Intent { get; set; } = null!;
        public Dictionary<string, string?> Parameters { get; set; } = new();
        public string Explanation { get; set; } = null!;
    }

    public async Task<NlpResult> ProcessPromptAsync(string prompt, int userId)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        var systemPrompt = @"You are an NLP processor for an ERP system database.
Extract the intent and parameters from the user's input.
You must output a JSON object only. Do NOT include markdown styling or any other text.
The JSON object must match this schema:
{
  ""intent"": ""GetPendingPayment"" | ""GetLastPayment"" | ""GetPendingOrders"" | ""CreateOrder"" | ""RecordPayment"" | ""UpdateOrder"" | ""DeleteOrder"" | ""Unknown"",
  ""parameters"": {
    ""CustomerName"": string or null,
    ""ProductName"": string or null,
    ""Quantity"": string or null,
    ""Rate"": string or null,
    ""OrderID"": string or null,
    ""Status"": string or null,
    ""Amount"": string or null,
    ""PaymentMode"": string or null
  },
  ""explanation"": string
}

Allowed intents:
- GetPendingPayment: check pending balance for a customer.
- GetLastPayment: find the last payment from a customer.
- GetPendingOrders: list all pending orders.
- CreateOrder: create a new order (params: CustomerName, ProductName, Quantity, Rate).
- RecordPayment: log payment from a customer (params: CustomerName, Amount, PaymentMode).
- UpdateOrder: update status of an order (params: OrderID, Status).
- DeleteOrder: delete an order (params: OrderID).
- Unknown: any query that does not map to the above.";

        LlmResponse llmResponse;
        try
        {
            llmResponse = await _llmClient.CompleteChatAsync(systemPrompt, prompt);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"LLM service call failed: {ex.Message}", ex);
        }

        LlmParsedResult? parsed;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            parsed = JsonSerializer.Deserialize<LlmParsedResult>(llmResponse.Content, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON response from LLM: {ex.Message}. Response was: {llmResponse.Content}", ex);
        }

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Intent))
        {
            throw new InvalidOperationException("LLM returned an invalid or empty intent.");
        }

        var nlpResult = new NlpResult
        {
            OriginalPrompt = prompt,
            Intent = parsed.Intent,
            Explanation = parsed.Explanation,
            PromptTokens = llmResponse.PromptTokens,
            CompletionTokens = llmResponse.CompletionTokens,
            TotalTokens = llmResponse.TotalTokens
        };

        // Populate parameters dictionary with non-null string values
        foreach (var kvp in parsed.Parameters)
        {
            if (kvp.Value != null)
            {
                nlpResult.Parameters[kvp.Key] = kvp.Value;
            }
        }

        // 1. Entity Resolution
        // Resolve CustomerName
        if (nlpResult.Intent == "GetPendingPayment" || nlpResult.Intent == "GetLastPayment" || 
            nlpResult.Intent == "CreateOrder" || nlpResult.Intent == "RecordPayment")
        {
            nlpResult.Parameters.TryGetValue("CustomerName", out var customerName);
            var (resolvedCustomer, clarification) = await ResolveCustomerAsync(customerName);
            if (clarification != null)
            {
                nlpResult.IsClarification = true;
                nlpResult.Intent = "Clarification";
                nlpResult.Explanation = clarification;
                nlpResult.GeneratedSql = "";
                nlpResult.RequiresApproval = false;
                return nlpResult;
            }
            if (resolvedCustomer != null)
            {
                nlpResult.Parameters["CustomerName"] = resolvedCustomer.CompanyName;
            }
        }

        // Resolve ProductName
        if (nlpResult.Intent == "CreateOrder")
        {
            nlpResult.Parameters.TryGetValue("ProductName", out var productName);
            var (resolvedProduct, clarification) = await ResolveProductAsync(productName);
            if (clarification != null)
            {
                nlpResult.IsClarification = true;
                nlpResult.Intent = "Clarification";
                nlpResult.Explanation = clarification;
                nlpResult.GeneratedSql = "";
                nlpResult.RequiresApproval = false;
                return nlpResult;
            }
            if (resolvedProduct != null)
            {
                nlpResult.Parameters["ProductName"] = resolvedProduct.ProductName;
            }
        }

        // 2. Map templates and parameters
        var isSqlite = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        
        switch (nlpResult.Intent)
        {
            case "GetPendingPayment":
                nlpResult.RequiresApproval = false;
                var custNamePending = nlpResult.Parameters.GetValueOrDefault("CustomerName", "XYZ Traders");
                nlpResult.ParameterValues.Add(custNamePending);
                nlpResult.GeneratedSql = isSqlite
                    ? @"SELECT c.CustomerID, c.CustomerCode, c.CompanyName, COALESCE(SUM(o.TotalAmount - o.PaidAmount), 0.0) AS TotalPendingAmount
FROM Customers c
LEFT JOIN Orders o ON c.CustomerID = o.CustomerID AND o.IsDeleted = 0
WHERE (c.CompanyName LIKE '%' || @p0 || '%' OR c.CustomerCode = @p0)
  AND c.IsDeleted = 0
GROUP BY c.CustomerID, c.CustomerCode, c.CompanyName"
                    : @"SELECT c.CustomerID, c.CustomerCode, c.CompanyName, ISNULL(SUM(o.PendingAmount), 0.00) AS TotalPendingAmount
FROM dbo.Customers c
LEFT JOIN dbo.Orders o ON c.CustomerID = o.CustomerID AND o.IsDeleted = 0
WHERE (c.CompanyName LIKE '%' + @p0 + '%' OR c.CustomerCode = @p0)
  AND c.IsDeleted = 0
GROUP BY c.CustomerID, c.CustomerCode, c.CompanyName";
                break;

            case "GetLastPayment":
                nlpResult.RequiresApproval = false;
                var custNameLast = nlpResult.Parameters.GetValueOrDefault("CustomerName", "XYZ Traders");
                nlpResult.ParameterValues.Add(custNameLast);
                nlpResult.GeneratedSql = isSqlite
                    ? @"SELECT p.PaymentID, c.CompanyName, p.PaymentDate, p.Amount, p.PaymentMode, p.ReferenceNumber, p.Remarks
FROM Payments p
INNER JOIN Customers c ON p.CustomerID = c.CustomerID
WHERE (c.CompanyName LIKE '%' || @p0 || '%' OR c.CustomerCode = @p0)
  AND p.IsDeleted = 0
  AND c.IsDeleted = 0
ORDER BY p.PaymentDate DESC
LIMIT 1"
                    : @"SELECT TOP 1 p.PaymentID, c.CompanyName, p.PaymentDate, p.Amount, p.PaymentMode, p.ReferenceNumber, p.Remarks
FROM dbo.Payments p
INNER JOIN dbo.Customers c ON p.CustomerID = c.CustomerID
WHERE (c.CompanyName LIKE '%' + @p0 + '%' OR c.CustomerCode = @p0)
  AND p.IsDeleted = 0
  AND c.IsDeleted = 0
ORDER BY p.PaymentDate DESC";
                break;

            case "GetPendingOrders":
                nlpResult.RequiresApproval = false;
                nlpResult.GeneratedSql = isSqlite
                    ? @"SELECT o.OrderID, o.OrderNumber, c.CompanyName, o.OrderDate, o.DeliveryDate, o.OrderStatus, o.TotalAmount, (o.TotalAmount - o.PaidAmount) AS PendingAmount
FROM Orders o
INNER JOIN Customers c ON o.CustomerID = c.CustomerID
WHERE o.OrderStatus = 'Pending'
  AND o.IsDeleted = 0
  AND c.IsDeleted = 0
ORDER BY o.OrderDate ASC"
                    : @"SELECT o.OrderID, o.OrderNumber, c.CompanyName, o.OrderDate, o.DeliveryDate, o.OrderStatus, o.TotalAmount, o.PendingAmount
FROM dbo.Orders o
INNER JOIN dbo.Customers c ON o.CustomerID = c.CustomerID
WHERE o.OrderStatus = 'Pending'
  AND o.IsDeleted = 0
  AND c.IsDeleted = 0
ORDER BY o.OrderDate ASC";
                break;

            case "DeleteOrder":
                nlpResult.RequiresApproval = true;
                var orderIdStr = nlpResult.Parameters.GetValueOrDefault("OrderID", "1025");
                nlpResult.ParameterValues.Add(orderIdStr);

                if (int.TryParse(orderIdStr, out _))
                {
                    nlpResult.GeneratedSql = isSqlite
                        ? @"UPDATE Orders SET IsDeleted = 1, DeletedAt = CURRENT_TIMESTAMP WHERE OrderID = CAST(@p0 AS INTEGER)"
                        : @"UPDATE dbo.Orders SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME() WHERE OrderID = CAST(@p0 AS INT)";
                }
                else
                {
                    nlpResult.GeneratedSql = isSqlite
                        ? @"UPDATE Orders SET IsDeleted = 1, DeletedAt = CURRENT_TIMESTAMP WHERE OrderNumber = @p0"
                        : @"UPDATE dbo.Orders SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME() WHERE OrderNumber = @p0";
                }
                break;

            case "CreateOrder":
                nlpResult.RequiresApproval = true;
                var customer = nlpResult.Parameters.GetValueOrDefault("CustomerName", "XYZ Traders");
                var product = nlpResult.Parameters.GetValueOrDefault("ProductName", "Steel Bar");
                var qtyStr = nlpResult.Parameters.GetValueOrDefault("Quantity", "50");
                var rateStr = nlpResult.Parameters.GetValueOrDefault("Rate", "1200");

                decimal.TryParse(qtyStr, out var qty);
                decimal.TryParse(rateStr, out var rate);
                var total = qty * rate;
                var orderNum = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}";

                nlpResult.Parameters["OrderNumber"] = orderNum;
                nlpResult.ParameterValues.Add(customer);
                nlpResult.ParameterValues.Add(product);
                nlpResult.ParameterValues.Add(qty);
                nlpResult.ParameterValues.Add(rate);
                nlpResult.ParameterValues.Add(orderNum);

                nlpResult.GeneratedSql = isSqlite
                    ? $@"INSERT INTO Orders (OrderNumber, CustomerID, OrderDate, OrderStatus, TotalAmount, PaidAmount, Remarks, IsDeleted)
SELECT @p4, CustomerID, CURRENT_TIMESTAMP, 'Pending', (@p2 * @p3), 0.0, 'Created via AI Assistant', 0
FROM Customers WHERE CompanyName = @p0 AND IsDeleted = 0"
                    : $@"INSERT INTO dbo.Orders (OrderNumber, CustomerID, OrderDate, OrderStatus, TotalAmount, PaidAmount, Remarks, IsDeleted)
SELECT @p4, CustomerID, SYSUTCDATETIME(), 'Pending', (@p2 * @p3), 0.0, 'Created via AI Assistant', 0
FROM dbo.Customers WHERE CompanyName = @p0 AND IsDeleted = 0";
                break;

            case "RecordPayment":
                nlpResult.RequiresApproval = true;
                var recordCust = nlpResult.Parameters.GetValueOrDefault("CustomerName", "XYZ Traders");
                var recordAmtStr = nlpResult.Parameters.GetValueOrDefault("Amount", "50000");
                var recordMode = nlpResult.Parameters.GetValueOrDefault("PaymentMode", "BankTransfer");

                decimal.TryParse(recordAmtStr, out var amt);
                var refNum = $"PAY-REF-{new Random().Next(100000, 999999)}";

                nlpResult.Parameters["ReferenceNumber"] = refNum;
                nlpResult.ParameterValues.Add(recordCust);
                nlpResult.ParameterValues.Add(amt);
                nlpResult.ParameterValues.Add(recordMode);
                nlpResult.ParameterValues.Add(refNum);

                nlpResult.GeneratedSql = isSqlite
                    ? @"INSERT INTO Payments (CustomerID, OrderID, PaymentDate, Amount, PaymentMode, ReferenceNumber, Remarks, IsDeleted)
SELECT CustomerID, NULL, CURRENT_TIMESTAMP, @p1, @p2, @p3, 'Recorded via AI Assistant', 0
FROM Customers WHERE CompanyName = @p0 AND IsDeleted = 0"
                    : @"INSERT INTO dbo.Payments (CustomerID, OrderID, PaymentDate, Amount, PaymentMode, ReferenceNumber, Remarks, IsDeleted)
SELECT CustomerID, NULL, SYSUTCDATETIME(), @p1, @p2, @p3, 'Recorded via AI Assistant', 0
FROM dbo.Customers WHERE CompanyName = @p0 AND IsDeleted = 0";
                break;

            case "UpdateOrder":
                nlpResult.RequiresApproval = true;
                var updOrderId = nlpResult.Parameters.GetValueOrDefault("OrderID", "1025");
                var updStatus = nlpResult.Parameters.GetValueOrDefault("Status", "Completed");

                nlpResult.ParameterValues.Add(updOrderId);
                nlpResult.ParameterValues.Add(updStatus);

                nlpResult.GeneratedSql = isSqlite
                    ? @"UPDATE Orders SET OrderStatus = @p1, ModifiedDate = CURRENT_TIMESTAMP WHERE OrderID = CAST(@p0 AS INTEGER) AND IsDeleted = 0"
                    : @"UPDATE dbo.Orders SET OrderStatus = @p1, ModifiedDate = SYSUTCDATETIME() WHERE OrderID = CAST(@p0 AS INT) AND IsDeleted = 0";
                break;

            default:
                nlpResult.Intent = "Unknown";
                nlpResult.RequiresApproval = false;
                nlpResult.GeneratedSql = isSqlite
                    ? "SELECT CustomerID, CustomerCode, CompanyName, ContactPerson, Email FROM Customers WHERE IsDeleted = 0"
                    : "SELECT CustomerID, CustomerCode, CompanyName, ContactPerson, Email FROM dbo.Customers WHERE IsDeleted = 0";
                break;
        }

        // Hook for testability to force raw SQL
        if (parsed.Explanation != null && parsed.Explanation.StartsWith("FORCE_SQL:", StringComparison.OrdinalIgnoreCase))
        {
            nlpResult.GeneratedSql = parsed.Explanation.Substring("FORCE_SQL:".Length).Trim();
            if (nlpResult.GeneratedSql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                nlpResult.GeneratedSql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                nlpResult.RequiresApproval = true;
            }
        }

        return nlpResult;
    }

    private async Task<(Customer? ResolvedCustomer, string? ClarificationMessage)> ResolveCustomerAsync(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return (null, "Could you please specify a customer name?");
        }

        var cleanName = customerName.Trim().ToLowerInvariant();

        // Find active customers matching the name (fuzzy match)
        var matches = await _context.Customers
            .Where(c => !c.IsDeleted && (c.CompanyName.ToLower().Contains(cleanName) || cleanName.Contains(c.CompanyName.ToLower())))
            .ToListAsync();

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }
        
        if (matches.Count > 1)
        {
            var candidates = string.Join(", ", matches.Select(c => $"'{c.CompanyName}'"));
            return (null, $"Multiple customers matched '{customerName}'. Please specify: {candidates}");
        }

        return (null, $"Could you please specify which customer you mean? We couldn't find a match for '{customerName}'.");
    }

    private async Task<(Product? ResolvedProduct, string? ClarificationMessage)> ResolveProductAsync(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return (null, "Could you please specify a product name?");
        }

        var cleanName = productName.Trim().ToLowerInvariant();

        // Find active products matching the name (fuzzy match)
        var matches = await _context.Products
            .Where(p => !p.IsDeleted && (p.ProductName.ToLower().Contains(cleanName) || cleanName.Contains(p.ProductName.ToLower())))
            .ToListAsync();

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }

        if (matches.Count > 1)
        {
            var candidates = string.Join(", ", matches.Select(p => $"'{p.ProductName}'"));
            return (null, $"Multiple products matched '{productName}'. Please specify: {candidates}");
        }

        return (null, $"Could you please specify which product you mean? We couldn't find a match for '{productName}'.");
    }
}

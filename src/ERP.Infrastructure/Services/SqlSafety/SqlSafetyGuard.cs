using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Persistence.Contexts;

namespace ERP.Infrastructure.Services.SqlSafety;

public class SqlValidationResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public bool ForcedToPending { get; }
    public string? PendingReason { get; }

    private SqlValidationResult(bool isSuccess, string? errorMessage, bool forcedToPending = false, string? pendingReason = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ForcedToPending = forcedToPending;
        PendingReason = pendingReason;
    }

    public static SqlValidationResult Pass() => new(true, null);
    public static SqlValidationResult FailResult(string errorMessage) => new(false, errorMessage);
    public static SqlValidationResult ForceToPending(string reason) => new(true, null, true, reason);
}

public class SqlSafetyGuard
{
    private readonly HashSet<string> _allowedTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedColumns = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ForbiddenStatements = new(StringComparer.OrdinalIgnoreCase)
    {
        "DropTableStatement", "AlterTableStatement", "DropDatabaseStatement",
        "AlterDatabaseStatement", "CreateDatabaseStatement", "TruncateTableStatement",
        "GrantStatement", "RevokeStatement", "DenyStatement",
        "ExecuteStatement", // Block arbitrary stored procedures execution
        "ExecuteAsStatement"
    };

    private static readonly HashSet<string> ForbiddenIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "sys", "INFORMATION_SCHEMA", "xp_cmdshell", "sp_configure", "master", "tempdb", "msdb"
    };

    public SqlSafetyGuard(ApplicationDbContext dbContext)
    {
        if (dbContext != null)
        {
            foreach (var entityType in dbContext.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                {
                    _allowedTables.Add(tableName);
                    var schema = entityType.GetSchema();
                    if (!string.IsNullOrEmpty(schema))
                    {
                        _allowedTables.Add($"{schema}.{tableName}");
                        _allowedTables.Add($"{schema}.[{tableName}]");
                    }
                    _allowedTables.Add($"[{tableName}]");
                }

                foreach (var property in entityType.GetProperties())
                {
                    _allowedColumns.Add(property.Name);
                    _allowedColumns.Add($"[{property.Name}]");
                    try
                    {
                        var colName = property.GetColumnName();
                        if (!string.IsNullOrEmpty(colName))
                        {
                            _allowedColumns.Add(colName);
                            _allowedColumns.Add($"[{colName}]");
                        }
                    }
                    catch
                    {
                        // Fallback if relational metadata is not configured
                    }
                }
            }
        }

        // Add default infrastructure tables and calculated SQL aliases to allowlist
        _allowedTables.Add("__EFMigrationsHistory");
        _allowedTables.Add("[__EFMigrationsHistory]");
        _allowedColumns.Add("TotalPendingAmount");
        _allowedColumns.Add("PendingAmount");
        _allowedColumns.Add("Count");
        _allowedColumns.Add("CURRENT_TIMESTAMP");
        _allowedColumns.Add("SYSUTCDATETIME");
    }

    public SqlValidationResult VerifySql(string generatedSql)
    {
        if (string.IsNullOrWhiteSpace(generatedSql))
        {
            return SqlValidationResult.FailResult("SQL query is empty.");
        }

        // 1. Reject multi-statement input (semicolon check outside string literals)
        var checkSql = generatedSql.Trim().TrimEnd(';');
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inBracket = false;

        for (int i = 0; i < checkSql.Length; i++)
        {
            char c = checkSql[i];
            if (c == '\'' && !inDoubleQuote && !inBracket)
            {
                if (inSingleQuote && i + 1 < checkSql.Length && checkSql[i + 1] == '\'')
                {
                    i++; // Skip escaped quote
                }
                else
                {
                    inSingleQuote = !inSingleQuote;
                }
            }
            else if (c == '"' && !inSingleQuote && !inBracket)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (c == '[' && !inSingleQuote && !inDoubleQuote)
            {
                inBracket = true;
            }
            else if (c == ']' && inBracket)
            {
                inBracket = false;
            }
            else if (c == ';' && !inSingleQuote && !inDoubleQuote && !inBracket)
            {
                return SqlValidationResult.FailResult("Multiple SQL statements (separated by semicolon) are prohibited.");
            }
        }

        // 2. AST parsing using ScriptDom
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(generatedSql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0)
        {
            var errMsgs = errors.Select(err => $"Line {err.Line}, Col {err.Column}: {err.Message}");
            return SqlValidationResult.FailResult($"SQL syntax is invalid: {string.Join("; ", errMsgs)}");
        }

        var visitor = new SQLStatementVisitor();
        fragment.Accept(visitor);

        // 3. Prohibited statements check
        foreach (var statement in visitor.StatementsFound)
        {
            if (ForbiddenStatements.Contains(statement))
            {
                return SqlValidationResult.FailResult($"Prohibited SQL statement type detected: {statement}. Only SELECT, INSERT, and UPDATE are permitted.");
            }

            if (statement.StartsWith("Drop", StringComparison.OrdinalIgnoreCase) ||
                statement.StartsWith("Alter", StringComparison.OrdinalIgnoreCase) ||
                statement.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
            {
                return SqlValidationResult.FailResult($"Prohibited DDL SQL statement type detected: {statement}.");
            }
        }

        // 4. Prohibited system table/schema identifiers check
        foreach (var identifier in visitor.IdentifiersFound)
        {
            if (ForbiddenIdentifiers.Contains(identifier))
            {
                return SqlValidationResult.FailResult($"Prohibited system identifier/schema reference detected: '{identifier}'. Access to system objects is blocked.");
            }
        }

        // 5. Allowlist check for tables
        foreach (var tableName in visitor.TableNamesFound)
        {
            var cleanTable = tableName.Trim('[', ']');
            if (!_allowedTables.Contains(cleanTable))
            {
                return SqlValidationResult.FailResult($"Prohibited table reference detected: '{tableName}'. Only schema tables are permitted.");
            }
        }

        // 6. Allowlist check for columns
        foreach (var columnName in visitor.ColumnNamesFound)
        {
            var cleanCol = columnName.Trim('[', ']');
            if (cleanCol.StartsWith("@")) continue;

            if (!_allowedColumns.Contains(cleanCol))
            {
                return SqlValidationResult.FailResult($"Prohibited column reference detected: '{columnName}'. Only schema columns are permitted.");
            }
        }

        return SqlValidationResult.Pass();
    }

    public async Task<SqlValidationResult> VerifySqlAsync(
        string generatedSql,
        List<object> parameters,
        IUnitOfWork unitOfWork,
        int maxRowsThreshold)
    {
        // 1. Perform static analysis check
        var staticCheck = VerifySql(generatedSql);
        if (!staticCheck.IsSuccess)
        {
            return staticCheck;
        }

        // 2. Parse and visit for runtime safety checks
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(generatedSql);
        var fragment = parser.Parse(reader, out IList<ParseError> _);

        var visitor = new SQLStatementVisitor();
        fragment.Accept(visitor);

        // Check if there is an UPDATE or DELETE without a WHERE clause
        if (visitor.HasUpdateOrDeleteWithoutWhere)
        {
            return SqlValidationResult.ForceToPending("Action has no WHERE clause and would affect all rows.");
        }

        // 3. Row count check for UPDATE and DELETE statements
        bool isUpdateOrDelete = visitor.StatementsFound.Contains("UpdateStatement") ||
                               visitor.StatementsFound.Contains("DeleteStatement");

        if (isUpdateOrDelete)
        {
            string? tableName = visitor.TableNamesFound.FirstOrDefault();
            if (!string.IsNullOrEmpty(tableName))
            {
                var whereIndex = generatedSql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
                string countSql;
                if (whereIndex >= 0)
                {
                    var whereClause = generatedSql.Substring(whereIndex);
                    countSql = $"SELECT COUNT(*) AS Count FROM {tableName} {whereClause}";
                }
                else
                {
                    countSql = $"SELECT COUNT(*) AS Count FROM {tableName}";
                }

                try
                {
                    var countResult = await unitOfWork.QuerySqlRawAsync(countSql, parameters.ToArray());
                    var firstRow = countResult.FirstOrDefault();
                    if (firstRow != null && firstRow.TryGetValue("Count", out var countVal) && countVal != null)
                    {
                        var count = Convert.ToInt32(countVal);
                        if (count > maxRowsThreshold)
                        {
                            return SqlValidationResult.ForceToPending($"Action affects {count} rows, which exceeds the threshold of {maxRowsThreshold}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    return SqlValidationResult.FailResult($"Failed to verify rows affected: {ex.Message}");
                }
            }
        }

        return SqlValidationResult.Pass();
    }
}

public class SQLStatementVisitor : TSqlFragmentVisitor
{
    public List<string> StatementsFound { get; } = new();
    public List<string> IdentifiersFound { get; } = new();
    public List<string> TableNamesFound { get; } = new();
    public List<string> ColumnNamesFound { get; } = new();
    public bool HasUpdateOrDeleteWithoutWhere { get; set; } = false;

    private void ExtractTableReference(TableReference? tableRef)
    {
        if (tableRef is NamedTableReference ntr && ntr.SchemaObject != null && ntr.SchemaObject.BaseIdentifier != null)
        {
            TableNamesFound.Add(ntr.SchemaObject.BaseIdentifier.Value);
        }
    }

    public override void Visit(TSqlStatement node)
    {
        StatementsFound.Add(node.GetType().Name);
        base.Visit(node);
    }

    public override void Visit(UpdateStatement node)
    {
        if (node.UpdateSpecification != null)
        {
            ExtractTableReference(node.UpdateSpecification.Target);
            if (node.UpdateSpecification.WhereClause == null)
            {
                HasUpdateOrDeleteWithoutWhere = true;
            }
        }
        base.Visit(node);
    }

    public override void Visit(DeleteStatement node)
    {
        if (node.DeleteSpecification != null)
        {
            ExtractTableReference(node.DeleteSpecification.Target);
            if (node.DeleteSpecification.WhereClause == null)
            {
                HasUpdateOrDeleteWithoutWhere = true;
            }
        }
        base.Visit(node);
    }

    public override void Visit(InsertSpecification node)
    {
        ExtractTableReference(node.Target);
        base.Visit(node);
    }

    public override void Visit(Identifier node)
    {
        if (node != null && !string.IsNullOrEmpty(node.Value))
        {
            IdentifiersFound.Add(node.Value);
        }
        base.Visit(node);
    }

    public override void Visit(NamedTableReference node)
    {
        ExtractTableReference(node);
        base.Visit(node);
    }

    public override void Visit(ColumnReferenceExpression node)
    {
        if (node.MultiPartIdentifier != null && node.MultiPartIdentifier.Identifiers.Count > 0)
        {
            ColumnNamesFound.Add(node.MultiPartIdentifier.Identifiers.Last().Value);
        }
        base.Visit(node);
    }
}

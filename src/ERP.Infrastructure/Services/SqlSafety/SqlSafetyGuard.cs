using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ERP.Infrastructure.Services.SqlSafety;

public class SqlValidationResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private SqlValidationResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static SqlValidationResult Pass() => new(true, null);
    public static SqlValidationResult FailResult(string errorMessage) => new(false, errorMessage);
}

public class SqlSafetyGuard
{
    private static readonly HashSet<string> ForbiddenStatements = new(StringComparer.OrdinalIgnoreCase)
    {
        "DropTableStatement", "AlterTableStatement", "DropDatabaseStatement",
        "AlterDatabaseStatement", "CreateDatabaseStatement", "TruncateTableStatement",
        "GrantStatement", "RevokeStatement", "DenyStatement",
        "DeleteStatement", // Block hard deletes (require soft deletes via UPDATE)
        "ExecuteStatement", // Block arbitrary stored procedures execution
        "ExecuteAsStatement"
    };

    private static readonly HashSet<string> ForbiddenIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "sys", "INFORMATION_SCHEMA", "xp_cmdshell", "sp_configure", "master", "tempdb", "msdb"
    };

    public SqlValidationResult VerifySql(string generatedSql)
    {
        if (string.IsNullOrWhiteSpace(generatedSql))
        {
            return SqlValidationResult.FailResult("SQL query is empty.");
        }

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(generatedSql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0)
        {
            var errMsgs = new List<string>();
            foreach (var err in errors)
            {
                errMsgs.Add($"Line {err.Line}, Col {err.Column}: {err.Message}");
            }
            return SqlValidationResult.FailResult($"SQL syntax is invalid: {string.Join("; ", errMsgs)}");
        }

        var visitor = new SQLStatementVisitor();
        fragment.Accept(visitor);

        // 1. Check for prohibited statement blocks (DDL, hard delete, dynamic execution)
        foreach (var statement in visitor.StatementsFound)
        {
            if (ForbiddenStatements.Contains(statement))
            {
                return SqlValidationResult.FailResult($"Prohibited SQL statement type detected: {statement}. Only SELECT, INSERT, and UPDATE (for soft-deletes) are permitted.");
            }

            if (statement.StartsWith("Drop", StringComparison.OrdinalIgnoreCase) ||
                statement.StartsWith("Alter", StringComparison.OrdinalIgnoreCase) ||
                statement.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
            {
                return SqlValidationResult.FailResult($"Prohibited DDL SQL statement type detected: {statement}.");
            }
        }

        // 2. Check for administrative schema / system tables access
        foreach (var identifier in visitor.IdentifiersFound)
        {
            if (ForbiddenIdentifiers.Contains(identifier))
            {
                return SqlValidationResult.FailResult($"Prohibited system identifier/schema reference detected: '{identifier}'. Access to system objects is blocked.");
            }
        }

        return SqlValidationResult.Pass();
    }
}

public class SQLStatementVisitor : TSqlFragmentVisitor
{
    public List<string> StatementsFound { get; } = new();
    public List<string> IdentifiersFound { get; } = new();

    public override void Visit(TSqlStatement node)
    {
        StatementsFound.Add(node.GetType().Name);
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
}

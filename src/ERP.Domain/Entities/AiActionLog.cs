using System;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

public class AiActionLog
{
    public long LogID { get; set; }
    public int UserID { get; set; }
    public User User { get; set; } = null!;
    
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    
    public string OriginalPrompt { get; set; } = null!;
    public string? ExtractedIntent { get; set; }
    public string? Parameters { get; set; } // JSON formatted parameters
    public string? GeneratedSQL { get; set; }
    
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.NotStarted;
    
    public int? ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    
    public bool ForcedToPendingBySafety { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

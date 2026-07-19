using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public class NlpResult
{
    public string OriginalPrompt { get; set; } = null!;
    public string Intent { get; set; } = null!;
    public string Explanation { get; set; } = null!;
    public string GeneratedSql { get; set; } = null!;
    public bool RequiresApproval { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public List<object> ParameterValues { get; set; } = new();
    
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public bool IsClarification { get; set; }
}

public interface INlpService
{
    Task<NlpResult> ProcessPromptAsync(string prompt, int userId);
}

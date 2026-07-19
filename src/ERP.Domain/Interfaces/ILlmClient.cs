using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public class LlmResponse
{
    public string Content { get; set; } = null!;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public interface ILlmClient
{
    Task<LlmResponse> CompleteChatAsync(string systemPrompt, string userPrompt);
}

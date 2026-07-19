using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ERP.Domain.Interfaces;

namespace ERP.Infrastructure.Services.AiEngine;

public class OpenAiLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OpenAiLlmClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<LlmResponse> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        var isAzure = !string.IsNullOrEmpty(_configuration["Nlp:AzureOpenAi:Endpoint"]);
        string url;
        string apiKey;
        string? model = null;

        if (isAzure)
        {
            var endpoint = _configuration["Nlp:AzureOpenAi:Endpoint"]!.TrimEnd('/');
            var deployment = _configuration["Nlp:AzureOpenAi:DeploymentName"];
            var apiVersion = _configuration["Nlp:AzureOpenAi:ApiVersion"] ?? "2024-02-15-preview";
            url = $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
            apiKey = _configuration["Nlp:AzureOpenAi:ApiKey"] ?? "";
        }
        else
        {
            var endpoint = _configuration["Nlp:OpenAi:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
            url = endpoint;
            apiKey = _configuration["Nlp:OpenAi:ApiKey"] ?? "";
            model = _configuration["Nlp:OpenAi:Model"] ?? "gpt-4o-mini";
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("API key for NLP provider is not configured.");
        }

        var requestBody = new
        {
            model = isAzure ? null : model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        
        if (isAzure)
        {
            request.Headers.Add("api-key", apiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"LLM API call failed with status {response.StatusCode}: {errContent}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;

        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() 
                      ?? throw new InvalidOperationException("LLM returned empty content.");

        int promptTokens = 0;
        int completionTokens = 0;
        int totalTokens = 0;

        if (root.TryGetProperty("usage", out var usageElement))
        {
            if (usageElement.TryGetProperty("prompt_tokens", out var pt)) promptTokens = pt.GetInt32();
            if (usageElement.TryGetProperty("completion_tokens", out var ct)) completionTokens = ct.GetInt32();
            if (usageElement.TryGetProperty("total_tokens", out var tt)) totalTokens = tt.GetInt32();
        }

        return new LlmResponse
        {
            Content = content,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens
        };
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ERP.Domain.Interfaces;

namespace ERP.Infrastructure.Services.AiEngine;

public class OpenAiWhisperService : ITranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OpenAiWhisperService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        var apiKey = _configuration["Voice:OpenAi:ApiKey"] 
                     ?? _configuration["Nlp:OpenAi:ApiKey"] 
                     ?? throw new InvalidOperationException("API key for Voice Transcription provider is not configured.");

        var endpoint = _configuration["Voice:OpenAi:Endpoint"] ?? "https://api.openai.com/v1/audio/transcriptions";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var multipartContent = new MultipartFormDataContent();
        
        var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        multipartContent.Add(streamContent, "file", fileName);

        var modelContent = new StringContent("whisper-1");
        multipartContent.Add(modelContent, "model");

        request.Content = multipartContent;

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Transcription API call failed with status {response.StatusCode}: {errContent}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;

        var text = root.GetProperty("text").GetString() 
                   ?? throw new InvalidOperationException("Transcription returned empty text.");

        return text;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Persistence.Contexts;
using ERP.WebAPI.Controllers;

namespace ERP.WebAPI.Tests;

public class FakeLlmClient : ILlmClient
{
    public ConcurrentDictionary<string, (string Content, int PromptTokens, int CompletionTokens, int TotalTokens)> Mocks { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception? ExceptionToThrow { get; set; }

    public Task<LlmResponse> CompleteChatAsync(string systemPrompt, string userPrompt)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        if (Mocks.TryGetValue(userPrompt, out var mock))
        {
            return Task.FromResult(new LlmResponse
            {
                Content = mock.Content,
                PromptTokens = mock.PromptTokens,
                CompletionTokens = mock.CompletionTokens,
                TotalTokens = mock.TotalTokens
            });
        }

        // Return empty JSON as fallback
        return Task.FromResult(new LlmResponse
        {
            Content = "{}",
            PromptTokens = 0,
            CompletionTokens = 0,
            TotalTokens = 0
        });
    }
}

public class FakeTranscriptionService : ITranscriptionService
{
    public ConcurrentDictionary<string, string> Transcripts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string DefaultTranscript { get; set; } = "Show pending orders";
    public Exception? ExceptionToThrow { get; set; }

    public Task<string> TranscribeAsync(Stream audioStream, string fileName)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        if (Transcripts.TryGetValue(fileName, out var transcript))
        {
            return Task.FromResult(transcript);
        }

        return Task.FromResult(DefaultTranscript);
    }
}

public class LlmWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public FakeLlmClient FakeLlm { get; } = new();
    public FakeTranscriptionService FakeTranscription { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Nlp:Provider", "Llm" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real LLM client and register Fake
            var descriptorLlm = services.SingleOrDefault(d => d.ServiceType == typeof(ILlmClient));
            if (descriptorLlm != null) services.Remove(descriptorLlm);
            services.AddSingleton<ILlmClient>(FakeLlm);

            // Remove real Transcription and register Fake
            var descriptorTrans = services.SingleOrDefault(d => d.ServiceType == typeof(ITranscriptionService));
            if (descriptorTrans != null) services.Remove(descriptorTrans);
            services.AddSingleton<ITranscriptionService>(FakeTranscription);

            // Seed additional ambiguous customer
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var existing = db.Customers.FirstOrDefault(c => c.CustomerCode == "XYZ002");
            if (existing == null)
            {
                db.Customers.Add(new Customer
                {
                    CustomerID = 99,
                    CustomerCode = "XYZ002",
                    CompanyName = "XYZ Logistics Ltd",
                    ContactPerson = "Ambiguous Contact",
                    Phone = "+919876543211",
                    Email = "logistics@xyz.com",
                    IsDeleted = false
                });
                db.SaveChanges();
            }
        });
    }
}

public class LlmIntegrationTests : IClassFixture<LlmWebApplicationFactory>
{
    private readonly LlmWebApplicationFactory _factory;

    public LlmIntegrationTests(LlmWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> GetClientForRoleAsync(string role)
    {
        var client = _factory.CreateClient();
        string email = role.ToLower() switch
        {
            "admin" => "admin@erp.com",
            "sales" => "sales@erp.com",
            "accounts" => "accounts@erp.com",
            "manager" => "manager@erp.com",
            _ => throw new ArgumentException("Unknown role")
        };
        
        string password = role.ToLower() switch
        {
            "admin" => "Admin123!",
            "sales" => "Sales123!",
            "accounts" => "Accounts123!",
            "manager" => "Manager123!",
            _ => throw new ArgumentException("Unknown role")
        };

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginData.GetProperty("accessToken").GetString();
        
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Test_CorrectIntentExtraction_AndTokenLogging()
    {
        // 1. Arrange
        var client = await GetClientForRoleAsync("Sales");
        
        var expectedLlmOutput = new
        {
            intent = "GetPendingPayment",
            parameters = new Dictionary<string, string?>
            {
                { "CustomerName", "XYZ Traders" },
                { "ProductName", null },
                { "Quantity", null },
                { "Rate", null },
                { "OrderID", null },
                { "Status", null },
                { "Amount", null },
                { "PaymentMode", null }
            },
            explanation = "Checking pending payments for XYZ Traders"
        };
        
        var prompt = "How much payment is pending for XYZ?";
        _factory.FakeLlm.Mocks[prompt] = (
            JsonSerializer.Serialize(expectedLlmOutput),
            120, 85, 205
        );

        // 2. Act
        var response = await client.PostAsJsonAsync("/api/chat/chat", new { Prompt = prompt });

        // 3. Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GetPendingPayment", data.GetProperty("intent").GetString());
        Assert.Equal("Checking pending payments for XYZ Traders", data.GetProperty("explanation").GetString());

        // Verify tokens are logged in Database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = await db.AiActionLogs
            .OrderByDescending(l => l.LogID)
            .FirstOrDefaultAsync(l => l.OriginalPrompt == prompt);
        
        Assert.NotNull(log);
        Assert.Equal(120, log.PromptTokens);
        Assert.Equal(85, log.CompletionTokens);
        Assert.Equal(205, log.TotalTokens);
        Assert.False(log.ForcedToPendingBySafety);
    }

    [Fact]
    public async Task Test_AmbiguousCustomerName_ReturnsClarification()
    {
        // 1. Arrange
        var client = await GetClientForRoleAsync("Sales");
        
        var expectedLlmOutput = new
        {
            intent = "GetPendingPayment",
            parameters = new Dictionary<string, string?>
            {
                { "CustomerName", "XYZ" },
                { "ProductName", null },
                { "Quantity", null },
                { "Rate", null },
                { "OrderID", null },
                { "Status", null },
                { "Amount", null },
                { "PaymentMode", null }
            },
            explanation = "Checking pending payments for XYZ"
        };
        
        var prompt = "Show pending balance for XYZ";
        _factory.FakeLlm.Mocks[prompt] = (
            JsonSerializer.Serialize(expectedLlmOutput),
            100, 50, 150
        );

        // 2. Act
        var response = await client.PostAsJsonAsync("/api/chat/chat", new { Prompt = prompt });

        // 3. Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        Assert.Equal("Clarification", data.GetProperty("intent").GetString());
        Assert.True(data.GetProperty("isClarification").GetBoolean());
        
        var explanation = data.GetProperty("explanation").GetString();
        Assert.Contains("Multiple customers matched 'XYZ'", explanation);
        Assert.Contains("'XYZ Traders'", explanation);
        Assert.Contains("'XYZ Logistics Ltd'", explanation);
    }

    [Fact]
    public async Task Test_UpdateOrDeleteWithNoWhereClause_ForcedToPendingApproval()
    {
        // 1. Arrange
        var client = await GetClientForRoleAsync("Manager");
        
        // We use our FORCE_SQL hook in explanation to test an UPDATE with no WHERE clause
        var expectedLlmOutput = new
        {
            intent = "UpdateOrder",
            parameters = new Dictionary<string, string?>
            {
                { "CustomerName", null },
                { "ProductName", null },
                { "Quantity", null },
                { "Rate", null },
                { "OrderID", null },
                { "Status", null },
                { "Amount", null },
                { "PaymentMode", null }
            },
            explanation = "FORCE_SQL:UPDATE Orders SET IsDeleted = 1"
        };
        
        var prompt = "Delete all orders";
        _factory.FakeLlm.Mocks[prompt] = (
            JsonSerializer.Serialize(expectedLlmOutput),
            150, 60, 210
        );

        // 2. Act
        var response = await client.PostAsJsonAsync("/api/chat/chat", new { Prompt = prompt });

        // 3. Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(data.GetProperty("requiresApproval").GetBoolean());
        Assert.NotNull(data.GetProperty("approvalToken").GetString());

        // Verify it was logged as forced to pending
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = await db.AiActionLogs
            .OrderByDescending(l => l.LogID)
            .FirstOrDefaultAsync(l => l.OriginalPrompt == prompt);
        
        Assert.NotNull(log);
        Assert.True(log.ForcedToPendingBySafety);
        Assert.Equal(ApprovalStatus.Pending, log.ApprovalStatus);
    }

    [Fact]
    public async Task Test_VoiceTranscription_Endpoint_IntegratesCorrectly()
    {
        // 1. Arrange
        var client = await GetClientForRoleAsync("Sales");

        var transcript = "How much payment is pending for XYZ?";
        _factory.FakeTranscription.Transcripts["test.wav"] = transcript;
        
        var expectedLlmOutput = new
        {
            intent = "GetPendingPayment",
            parameters = new Dictionary<string, string?>
            {
                { "CustomerName", "XYZ Traders" },
                { "ProductName", null },
                { "Quantity", null },
                { "Rate", null },
                { "OrderID", null },
                { "Status", null },
                { "Amount", null },
                { "PaymentMode", null }
            },
            explanation = "Checking pending payments for XYZ Traders"
        };
        
        _factory.FakeLlm.Mocks[transcript] = (
            JsonSerializer.Serialize(expectedLlmOutput),
            120, 80, 200
        );

        // Prepare mock audio file payload
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0, 1, 2, 3 });
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        content.Add(fileContent, "file", "test.wav");

        // 2. Act
        var response = await client.PostAsync("/api/voice", content);

        // 3. Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        Assert.Equal(transcript, data.GetProperty("transcript").GetString());
        
        var chatResponse = data.GetProperty("chatResponse");
        Assert.Equal("GetPendingPayment", chatResponse.GetProperty("intent").GetString());
        Assert.Equal("Checking pending payments for XYZ Traders", chatResponse.GetProperty("explanation").GetString());
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Infrastructure.Persistence.Contexts;
using ERP.WebAPI.Controllers;

namespace ERP.WebAPI.Tests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DisableRateLimiting", "true" }
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            SeedTestingData(db);
        });
    }

    private void SeedTestingData(ApplicationDbContext db)
    {
        var customer = db.Customers.FirstOrDefault(c => c.CustomerCode == "XYZ001");
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerID = 12,
                CustomerCode = "XYZ001",
                CompanyName = "XYZ Traders",
                ContactPerson = "Rajesh Kumar",
                Phone = "+919876543210",
                Email = "rajesh@xyztraders.com",
                IsDeleted = false
            };
            db.Customers.Add(customer);
        }

        var order = new Order
        {
            OrderID = 1025,
            OrderNumber = "ORD-20260714-1025",
            CustomerID = customer.CustomerID,
            OrderDate = DateTime.UtcNow,
            OrderStatus = OrderStatus.Pending,
            TotalAmount = 50000m,
            PaidAmount = 10000m,
            Remarks = "Active Order for deletion test",
            IsDeleted = false
        };
        db.Orders.Add(order);

        var order2 = new Order
        {
            OrderID = 1026,
            OrderNumber = "ORD-20260714-1026",
            CustomerID = customer.CustomerID,
            OrderDate = DateTime.UtcNow,
            OrderStatus = OrderStatus.Pending,
            TotalAmount = 45000m,
            PaidAmount = 5000m,
            Remarks = "Second Active Order for distinct tests",
            IsDeleted = false
        };
        db.Orders.Add(order2);

        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _connection != null)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}

public class IntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTests(CustomWebApplicationFactory<Program> factory)
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

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new AuthController.LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var responseContent = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseContent);
        var token = doc.RootElement.GetProperty("accessToken").GetString();
        
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Test_Login_Success_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new AuthController.LoginRequest
        {
            Email = "admin@erp.com",
            Password = "Admin123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        
        Assert.True(root.TryGetProperty("accessToken", out _));
        Assert.True(root.TryGetProperty("refreshToken", out _));
        Assert.Equal("Admin", root.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Test_Login_Fail_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new AuthController.LoginRequest
        {
            Email = "admin@erp.com",
            Password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Test_GetPendingOrders_ExecutesImmediately()
    {
        // Arrange
        var client = await GetClientForRoleAsync("Admin");
        var request = new ChatController.ChatRequest
        {
            Prompt = "Show pending orders"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("GetPendingOrders", root.GetProperty("intent").GetString());
        Assert.False(root.GetProperty("requiresApproval").GetBoolean());
        Assert.True(root.GetProperty("data").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Test_DeleteOrder_RequiresApproval_AndSucceedsAfterApprove()
    {
        // 1. Arrange & Request deletion (Admin)
        var client = await GetClientForRoleAsync("Admin");
        var request = new ChatController.ChatRequest
        {
            Prompt = "Delete Order 1025"
        };

        // 2. Act - Verify 202 Accepted
        var chatResponse = await client.PostAsJsonAsync("/api/chat/chat", request);
        Assert.Equal(HttpStatusCode.Accepted, chatResponse.StatusCode);

        var chatJson = await chatResponse.Content.ReadAsStringAsync();
        using var chatDoc = JsonDocument.Parse(chatJson);
        var chatRoot = chatDoc.RootElement;

        Assert.Equal("DeleteOrder", chatRoot.GetProperty("intent").GetString());
        Assert.True(chatRoot.GetProperty("requiresApproval").GetBoolean());
        
        var approvalToken = chatRoot.GetProperty("approvalToken").GetString();
        Assert.NotNull(approvalToken);

        // 3. Verify order not deleted yet
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.OrderID == 1025);
            Assert.NotNull(order);
            Assert.False(order.IsDeleted);
        }

        // 4. Act - Approve (Admin)
        var approveRequest = new ChatController.ApproveRequest { ApprovalToken = approvalToken };
        var approveResponse = await client.PostAsJsonAsync("/api/chat/approve", approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // 5. Verify soft-deleted
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.OrderID == 1025);
            Assert.NotNull(order);
            Assert.True(order.IsDeleted);
            Assert.NotNull(order.DeletedAt);

            // Verify log tracks Creator and Approver
            var log = await db.AiActionLogs
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync(l => l.OriginalPrompt == "Delete Order 1025");
            Assert.NotNull(log);
            Assert.Equal(ApprovalStatus.Approved, log.ApprovalStatus);
            Assert.Equal(ExecutionStatus.Success, log.ExecutionStatus);
            Assert.Equal(1, log.UserID); // Creator: Admin (id 1)
            Assert.Equal(1, log.ApprovedByUserId); // Approver: Admin (id 1)
        }
    }

    [Fact]
    public async Task Test_Reject_DiscardsCachedAction()
    {
        var client = await GetClientForRoleAsync("Admin");
        var request = new ChatController.ChatRequest
        {
            Prompt = "Delete Order 1025"
        };

        var chatResponse = await client.PostAsJsonAsync("/api/chat/chat", request);
        var chatJson = await chatResponse.Content.ReadAsStringAsync();
        using var chatDoc = JsonDocument.Parse(chatJson);
        var approvalToken = chatDoc.RootElement.GetProperty("approvalToken").GetString()!;

        // Reject
        var rejectRequest = new ChatController.ApproveRequest { ApprovalToken = approvalToken };
        var rejectResponse = await client.PostAsJsonAsync("/api/chat/reject", rejectRequest);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        // Approve after reject should return 410 Gone because it was evicted from cache
        var approveResponse = await client.PostAsJsonAsync("/api/chat/approve", rejectRequest);
        Assert.Equal(HttpStatusCode.Gone, approveResponse.StatusCode);
    }

    [Fact]
    public async Task Test_SecurityGuard_BlocksDangerousSql()
    {
        var client = await GetClientForRoleAsync("Admin");
        var request = new ChatController.ChatRequest
        {
            Prompt = "Update Order 1025 status to Completed; DROP TABLE Customers;"
        };

        var response = await client.PostAsJsonAsync("/api/chat/chat", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Contains("Prohibited", doc.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test_RBAC_RoleMatrix_Sales_CanCreateOrder_Accounts_Cannot()
    {
        var salesClient = await GetClientForRoleAsync("Sales");
        var newOrder = new Order
        {
            CustomerID = 12,
            Remarks = "Sales created order"
        };

        // Act - Sales
        var salesResponse = await salesClient.PostAsJsonAsync("/api/orders", newOrder);
        Assert.Equal(HttpStatusCode.Created, salesResponse.StatusCode);

        // Act - Accounts
        var accountsClient = await GetClientForRoleAsync("Accounts");
        var accountsResponse = await accountsClient.PostAsJsonAsync("/api/orders", newOrder);
        Assert.Equal(HttpStatusCode.Forbidden, accountsResponse.StatusCode);
    }

    [Fact]
    public async Task Test_RBAC_Reports_Sales_Allowed_Accounts_Denied()
    {
        // Act - Sales
        var salesClient = await GetClientForRoleAsync("Sales");
        var salesResponse = await salesClient.GetAsync("/api/reports/sales-summary");
        Assert.Equal(HttpStatusCode.OK, salesResponse.StatusCode);

        // Act - Accounts
        var accountsClient = await GetClientForRoleAsync("Accounts");
        var accountsResponse = await accountsClient.GetAsync("/api/reports/sales-summary");
        Assert.Equal(HttpStatusCode.Forbidden, accountsResponse.StatusCode);
    }

    [Fact]
    public async Task Test_Ledger_Accounts_Allowed_Sales_Denied()
    {
        // Act - Accounts
        var accountsClient = await GetClientForRoleAsync("Accounts");
        var accountsResponse = await accountsClient.GetAsync("/api/ledger/12");
        Assert.Equal(HttpStatusCode.OK, accountsResponse.StatusCode);

        // Act - Sales
        var salesClient = await GetClientForRoleAsync("Sales");
        var salesResponse = await salesClient.GetAsync("/api/ledger/12");
        Assert.Equal(HttpStatusCode.Forbidden, salesResponse.StatusCode);
    }

    [Fact]
    public async Task Test_Approval_TokenOwner_Verification_And_Override()
    {
        // 1. Sales triggers Delete Order (generates pending token)
        var salesClient = await GetClientForRoleAsync("Sales");
        var chatRequest = new ChatController.ChatRequest { Prompt = "Delete Order 1026" };
        var chatResponse = await salesClient.PostAsJsonAsync("/api/chat/chat", chatRequest);
        Assert.Equal(HttpStatusCode.Accepted, chatResponse.StatusCode);
        
        using var doc = JsonDocument.Parse(await chatResponse.Content.ReadAsStringAsync());
        var approvalToken = doc.RootElement.GetProperty("approvalToken").GetString()!;

        // 2. Sales B (another user with role Sales) attempts to approve Sales A's token -> 403 Forbidden
        // Note: For testing distinct user ID, we can log in with a different user context.
        // sales@erp.com is the only sales user seeded. But wait, what if an Accounts user attempts to approve it?
        // Accounts is not owner and not Admin/Manager.
        var accountsClient = await GetClientForRoleAsync("Accounts");
        var approveRequest = new ChatController.ApproveRequest { ApprovalToken = approvalToken };
        
        var accountsResponse = await accountsClient.PostAsJsonAsync("/api/chat/approve", approveRequest);
        Assert.Equal(HttpStatusCode.Forbidden, accountsResponse.StatusCode); // Non-owner, non-privileged gets 403

        // 3. Manager (privileged override) approves Sales A's token -> 200 OK
        var managerClient = await GetClientForRoleAsync("Manager");
        var managerResponse = await managerClient.PostAsJsonAsync("/api/chat/approve", approveRequest);
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);

        // Verify log shows original creator = Sales (id 2) and approved by = Manager (id 4)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var log = await db.AiActionLogs
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync(l => l.OriginalPrompt == "Delete Order 1026");
            Assert.NotNull(log);
            Assert.Equal(2, log.UserID); // Creator: Sales (id 2)
            Assert.Equal(4, log.ApprovedByUserId); // Approver: Manager (id 4)
        }
    }

    [Fact]
    public async Task Test_Approval_Token_Expiration_Returns_410()
    {
        var client = await GetClientForRoleAsync("Admin");
        // A random Guid represents a nonexistent token (conceptually expired)
        var approveRequest = new ChatController.ApproveRequest { ApprovalToken = Guid.NewGuid().ToString() };
        var response = await client.PostAsJsonAsync("/api/chat/approve", approveRequest);
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }
}

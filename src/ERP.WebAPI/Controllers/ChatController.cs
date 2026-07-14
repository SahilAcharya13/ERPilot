using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services.SqlSafety;

namespace ERP.WebAPI.Controllers;

[Authorize]
[EnableRateLimiting("ChatPolicy")]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly INlpService _nlpService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly SqlSafetyGuard _safetyGuard;

    public ChatController(
        INlpService nlpService,
        IUnitOfWork unitOfWork,
        IMemoryCache cache)
    {
        _nlpService = nlpService;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _safetyGuard = new SqlSafetyGuard();
    }

    public class ChatRequest
    {
        public string Prompt { get; set; } = null!;
    }

    public class ApproveRequest
    {
        public string ApprovalToken { get; set; } = null!;
    }

    private class CachedAction
    {
        public long LogID { get; set; }
        public string GeneratedSql { get; set; } = null!;
        public List<object> ParameterValues { get; set; } = new();
        public int CreatorUserId { get; set; }
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt cannot be empty.");
        }

        // Get authenticated user ID from JWT Claims
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized("Invalid user token claims.");
        }

        // Validate user existence
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            return BadRequest("Invalid User ID.");
        }

        // 1. Process NLP
        var nlpResult = await _nlpService.ProcessPromptAsync(request.Prompt, userId);

        // 2. Security Guard check (AST validation)
        var safetyCheck = _safetyGuard.VerifySql(nlpResult.GeneratedSql);
        if (!safetyCheck.IsSuccess)
        {
            // Log security failure to AiActionLog
            var failedLog = new AiActionLog
            {
                UserID = userId,
                OriginalPrompt = request.Prompt,
                ExtractedIntent = nlpResult.Intent,
                Parameters = JsonSerializer.Serialize(nlpResult.Parameters),
                GeneratedSQL = nlpResult.GeneratedSql,
                ApprovalStatus = ApprovalStatus.SystemBypassed,
                ExecutionStatus = ExecutionStatus.Failed,
                ErrorMessage = $"Security Guard Blocked: {safetyCheck.ErrorMessage}",
                Timestamp = DateTime.UtcNow
            };
            await _unitOfWork.AiActionLogs.AddAsync(failedLog);
            await _unitOfWork.CompleteAsync();

            return BadRequest(new { error = safetyCheck.ErrorMessage });
        }

        // 3. Handle Flow based on DML vs Select
        if (!nlpResult.RequiresApproval)
        {
            // SELECT: Execute immediately
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var queryData = await _unitOfWork.QuerySqlRawAsync(nlpResult.GeneratedSql, nlpResult.ParameterValues.ToArray());
                stopwatch.Stop();

                // Log execution success
                var successLog = new AiActionLog
                {
                    UserID = userId,
                    OriginalPrompt = request.Prompt,
                    ExtractedIntent = nlpResult.Intent,
                    Parameters = JsonSerializer.Serialize(nlpResult.Parameters),
                    GeneratedSQL = nlpResult.GeneratedSql,
                    ApprovalStatus = ApprovalStatus.SystemBypassed,
                    ExecutionStatus = ExecutionStatus.Success,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow
                };
                await _unitOfWork.AiActionLogs.AddAsync(successLog);
                await _unitOfWork.CompleteAsync();

                return Ok(new
                {
                    intent = nlpResult.Intent,
                    explanation = nlpResult.Explanation,
                    sql = nlpResult.GeneratedSql,
                    requiresApproval = false,
                    data = queryData
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // Log execution failure
                var dbFailedLog = new AiActionLog
                {
                    UserID = userId,
                    OriginalPrompt = request.Prompt,
                    ExtractedIntent = nlpResult.Intent,
                    Parameters = JsonSerializer.Serialize(nlpResult.Parameters),
                    GeneratedSQL = nlpResult.GeneratedSql,
                    ApprovalStatus = ApprovalStatus.SystemBypassed,
                    ExecutionStatus = ExecutionStatus.Failed,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
                await _unitOfWork.AiActionLogs.AddAsync(dbFailedLog);
                await _unitOfWork.CompleteAsync();

                return StatusCode(500, new { error = "Database query execution failed.", details = ex.Message });
            }
        }
        else
        {
            // DML (INSERT/UPDATE/DELETE): Require approval
            // Create a pending action log
            var pendingLog = new AiActionLog
            {
                UserID = userId,
                OriginalPrompt = request.Prompt,
                ExtractedIntent = nlpResult.Intent,
                Parameters = JsonSerializer.Serialize(nlpResult.Parameters),
                GeneratedSQL = nlpResult.GeneratedSql,
                ApprovalStatus = ApprovalStatus.Pending,
                ExecutionStatus = ExecutionStatus.NotStarted,
                Timestamp = DateTime.UtcNow
            };
            await _unitOfWork.AiActionLogs.AddAsync(pendingLog);
            await _unitOfWork.CompleteAsync(); // Generates LogID

            // Generate unique token
            var token = Guid.NewGuid().ToString();

            // Cache details with 5-minute absolute expiration
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _cache.Set(token, new CachedAction
            {
                LogID = pendingLog.LogID,
                GeneratedSql = nlpResult.GeneratedSql,
                ParameterValues = nlpResult.ParameterValues,
                CreatorUserId = userId
            }, cacheOptions);

            return Accepted(new
            {
                approvalToken = token,
                intent = nlpResult.Intent,
                explanation = nlpResult.Explanation,
                sql = nlpResult.GeneratedSql,
                requiresApproval = true
            });
        }
    }

    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromBody] ApproveRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ApprovalToken))
        {
            return BadRequest("Approval token cannot be empty.");
        }

        // 1. Retrieve from cache. If expired or not present, return 410 Gone
        if (!_cache.TryGetValue(request.ApprovalToken, out CachedAction? cachedAction) || cachedAction == null)
        {
            return StatusCode(410, new { error = "Approval token has expired or is invalid." });
        }

        // Fetch corresponding log row
        var log = await _unitOfWork.AiActionLogs.GetByIdAsync(cachedAction.LogID);
        if (log == null)
        {
            return NotFound("Associated action log not found.");
        }

        // 2. Fetch current user context
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(currentUserIdStr, out var currentUserId))
        {
            return Unauthorized("Invalid user token claims.");
        }
        var currentUserRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;

        // 3. Ownership Check: Reject with 403 if not token owner, UNLESS Admin or Manager
        var isTokenOwner = currentUserId == cachedAction.CreatorUserId;
        var isPrivileged = currentUserRoleStr == "Admin" || currentUserRoleStr == "Manager";
        if (!isTokenOwner && !isPrivileged)
        {
            return StatusCode(403, new { error = "You are not authorized to approve someone else's action." });
        }

        // 4. RBAC Check on the action intent
        var intent = log.ExtractedIntent;
        bool isRoleAllowed = false;
        if (currentUserRoleStr == "Admin" || currentUserRoleStr == "Manager")
        {
            isRoleAllowed = true;
        }
        else if (currentUserRoleStr == "Sales")
        {
            isRoleAllowed = (intent == "CreateOrder" || intent == "RecordPayment");
        }
        else if (currentUserRoleStr == "Accounts")
        {
            isRoleAllowed = (intent == "RecordPayment");
        }

        if (!isRoleAllowed)
        {
            return StatusCode(403, new { error = $"Role {currentUserRoleStr} is not permitted to approve intent '{intent}'." });
        }

        // Secondary safety validation (defense in depth)
        var safetyCheck = _safetyGuard.VerifySql(cachedAction.GeneratedSql);
        if (!safetyCheck.IsSuccess)
        {
            log.ApprovalStatus = ApprovalStatus.Rejected;
            log.ExecutionStatus = ExecutionStatus.Failed;
            log.ErrorMessage = $"Security Guard Intercepted during Approval: {safetyCheck.ErrorMessage}";
            log.ApprovedByUserId = currentUserId;
            await _unitOfWork.CompleteAsync();
            _cache.Remove(request.ApprovalToken);
            return BadRequest(new { error = safetyCheck.ErrorMessage });
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Execute the action (writes/updates database)
            var rowsAffected = await _unitOfWork.ExecuteSqlRawAsync(cachedAction.GeneratedSql, cachedAction.ParameterValues.ToArray());
            stopwatch.Stop();

            // Update database log
            log.ApprovedByUserId = currentUserId;
            log.ApprovalStatus = ApprovalStatus.Approved;
            log.ExecutionStatus = ExecutionStatus.Success;
            log.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
            await _unitOfWork.CompleteAsync();

            // Evict from cache
            _cache.Remove(request.ApprovalToken);

            return Ok(new
            {
                message = "Action executed successfully.",
                rowsAffected = rowsAffected
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Update log to failed
            log.ApprovedByUserId = currentUserId;
            log.ApprovalStatus = ApprovalStatus.Approved;
            log.ExecutionStatus = ExecutionStatus.Failed;
            log.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
            log.ErrorMessage = ex.Message;
            await _unitOfWork.CompleteAsync();

            return StatusCode(500, new { error = "Database transaction execution failed.", details = ex.Message });
        }
    }

    [HttpPost("reject")]
    public async Task<IActionResult> Reject([FromBody] ApproveRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ApprovalToken))
        {
            return BadRequest("Approval token cannot be empty.");
        }

        if (!_cache.TryGetValue(request.ApprovalToken, out CachedAction? cachedAction) || cachedAction == null)
        {
            return StatusCode(410, new { error = "Approval token has expired or is invalid." });
        }

        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(currentUserIdStr, out var currentUserId))
        {
            return Unauthorized("Invalid user token claims.");
        }
        var currentUserRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;

        var isTokenOwner = currentUserId == cachedAction.CreatorUserId;
        var isPrivileged = currentUserRoleStr == "Admin" || currentUserRoleStr == "Manager";
        if (!isTokenOwner && !isPrivileged)
        {
            return StatusCode(403, new { error = "You are not authorized to reject someone else's action." });
        }

        var log = await _unitOfWork.AiActionLogs.GetByIdAsync(cachedAction.LogID);
        if (log != null)
        {
            log.ApprovedByUserId = currentUserId;
            log.ApprovalStatus = ApprovalStatus.Rejected;
            log.ExecutionStatus = ExecutionStatus.Cancelled;
            await _unitOfWork.CompleteAsync();
        }

        _cache.Remove(request.ApprovalToken);

        return Ok(new
        {
            message = "Action successfully rejected and discarded."
        });
    }
}

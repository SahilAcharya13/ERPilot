using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ERP.Domain.Interfaces;

namespace ERP.WebAPI.Controllers;

[EnableRateLimiting("LoginPolicy")]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _tokenService;

    public AuthController(IUnitOfWork unitOfWork, IJwtTokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and Password are required." });
        }

        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        if (user == null || !user.IsActive || user.IsDeleted)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        // Verify password hash using BCrypt
        var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isValid)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var tokens = _tokenService.GenerateTokens(user);

        return Ok(new
        {
            accessToken = tokens.AccessToken,
            refreshToken = tokens.RefreshToken,
            expiresAt = tokens.ExpiresAt,
            role = user.Role.ToString(),
            userId = user.UserID
        });
    }
}

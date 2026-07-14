using System;

namespace ERP.Domain.Interfaces;

public class JwtTokenResult
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

public interface IJwtTokenService
{
    JwtTokenResult GenerateTokens(Entities.User user);
}

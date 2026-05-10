using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public class AuthService
{
    private readonly SqliteConnection _db;
    private readonly IConfiguration _config;

    public AuthService(SqliteConnection db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<(User? user, string? error)> RegisterAsync(RegisterRequest request)
    {
        await _db.OpenAsync();

        var existing = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email",
            new { Email = request.Email.ToLower().Trim() });

        if (existing != null)
            return (null, "Email já cadastrado");

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = request.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
            Nome = request.Nome?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync("""
            INSERT INTO users (id, email, password_hash, nome, created_at)
            VALUES (@Id, @Email, @PasswordHash, @Nome, @CreatedAt)
            """, user);

        return (user, null);
    }

    public async Task<(User? user, string? error)> LoginAsync(LoginRequest request)
    {
        await _db.OpenAsync();

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email",
            new { Email = request.Email.ToLower().Trim() });

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Senha, user.PasswordHash))
            return (null, "Credenciais inválidas");

        return (user, null);
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret não configurado")));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Nome ?? user.Email)
        };

        var expireMinutes = int.TryParse(_config["Jwt:AccessTokenExpireMinutes"], out var m) ? m : 30;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Refresh token formato: "{id}.{secret}" — id permite lookup O(1), secret é validado por bcrypt.
    public async Task<(string token, string id)> GenerateRefreshTokenAsync(User user)
    {
        await _db.OpenAsync();

        var id = Guid.NewGuid().ToString("N");
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(secret);
        var expireDays = int.TryParse(_config["Jwt:RefreshTokenExpireDays"], out var d) ? d : 7;

        await _db.ExecuteAsync("""
            INSERT INTO refresh_tokens (id, user_id, token_hash, expires_at, created_at)
            VALUES (@Id, @UserId, @TokenHash, @ExpiresAt, @CreatedAt)
            """, new
            {
                Id = id,
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedAt = DateTime.UtcNow
            });

        return ($"{id}.{secret}", id);
    }

    public async Task<(User? user, string? newRefreshToken, string? error)> RotateRefreshTokenAsync(string token)
    {
        var (id, secret) = SplitToken(token);
        if (id == null) return (null, null, "invalid_token");

        await _db.OpenAsync();

        var rt = await _db.QueryFirstOrDefaultAsync<RefreshToken>(
            "SELECT * FROM refresh_tokens WHERE id = @Id", new { Id = id });

        if (rt == null || rt.ExpiresAt <= DateTime.UtcNow)
            return (null, null, "expired_or_unknown");

        if (!BCrypt.Net.BCrypt.Verify(secret, rt.TokenHash))
            return (null, null, "invalid_token");

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id", new { Id = rt.UserId });
        if (user == null) return (null, null, "user_not_found");

        // Rotação: revoga o atual e emite um novo
        await _db.ExecuteAsync("DELETE FROM refresh_tokens WHERE id = @Id", new { Id = id });
        var (newToken, _) = await GenerateRefreshTokenAsync(user);

        return (user, newToken, null);
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var (id, _) = SplitToken(token);
        if (id == null) return;

        await _db.OpenAsync();
        await _db.ExecuteAsync("DELETE FROM refresh_tokens WHERE id = @Id", new { Id = id });
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        await _db.OpenAsync();
        return await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id",
            new { Id = userId });
    }

    private static (string? id, string secret) SplitToken(string token)
    {
        var idx = token.IndexOf('.');
        if (idx <= 0 || idx == token.Length - 1) return (null, "");
        return (token[..idx], token[(idx + 1)..]);
    }

    public static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Nome = user.Nome
    };
}

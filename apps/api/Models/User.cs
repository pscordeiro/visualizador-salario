namespace Api.Models;

public class User
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Nome { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Nome { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
    public string? Nome { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = "";
}

public class AuthResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public UserDto User { get; set; } = new();
}

public class RefreshToken
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

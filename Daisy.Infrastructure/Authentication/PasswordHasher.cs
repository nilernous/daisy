using Daisy.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Daisy.Infrastructure.Authentication;

/// <summary>ASP.NET Core Identity PasswordHasher (PBKDF2), without the full Identity stack (BR-02).</summary>
public sealed class PasswordHasher : Application.Interfaces.IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password) => _hasher.HashPassword(null!, password);

    public bool VerifyPassword(string passwordHash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Not a valid hash (e.g. hand-edited seed data): treat as a wrong password.
            return false;
        }
    }
}

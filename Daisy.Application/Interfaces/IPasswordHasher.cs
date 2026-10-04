namespace Daisy.Application.Interfaces;

/// <summary>PBKDF2 password hashing (BR-02).</summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string password);
}

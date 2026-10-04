namespace Daisy.Domain.Common;

/// <summary>
/// Thrown when a business rule enforced by the domain model is broken.
/// The API maps it to 409 Conflict with <see cref="Code"/>.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    public object? Details { get; }
}

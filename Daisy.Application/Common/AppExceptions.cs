using Daisy.Domain.Common;

namespace Daisy.Application.Common;

/// <summary>
/// Base type for exceptions that the API turns into an error response (API_DESIGN §4).
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(int status, string code, string message, object? details = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Details = details;
    }

    public int Status { get; }

    public string Code { get; }

    public object? Details { get; }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string code, string message, object? details = null)
        : base(400, code, message, details)
    {
    }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string code, string message)
        : base(401, code, message)
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string code, string message)
        : base(403, code, message)
    {
    }
}

/// <summary>404 for missing, hidden or not-owned resources (BR-08).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message = "The resource was not found.", string code = ErrorCodes.NotFound)
        : base(404, code, message)
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message, object? details = null)
        : base(409, code, message, details)
    {
    }
}

public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string code, string message)
        : base(503, code, message)
    {
    }
}

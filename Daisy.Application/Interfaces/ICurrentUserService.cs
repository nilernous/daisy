using Daisy.Domain.Enums;

namespace Daisy.Application.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }

    UserRole? Role { get; }

    bool IsAuthenticated { get; }
}

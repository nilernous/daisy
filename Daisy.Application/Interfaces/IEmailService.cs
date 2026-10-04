namespace Daisy.Application.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(
        string toEmail,
        string fullName,
        string resetLink,
        CancellationToken cancellationToken = default);
}

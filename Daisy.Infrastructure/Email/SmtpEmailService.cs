using System.Net;
using Daisy.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Daisy.Infrastructure.Email;

/// <summary>Configuration section "Smtp" (API_DESIGN §11). An empty Host logs emails instead of sending them.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@daisy.local";

    public string FromName { get; set; } = "Daisy";
}

/// <summary>Sends email through SMTP with MailKit.</summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;

    public SmtpEmailService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(fullName, toEmail));
        message.Subject = "Đặt lại mật khẩu Daisy";
        message.Body = new BodyBuilder
        {
            TextBody = PasswordResetEmail.Text(fullName, resetLink),
            HtmlBody = PasswordResetEmail.Html(fullName, resetLink),
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        if (!string.IsNullOrEmpty(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

/// <summary>Development fallback when no SMTP host is configured: the email is written to the log.</summary>
public sealed class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SMTP is not configured. Password reset link for {Email}: {Link}", toEmail, resetLink);
        return Task.CompletedTask;
    }
}

internal static class PasswordResetEmail
{
    public static string Text(string fullName, string link) =>
        $"Chào {fullName},\n\nBạn vừa yêu cầu đặt lại mật khẩu Daisy. Mở liên kết sau trong 30 phút để đặt mật khẩu mới:\n{link}\n\nNếu bạn không yêu cầu, hãy bỏ qua email này.";

    public static string Html(string fullName, string link) =>
        $"<p>Chào {WebUtility.HtmlEncode(fullName)},</p>"
        + "<p>Bạn vừa yêu cầu đặt lại mật khẩu Daisy. Liên kết dưới đây có hiệu lực trong 30 phút.</p>"
        + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Đặt mật khẩu mới</a></p>"
        + "<p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>";
}

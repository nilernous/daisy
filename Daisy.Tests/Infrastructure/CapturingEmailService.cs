using System.Collections.Concurrent;
using Daisy.Application.Interfaces;

namespace Daisy.Tests.Infrastructure;

/// <summary>Keeps the last password reset link per email so tests can follow it.</summary>
public sealed class CapturingEmailService : IEmailService
{
    private readonly ConcurrentDictionary<string, string> _links = new(StringComparer.OrdinalIgnoreCase);

    public Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink, CancellationToken cancellationToken = default)
    {
        _links[toEmail] = resetLink;
        return Task.CompletedTask;
    }

    public string? LastResetToken(string email) =>
        _links.TryGetValue(email, out var link) ? Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + 6)..]) : null;
}

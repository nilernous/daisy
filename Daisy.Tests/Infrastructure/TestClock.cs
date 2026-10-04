namespace Daisy.Tests.Infrastructure;

/// <summary>
/// Real time plus an adjustable offset, so a test can jump past a payment deadline (BR-41)
/// and come back. JWT validation still uses the real clock, so the offset is reset after use.
/// </summary>
public sealed class TestClock : TimeProvider
{
    public TimeSpan Offset { get; set; }

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
}

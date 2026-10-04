namespace Daisy.Application.Common;

/// <summary>
/// Daisy stores and compares times in Vietnam time (UTC+07:00, no daylight saving),
/// the business time zone in API_DESIGN §2.1.
/// </summary>
public static class VietnamTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static readonly TimeZoneInfo TimeZone =
        TimeZoneInfo.CreateCustomTimeZone("Daisy/Vietnam", Offset, "Vietnam Time", "Vietnam Time");

    /// <summary>Current Vietnam time as a DateTime (Kind = Unspecified), as stored in the database.</summary>
    public static DateTime GetVietnamNow(this TimeProvider timeProvider) =>
        DateTime.SpecifyKind(timeProvider.GetUtcNow().ToOffset(Offset).DateTime, DateTimeKind.Unspecified);

    public static DateOnly GetVietnamToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetVietnamNow());

    /// <summary>Converts any DateTimeOffset to Vietnam time as stored in the database.</summary>
    public static DateTime ToVietnamTime(this DateTimeOffset value) =>
        DateTime.SpecifyKind(value.ToOffset(Offset).DateTime, DateTimeKind.Unspecified);
}

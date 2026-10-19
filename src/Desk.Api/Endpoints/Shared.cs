namespace Desk.Api.Endpoints;

// Small helpers used by the module endpoints.
public static class Shared
{
    // The hotels' time zone. Times officers type (when it happened, shift start) are
    // Pacific wall-clock times; system times (created, approved) are UTC.
    public static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    public static DateTime PacificNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Pacific);

    public static IResult Bad(string message) => Results.BadRequest(new { message });

    // Trimmed text, or null when empty; cut to max characters.
    public static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }
}

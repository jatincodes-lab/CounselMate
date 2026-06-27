namespace EducationCrm.Api.Services;

public static class IndianClock
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5.5);

    public static DateTimeOffset Now()
    {
        return DateTimeOffset.UtcNow.ToOffset(Offset);
    }

    public static DateTimeOffset TodayStart()
    {
        var now = Now();
        return new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, Offset);
    }

    public static DateTimeOffset ToIndianTime(DateTimeOffset value)
    {
        return value.ToOffset(Offset);
    }

    public static DateTimeOffset? ToIndianTime(DateTimeOffset? value)
    {
        return value.HasValue ? ToIndianTime(value.Value) : null;
    }
}

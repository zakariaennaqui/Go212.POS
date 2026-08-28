namespace Go212.POS.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated time interval for queries, reports, and sessions.
/// </summary>
public readonly record struct DateRange
{
    public DateTime From { get; }
    public DateTime To { get; }

    public DateRange(DateTime from, DateTime to)
    {
        if (from > to)
            throw new ArgumentException("The 'from' date cannot be after the 'to' date.", nameof(from));

        From = from;
        To = to;
    }

    public static DateRange Today()
    {
        var now = DateTime.Today;
        return new DateRange(now, now.AddDays(1).AddTicks(-1));
    }

    public static DateRange ThisWeek()
    {
        var today = DateTime.Today;
        var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-1 * diff).Date;
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
        return new DateRange(startOfWeek, endOfWeek);
    }

    public static DateRange ThisMonth()
    {
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
        return new DateRange(startOfMonth, endOfMonth);
    }

    public static DateRange LastDays(int days)
    {
        var end = DateTime.Now;
        var start = end.AddDays(-days);
        return new DateRange(start, end);
    }

    public bool Contains(DateTime date) => date >= From && date <= To;

    public TimeSpan Duration => To - From;
}

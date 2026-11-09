using Hotelly.Seed;

namespace Sales.Backend;

record SeedRate(Guid HotelId, DateOnly Night, decimal Rate);

static class SalesSeed
{
    public const int Days = 365;

    /// <summary>
    /// A year of nightly rates for every hotel, from the first day of the
    /// current month. Seasonal: a hotel's rate for a given night is the same
    /// at every run, whatever the day the demo runs.
    /// </summary>
    public static IEnumerable<SeedRate> NightlyRates(DateOnly today)
    {
        var random = SeedHotels.Random(2);
        var start = new DateOnly(today.Year, today.Month, 1);

        foreach (var number in SeedHotels.Numbers)
        {
            var id = SeedHotels.Id(number);
            // Sales doesn't know stars, cities, or amenities: those are
            // Registry's. Each hotel has its own base rate.
            var baseRate = random.Next(60, 400);

            for (var day = 0; day < Days; day++)
            {
                var night = start.AddDays(day);
                var rate = baseRate * Season(night) * (IsWeekend(night) ? 1.15m : 1m);
                yield return new SeedRate(id, night, Math.Round(rate));
            }
        }
    }

    static decimal Season(DateOnly night) => night switch
    {
        { Month: 12, Day: >= 20 } or { Month: 1, Day: <= 6 } => 1.4m,
        { Month: 7 or 8 } => 1.5m,
        { Month: 4 or 5 or 6 or 9 } => 1.2m,
        _ => 0.85m,
    };

    static bool IsWeekend(DateOnly night) => night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
}

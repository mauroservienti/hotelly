using Hotelly.Seed;

namespace Marketing.Api;

record SeedReview(Guid Id, Guid HotelId, short Rating, string Comment, DateOnly ReviewedOn);

static class MarketingSeed
{
    static readonly string[][] Comments =
    [
        ["Never again.", "Dirty room, rude staff.", "Not what the pictures showed."],
        ["Disappointing.", "Noisy at night.", "Overpriced for what it is."],
        ["Fine for a night.", "Nothing special.", "Good location, tired rooms."],
        ["Very nice stay.", "Friendly staff.", "Great breakfast."],
        ["Perfect in every way.", "We'll be back.", "The best hotel in town."],
    ];

    /// <summary>
    /// Reviews from the last two years. Each hotel has its own quality, which
    /// reviews scatter around, and its own number of reviews: some have none.
    /// Every run seeds the same data.
    /// </summary>
    public static IEnumerable<SeedReview> Reviews(DateOnly today)
    {
        var random = SeedHotels.Random(4);

        foreach (var number in SeedHotels.Numbers)
        {
            var hotelId = SeedHotels.Id(number);
            // Marketing doesn't know stars: quality is its own opinion.
            var quality = 2.5 + random.NextDouble() * 2.3;
            var reviews = random.Next(10) == 0 ? 0 : random.Next(1, 151);

            for (var i = 0; i < reviews; i++)
            {
                var spread = random.NextDouble() + random.NextDouble() - 1;
                var rating = (short)Math.Clamp(Math.Round(quality + spread * 2), 1, 5);
                var comments = Comments[rating - 1];

                yield return new SeedReview(
                    Id(random),
                    hotelId,
                    rating,
                    comments[random.Next(comments.Length)],
                    today.AddDays(-random.Next(730)));
            }
        }
    }

    static Guid Id(Random random)
    {
        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        return new Guid(bytes);
    }
}

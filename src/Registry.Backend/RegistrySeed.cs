using Hotelly.Seed;

namespace Registry.Backend;

record SeedHotel(Guid Id, string Name, string City, int Stars, string[] Amenities, string Description);

static class RegistrySeed
{
    // Weighted: more hotels in bigger cities.
    static readonly (string City, int Weight)[] Cities =
    [
        ("Rome", 30), ("Milan", 20), ("Venice", 15), ("Florence", 15),
        ("Naples", 8), ("Bologna", 6), ("Turin", 6),
    ];

    static readonly string[] Amenities = ["wifi", "parking", "pool", "spa", "gym", "restaurant", "pets", "airport-shuttle"];

    static readonly string[] Adjectives = ["Grand", "Royal", "Little", "Old", "Blue", "Golden", "Quiet", "Central", "Hidden", "Sunny"];
    static readonly string[] Nouns = ["Palace", "Garden", "Bridge", "Tower", "Harbor", "Square", "Fountain", "Villa", "Cloister", "Terrace"];

    public static IEnumerable<SeedHotel> Hotels()
    {
        var random = SeedHotels.Random(1);
        var totalWeight = Cities.Sum(c => c.Weight);

        foreach (var number in SeedHotels.Numbers)
        {
            var city = PickCity(random.Next(totalWeight));
            // Few 1-star and 5-star hotels, mostly 3 and 4.
            var stars = random.Next(100) switch
            {
                < 5 => 1,
                < 20 => 2,
                < 55 => 3,
                < 88 => 4,
                _ => 5,
            };
            // Better hotels have more amenities. Wifi is almost everywhere.
            var amenities = Amenities
                .Where(a => a == "wifi" ? random.Next(100) < 95 : random.Next(10) < stars + 1)
                .ToArray();
            var name = $"{Adjectives[random.Next(Adjectives.Length)]} {Nouns[random.Next(Nouns.Length)]} {city}";

            yield return new SeedHotel(
                SeedHotels.Id(number),
                name,
                city,
                stars,
                amenities,
                $"A {stars}-star hotel in {city}.");
        }
    }

    static string PickCity(int value)
    {
        foreach (var (city, weight) in Cities)
        {
            if (value < weight)
            {
                return city;
            }

            value -= weight;
        }

        throw new InvalidOperationException();
    }
}

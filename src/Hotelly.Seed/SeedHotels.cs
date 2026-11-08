using System.Security.Cryptography;
using System.Text;

namespace Hotelly.Seed;

/// <summary>
/// Demo-only helpers to seed data. Services seed their own data independently,
/// and must agree on hotel IDs without talking to each other: IDs are derived
/// from the hotel number, so every service computes the same ones.
/// </summary>
public static class SeedHotels
{
    public const int Count = 300;

    public static IEnumerable<int> Numbers => Enumerable.Range(1, Count);

    public static Guid Id(int number) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"hotelly-hotel-{number}")));

    /// <summary>
    /// A random generator with a fixed seed, so every run seeds the same data.
    /// Each service uses its own seed.
    /// </summary>
    public static Random Random(int seed) => new(seed);
}

using Microsoft.EntityFrameworkCore;
using Search;

namespace Registry.Search;

/// <summary>
/// Registry's table in the search database, replicated from Registry's own
/// database. Only what search needs: no name, no description.
/// </summary>
class RegistryHotel
{
    public Guid Id { get; set; }
    public string City { get; set; } = "";
    public int Stars { get; set; }
    public string[] Amenities { get; set; } = [];
}

class RegistryModel : ISearchModelContributor
{
    public void Contribute(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<RegistryHotel>(e =>
        {
            e.ToTable("hotels", "registry");
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.City).HasColumnName("city");
            e.Property(h => h.Stars).HasColumnName("stars");
            e.Property(h => h.Amenities).HasColumnName("amenities");
        });
}

/// <summary>
/// Registry owns the list of hotels: search starts from it.
/// </summary>
class RegistryHotelsSource : IHotelsSource
{
    public IQueryable<Guid> Hotels(SearchDbContext db) => db.Set<RegistryHotel>().Select(h => h.Id);
}

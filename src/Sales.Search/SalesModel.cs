using Microsoft.EntityFrameworkCore;
using Search;

namespace Sales.Search;

/// <summary>
/// Sales' table in the search database, replicated from Sales' own database:
/// one row per hotel and night.
/// </summary>
class NightlyRate
{
    public Guid HotelId { get; set; }
    public DateOnly Night { get; set; }
    public decimal Rate { get; set; }
}

class SalesModel : ISearchModelContributor
{
    public void Contribute(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<NightlyRate>(e =>
        {
            e.ToTable("nightly_rates", "sales");
            e.HasKey(r => new { r.HotelId, r.Night });
            e.Property(r => r.HotelId).HasColumnName("hotel_id");
            e.Property(r => r.Night).HasColumnName("night");
            e.Property(r => r.Rate).HasColumnName("rate").HasColumnType("numeric(10, 2)");
        });
}

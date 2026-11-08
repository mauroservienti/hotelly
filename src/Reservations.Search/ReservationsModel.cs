using Microsoft.EntityFrameworkCore;
using Search;

namespace Reservations.Search;

/// <summary>
/// Reservations' table in the search database, replicated from Reservations'
/// own database. Only sold-out nights are replicated: one row per hotel and
/// sold-out night.
/// </summary>
class SoldOutNight
{
    public Guid HotelId { get; set; }
    public DateOnly Night { get; set; }
}

class ReservationsModel : ISearchModelContributor
{
    public void Contribute(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<SoldOutNight>(e =>
        {
            e.ToTable("availability", "reservations");
            e.HasKey(n => new { n.HotelId, n.Night });
            e.Property(n => n.HotelId).HasColumnName("hotel_id");
            e.Property(n => n.Night).HasColumnName("night");
        });
}

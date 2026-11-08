using Microsoft.EntityFrameworkCore;
using Search;

namespace Marketing.Search;

/// <summary>
/// Marketing's table in the search database, replicated from Marketing's own
/// database: one row per review, with the rating only.
/// </summary>
class Review
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public short Rating { get; set; }
}

class MarketingModel : ISearchModelContributor
{
    public void Contribute(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("reviews", "marketing");
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.HotelId).HasColumnName("hotel_id");
            e.Property(r => r.Rating).HasColumnName("rating");
        });
}

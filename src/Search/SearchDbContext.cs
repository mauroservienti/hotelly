using Microsoft.EntityFrameworkCore;

namespace Search;

/// <summary>
/// The search database, as seen by the search process: read-only, and with no
/// knowledge of the services' tables. Each service contributes the mapping of
/// its own tables through an <see cref="ISearchModelContributor"/>.
/// </summary>
public class SearchDbContext(DbContextOptions<SearchDbContext> options, IEnumerable<ISearchModelContributor> contributors)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var contributor in contributors)
        {
            contributor.Contribute(modelBuilder);
        }
    }
}

/// <summary>
/// Maps a service's search tables into the search model.
/// </summary>
public interface ISearchModelContributor
{
    void Contribute(ModelBuilder modelBuilder);
}

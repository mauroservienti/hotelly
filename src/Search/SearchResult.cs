namespace Search;

/// <summary>
/// The result of a search: a page of hotel IDs, the total number of matching
/// hotels, and the facets. IDs only: composition takes care of displaying them.
/// </summary>
public record SearchResult(IReadOnlyList<Guid> HotelIds, int TotalCount, IReadOnlyList<Facet> Facets);

public record Facet(string Name, IReadOnlyList<FacetValue> Values);

public record FacetValue(string Value, int Count);

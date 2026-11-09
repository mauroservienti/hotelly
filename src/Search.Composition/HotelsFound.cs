namespace Search.Composition;

/// <summary>
/// Raised when the search has found a page of hotels. Services subscribe to
/// it and add what they own to each hotel's view model, loading the whole
/// page in one call. Never raised for an empty page: subscribers can assume
/// there's at least one hotel.
/// </summary>
/// <param name="Request">The search request, for example to price the stay.</param>
/// <param name="Hotels">The hotels' view models, by hotel ID.</param>
public record HotelsFound(SearchRequest Request, IReadOnlyDictionary<Guid, dynamic> Hotels);

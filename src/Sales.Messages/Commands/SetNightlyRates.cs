namespace Sales.Messages.Commands;

/// <summary>
/// Sets the hotel's nightly rate for every night from <paramref name="From"/>
/// to <paramref name="Until"/>, excluded. Nights already priced get the new
/// rate.
/// </summary>
public record SetNightlyRates(Guid HotelId, DateOnly From, DateOnly Until, decimal Rate);

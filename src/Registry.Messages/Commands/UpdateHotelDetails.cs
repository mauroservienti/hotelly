namespace Registry.Messages.Commands;

/// <summary>
/// Updates the details Registry owns for an existing hotel.
/// </summary>
public record UpdateHotelDetails(Guid HotelId, string Name, string City, int Stars, string[] Amenities, string Description);

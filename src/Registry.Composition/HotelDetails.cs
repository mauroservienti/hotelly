using Microsoft.AspNetCore.Mvc;

namespace Registry.Composition;

/// <summary>
/// The hotel details Registry owns, as the back office sends them.
/// </summary>
public class HotelDetails
{
    public string? Name { get; set; }
    public string? City { get; set; }
    public int Stars { get; set; }
    public string[]? Amenities { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Validates the details: returns the response to reject them with, or null.
    /// </summary>
    public ActionResult? Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors["registry.name"] = ["The name is required."];
        }

        if (string.IsNullOrWhiteSpace(City))
        {
            errors["registry.city"] = ["The city is required."];
        }

        if (Stars is < 1 or > 5)
        {
            errors["registry.stars"] = ["Stars go from 1 to 5."];
        }

        return errors.Count == 0
            ? null
            : new BadRequestObjectResult(new ValidationProblemDetails(errors) { Title = "The hotel details aren't valid." });
    }

    public string[] NormalizedAmenities() =>
        (Amenities ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim().ToLowerInvariant()).Distinct().ToArray();
}

/// <summary>
/// Registry's part of the request creating a hotel. Each service binds only
/// its own part of the body.
/// </summary>
public class NewHotelRequest
{
    public HotelDetails? Registry { get; set; }
}

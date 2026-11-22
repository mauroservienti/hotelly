using Microsoft.AspNetCore.Mvc;

namespace Sales.Composition;

/// <summary>
/// A nightly rate for a range of nights, as the back office sends it.
/// </summary>
public class NightlyRates
{
    // Rates can be set up to a year ahead, like the seed data.
    const int MaxNights = 366;

    public DateOnly? From { get; set; }
    public DateOnly? Until { get; set; }
    public decimal Rate { get; set; }

    /// <summary>
    /// Validates the rates: returns the response to reject them with, or null.
    /// </summary>
    public ActionResult? Validate(DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        if (Rate <= 0)
        {
            errors["sales.rate"] = ["The nightly rate must be greater than zero."];
        }

        if (From is not { } from || Until is not { } until || until <= from)
        {
            errors["sales.until"] = ["The last night must come after the first one."];
        }
        else if (from < today || until.DayNumber - from.DayNumber > MaxNights)
        {
            errors["sales.from"] = [$"Rates can be set from today, for up to {MaxNights} nights."];
        }

        return errors.Count == 0
            ? null
            : new BadRequestObjectResult(new ValidationProblemDetails(errors) { Title = "The nightly rates aren't valid." });
    }
}

/// <summary>
/// Sales' part of the request creating a hotel: one nightly rate for the next
/// year. Each service binds only its own part of the body.
/// </summary>
public class NewHotelRequest
{
    public NewHotelRates? Sales { get; set; }
}

public class NewHotelRates
{
    public decimal NightlyRate { get; set; }
}

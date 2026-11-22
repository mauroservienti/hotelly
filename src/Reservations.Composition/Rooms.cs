using Microsoft.AspNetCore.Mvc;

namespace Reservations.Composition;

/// <summary>
/// The rooms a hotel has for a range of nights, as the back office sends them.
/// </summary>
public class Rooms
{
    const int MaxNights = 366;

    public DateOnly? From { get; set; }
    public DateOnly? Until { get; set; }
    public int Count { get; set; }

    /// <summary>
    /// Validates the rooms: returns the response to reject them with, or null.
    /// </summary>
    public ActionResult? Validate(DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        if (Count < 1)
        {
            errors["reservations.rooms"] = ["A hotel has at least one room."];
        }

        if (From is not { } from || Until is not { } until || until <= from)
        {
            errors["reservations.until"] = ["The last night must come after the first one."];
        }
        else if (from < today || until.DayNumber - from.DayNumber > MaxNights)
        {
            errors["reservations.from"] = [$"Rooms can be set from today, for up to {MaxNights} nights."];
        }

        return errors.Count == 0
            ? null
            : new BadRequestObjectResult(new ValidationProblemDetails(errors) { Title = "The rooms aren't valid." });
    }
}

/// <summary>
/// Reservations' part of the request creating a hotel: the number of rooms
/// for the next year. Each service binds only its own part of the body.
/// </summary>
public class NewHotelRequest
{
    public NewHotelRooms? Reservations { get; set; }
}

public class NewHotelRooms
{
    public int Rooms { get; set; }
}

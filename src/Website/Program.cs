using Hotelly.BackOffice;
using Marketing.BackOffice;
using Registry.BackOffice;
using Reservations.BackOffice;
using Sales.BackOffice;

var builder = WebApplication.CreateBuilder(args);

// The website is a client of the composition gateway, like any frontend. It
// forwards searches to the gateway, so the page calls its own origin: no CORS.
// Aspire passes the gateway's address.
builder.Services.AddHttpClient("gateway", client =>
    client.BaseAddress = new Uri(builder.Configuration["services:composition-gateway:http:0"]!));

// The back office hosts the services' panels. Each service ships its panel as
// a Razor class library, and the website references it: adding a service's
// panel means referencing its library and registering its panel here.
builder.Services.AddSingleton<IReadOnlyList<BackOfficePanel>>([
    RegistryBackOffice.Panel,
    SalesBackOffice.Panel,
    ReservationsBackOffice.Panel,
    MarketingBackOffice.Panel,
]);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/search", async (HttpRequest request, IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    using var response = await clients.CreateClient("gateway").GetAsync($"/search{request.QueryString}", cancellationToken);
    return Results.Content(
        await response.Content.ReadAsStringAsync(cancellationToken),
        "application/json",
        statusCode: (int)response.StatusCode);
});

// The back office page asks which panels to load.
app.MapGet("/back-office/panels", (IReadOnlyList<BackOfficePanel> panels) =>
    panels.OrderBy(panel => panel.Order));

// The back office reads and writes hotels through the gateway, like the
// search page. Writes carry the idempotency key the page generates.
app.MapMethods("/back-office/hotels/{**path}", [HttpMethods.Get, HttpMethods.Post],
    async (HttpRequest request, IHttpClientFactory clients, CancellationToken cancellationToken) =>
    {
        using var forwarded = new HttpRequestMessage(new HttpMethod(request.Method), $"{request.Path}{request.QueryString}");
        if (HttpMethods.IsPost(request.Method))
        {
            forwarded.Content = new StreamContent(request.Body);
            if (request.ContentType is { } contentType)
            {
                forwarded.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
        }

        if (request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
        {
            forwarded.Headers.TryAddWithoutValidation("Idempotency-Key", (IEnumerable<string>)idempotencyKey);
        }

        using var response = await clients.CreateClient("gateway").SendAsync(forwarded, cancellationToken);
        return Results.Content(
            await response.Content.ReadAsStringAsync(cancellationToken),
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    });

app.Run();

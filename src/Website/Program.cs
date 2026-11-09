var builder = WebApplication.CreateBuilder(args);

// The website is a client of the composition gateway, like any frontend. It
// forwards searches to the gateway, so the page calls its own origin: no CORS.
// Aspire passes the gateway's address.
builder.Services.AddHttpClient("gateway", client =>
    client.BaseAddress = new Uri(builder.Configuration["services:composition-gateway:http:0"]!));

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

app.Run();

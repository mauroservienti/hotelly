var builder = DistributedApplication.CreateBuilder(args);

// Each service owns its database server. Search has its own server too: it
// holds a copy of what search needs, replicated from the services' servers.

// The search database: one schema per service, and a role per service that
// can write only to its own schema. See postgres/01-search-database.sql.
// The init script runs against POSTGRES_DB, so the search database is created
// by the PostgreSQL image, not by Aspire. Services connect with their own role.
var searchServer = builder.AddPostgres("search-db")
    .WithImageTag("18")
    .WithEnvironment("POSTGRES_DB", "search")
    .WithInitFiles("../../postgres")
    .WithPgAdmin();

var registryServer = builder.AddPostgres("registry-db")
    .WithImageTag("18")
    // Logical replication publishes changes to the search database.
    .WithArgs("-c", "wal_level=logical")
    .WithPgAdmin();
var registryDatabase = registryServer.AddDatabase("registry");

builder.AddProject<Projects.Registry_Api>("registry-api")
    .WithReference(registryDatabase)
    .WithEnvironment("ConnectionStrings__search", SearchConnectionString(searchServer, "registry"))
    // The search database connects to Registry's server from its container,
    // so it uses the container network: the resource name and the target port.
    .WithEnvironment("Registry__Publisher", ReferenceExpression.Create(
        $"host={registryServer.Resource.Name} port=5432 dbname=registry user=postgres password={registryServer.Resource.PasswordParameter}"))
    .WaitFor(registryDatabase)
    .WaitFor(searchServer);

builder.Build().Run();

// Each role's password matches its name: it's a demo.
static ReferenceExpression SearchConnectionString(IResourceBuilder<PostgresServerResource> server, string role)
{
    var endpoint = server.Resource.PrimaryEndpoint;
    return ReferenceExpression.Create(
        $"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database=search;Username={role};Password={role}");
}

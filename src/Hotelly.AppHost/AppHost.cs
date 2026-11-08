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

var registryApi = AddService<Projects.Registry_Api>("registry");
var salesApi = AddService<Projects.Sales_Api>("sales");
var reservationsApi = AddService<Projects.Reservations_Api>("reservations");
var marketingApi = AddService<Projects.Marketing_Api>("marketing");

// The gateway hosts the search process and the services' search providers.
// It reads from the search database with the read-only search role.
builder.AddProject<Projects.CompositionGateway>("composition-gateway")
    .WithEnvironment("ConnectionStrings__search", SearchConnectionString(searchServer, "search"))
    .WaitFor(searchServer)
    .WaitFor(registryApi)
    .WaitFor(salesApi)
    .WaitFor(reservationsApi)
    .WaitFor(marketingApi);

builder.Build().Run();

// A service with its own PostgreSQL server and database, named after the
// service, replicating its search tables to the search database.
IResourceBuilder<ProjectResource> AddService<TProject>(string name) where TProject : IProjectMetadata, new()
{
    var server = builder.AddPostgres($"{name}-db")
        .WithImageTag("18")
        // Logical replication publishes changes to the search database.
        // While the search database is unreachable, this server keeps the
        // changes search hasn't confirmed yet. Past 1 GB it stops waiting: the
        // replication slot is invalidated, and search needs a full resync of
        // the service's tables.
        .WithArgs("-c", "wal_level=logical", "-c", "max_slot_wal_keep_size=1GB")
        .WithPgAdmin();
    var database = server.AddDatabase(name);

    return builder.AddProject<TProject>($"{name}-api")
        .WithReference(database)
        .WithEnvironment("ConnectionStrings__search", SearchConnectionString(searchServer, name))
        // The search database connects to the service's server from its
        // container, so it uses the container network: the resource name and
        // the target port.
        .WithEnvironment("Replication__Publisher", ReferenceExpression.Create(
            $"host={server.Resource.Name} port=5432 dbname={name} user=postgres password={server.Resource.PasswordParameter}"))
        .WaitFor(database)
        .WaitFor(searchServer);
}

// Each role's password matches its name: it's a demo.
static ReferenceExpression SearchConnectionString(IResourceBuilder<PostgresServerResource> server, string role)
{
    var endpoint = server.Resource.PrimaryEndpoint;
    return ReferenceExpression.Create(
        $"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database=search;Username={role};Password={role}");
}

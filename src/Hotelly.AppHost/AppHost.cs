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
    // Each subscription takes a logical replication worker, plus up to two
    // more while it copies the initial data. The default, 4 workers, isn't
    // enough for four services: the last subscriptions never get their
    // initial copy. Logical replication workers count toward the worker
    // processes too.
    .WithArgs("-c", "max_logical_replication_workers=16", "-c", "max_worker_processes=24")
    .WithPgAdmin();

var registry = AddService<Projects.Registry_Backend>("registry");
var sales = AddService<Projects.Sales_Backend>("sales");
var reservations = AddService<Projects.Reservations_Backend>("reservations");
var marketing = AddService<Projects.Marketing_Backend>("marketing");

// The gateway hosts the search process, and the services' search providers
// and composition handlers. It reads from the search database with the
// read-only search role. Composition handlers belong to their services, and
// read from their services' databases with a read-only role.
builder.AddProject<Projects.CompositionGateway>("composition-gateway")
    .WithEnvironment("ConnectionStrings__search", ConnectionString(searchServer, "search", "search"))
    .WithEnvironment("ConnectionStrings__registry", registry.CompositionConnectionString)
    .WithEnvironment("ConnectionStrings__sales", sales.CompositionConnectionString)
    .WithEnvironment("ConnectionStrings__marketing", marketing.CompositionConnectionString)
    .WaitFor(searchServer)
    .WaitFor(registry.Backend)
    .WaitFor(sales.Backend)
    .WaitFor(reservations.Backend)
    .WaitFor(marketing.Backend);

builder.Build().Run();

// A service with its own PostgreSQL server and database, named after the
// service, replicating its search tables to the search database.
Service AddService<TProject>(string name) where TProject : IProjectMetadata, new()
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

    var backend = builder.AddProject<TProject>($"{name}-backend")
        .WithReference(database)
        .WithEnvironment("ConnectionStrings__search", ConnectionString(searchServer, "search", name))
        // The search database connects to the service's server from its
        // container, so it uses the container network: the resource name and
        // the target port.
        .WithEnvironment("Replication__Publisher", ReferenceExpression.Create(
            $"host={server.Resource.Name} port=5432 dbname={name} user=postgres password={server.Resource.PasswordParameter}"))
        .WaitFor(database)
        .WaitFor(searchServer);

    // The service creates the read-only role its composition handlers use.
    return new Service(backend, ConnectionString(server, name, $"{name}_composition"));
}

// Each role's password matches its name: it's a demo.
static ReferenceExpression ConnectionString(IResourceBuilder<PostgresServerResource> server, string database, string role)
{
    var endpoint = server.Resource.PrimaryEndpoint;
    return ReferenceExpression.Create(
        $"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database={database};Username={role};Password={role}");
}

record Service(IResourceBuilder<ProjectResource> Backend, ReferenceExpression CompositionConnectionString);

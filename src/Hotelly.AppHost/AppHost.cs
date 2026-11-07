var builder = DistributedApplication.CreateBuilder(args);

// The search database: one schema per service, and a role per service
// that can write only to its own schema. See postgres/01-search-database.sql.
// The init script runs against POSTGRES_DB, so the search database is created
// by the PostgreSQL image, not by Aspire. Services connect with their own role.
builder.AddPostgres("postgres")
    .WithImageTag("18")
    .WithEnvironment("POSTGRES_DB", "search")
    .WithInitFiles("../../postgres")
    .WithPgAdmin();

builder.Build().Run();

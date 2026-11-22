using NServiceBus.Persistence.Sql;

// The gateway's outbox lives in PostgreSQL.
[assembly: SqlPersistenceSettings(PostgreSqlScripts = true)]

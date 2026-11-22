using System.Text.Json;
using Hotelly.Messaging;
using Marketing.Search;
using Registry.Search;
using Reservations.Search;
using Sales.Search;
using Search;
using Npgsql;
using NpgsqlTypes;
using NServiceBus;
using NServiceBus.TransactionalSession;
using ServiceComposer.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Services deploy their search providers here, the same way they deploy
// composition handlers.
builder.Services.AddHotelSearch(
    builder.Configuration.GetConnectionString("search")!,
    RegistrySearch.Assembly,
    SalesSearch.Assembly,
    ReservationsSearch.Assembly,
    MarketingSearch.Assembly);

// The gateway is an NServiceBus endpoint too: composition handlers handling
// writes send commands to their services' back ends. The outbox and the
// transactional session, in the gateway's own database, make sending atomic:
// all the commands of a request are sent, or none.
var endpointConfiguration = HotellyEndpoint.Create("Hotelly.CompositionGateway", builder.Configuration);
var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
var dialect = persistence.SqlDialect<SqlDialect.PostgreSql>();
dialect.JsonBParameterModifier(parameter => ((NpgsqlParameter)parameter).NpgsqlDbType = NpgsqlDbType.Jsonb);
var gatewayConnectionString = builder.Configuration.GetConnectionString("gateway")!;
persistence.ConnectionBuilder(() => new NpgsqlConnection(gatewayConnectionString));
// PostgreSQL table prefixes are limited to 20 characters: the default, based
// on the endpoint name, is too long.
persistence.TablePrefix("gateway_");
persistence.EnableTransactionalSession();
endpointConfiguration.EnableOutbox();
builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

// Heyi wraps composition requests in the transactional session. Reads run
// without one.
builder.Services.AddHeyi(options => options.OpenSessionOptions(() => new SqlPersistenceOpenSessionOptions()));

// Composition handlers are found by scanning the assemblies deployed with the
// gateway. Output formatters let handlers reject a request with an action
// result, such as a 400, which also rolls back the commands they sent.
// Composed view models are dynamic, serialized as dictionaries: their keys
// need the camel case policy too.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddViewModelComposition(options => options.ResponseSerialization.UseOutputFormatters = true);

var app = builder.Build();

app.MapCompositionHandlers().WithTransactionalSession();

app.Run();

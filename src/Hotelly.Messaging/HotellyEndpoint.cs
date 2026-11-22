using Microsoft.Extensions.Configuration;
using NServiceBus;

namespace Hotelly.Messaging;

/// <summary>
/// The NServiceBus configuration every Hotelly endpoint shares: RabbitMQ as
/// the transport, JSON as the serializer, and message conventions. Services
/// own their endpoints: this is plumbing, not shared business code.
/// </summary>
public static class HotellyEndpoint
{
    public static EndpointConfiguration Create(string endpointName, IConfiguration configuration)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();

        // Aspire passes the broker's address as an AMQP URI. The transport
        // also checks the broker through the management API, with the same
        // credentials.
        var connectionString = configuration.GetConnectionString("rabbitmq")!;
        var credentials = new Uri(connectionString).UserInfo.Split(':', 2);
        endpointConfiguration.UseTransport(new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), connectionString)
        {
            ManagementApiConfiguration = new ManagementApiConfiguration(
                configuration["RabbitMQ:ManagementApi"]!,
                Uri.UnescapeDataString(credentials[0]),
                Uri.UnescapeDataString(credentials[1]))
        });

        // Message contracts are plain classes: no NServiceBus reference in
        // the projects defining them.
        var conventions = endpointConfiguration.Conventions();
        conventions.DefiningCommandsAs(type => type.Namespace?.EndsWith(".Messages.Commands") == true);
        conventions.DefiningEventsAs(type => type.Namespace?.EndsWith(".Messages.Events") == true);

        // A demo: queues, exchanges, and tables are created at startup.
        endpointConfiguration.EnableInstallers();

        return endpointConfiguration;
    }
}

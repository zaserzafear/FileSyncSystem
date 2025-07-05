using RabbitMQ.Client;
using System.Reflection;

namespace DistributedFileStorage.Messaging;

public static class MessageBusServiceExtensions
{
    public static IServiceCollection AddMessageConsumers(this IServiceCollection services, params Type[] markerTypes)
    {
        var assemblies = markerTypes.Select(t => t.Assembly).Distinct().ToArray();
        return services.AddMessageConsumers(assemblies);
    }

    public static IServiceCollection AddMessageConsumers(this IServiceCollection services, params Assembly[] assemblies)
    {
        var handlerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IMessageBusHandle).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            services.AddSingleton(typeof(IMessageBusHandle), handlerType);
        }

        services.AddHostedService<MessageBusHandlerRegistrar>();
        return services;
    }

    public static IServiceCollection AddRabbitMqMessageBus(this IServiceCollection services, string amqpUri)
    {
        services.AddSingleton<IConnection>(sp =>
        {
            return CreateRabbitMqConnectionAsync(amqpUri).GetAwaiter().GetResult();
        });

        services.AddHealthChecks()
            .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>());

        services.AddSingleton<IMessageBus>(provider =>
            RabbitMqMessageBus.CreateAsync(
                provider.GetRequiredService<IConnection>(),
                provider.GetRequiredService<ILogger<RabbitMqMessageBus>>()).Result);

        return services;
    }

    private static async Task<IConnection> CreateRabbitMqConnectionAsync(string amqpUri)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(amqpUri),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        return await factory.CreateConnectionAsync();
    }
}

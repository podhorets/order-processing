using System.Reflection;

namespace OrderService.Infrastructure.Extensions;

public static class EndpointExtensions
{
    public static void MapEndpoints(this WebApplication app)
    {
        var endpoints = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpoint>();

        foreach (var endpoint in endpoints)
            endpoint.Map(app);
    }
}
namespace OrderService.Infrastructure.Extensions;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
namespace OrderService.Infrastructure.Http;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

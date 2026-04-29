namespace InventoryService.Infrastructure.Http;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

using FluentValidation;
using Shared.Contracts.Dto.V1;

namespace OrderService.Features.SubmitOrder;

public sealed class SubmitOrderRequestValidator : AbstractValidator<SubmitOrderRequest>
{
    public SubmitOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId must not be empty.");

        RuleFor(x => x.OrderItems)
            .NotEmpty()
            .WithMessage("Order must contain at least one item.");

        RuleForEach(x => x.OrderItems).SetValidator(new OrderItemDtoValidator());
    }
}

internal sealed class OrderItemDtoValidator : AbstractValidator<OrderItemDto>
{
    public OrderItemDtoValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty()
            .WithMessage("SKU must not be empty.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Unit price must not be negative.");
    }
}
using FluentValidation;

namespace OrderService.Features.SubmitOrder;

public sealed class SubmitOrderRequestValidator : AbstractValidator<SubmitOrderRequest>
{
    public SubmitOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Sku).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.Sku.ToLowerInvariant()).Distinct().Count() == items.Count)
            .WithMessage("Duplicate SKUs are not allowed.");
    }
}

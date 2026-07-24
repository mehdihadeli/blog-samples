using FluentValidation;

namespace ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;

internal sealed class CreateProductValidator : AbstractValidator<CreateProduct>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}

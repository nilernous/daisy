using Daisy.Application.Common;
using FluentValidation;

namespace Daisy.Application.Features.Products;

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).Slug(250);
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Material).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.GenderTarget)
            .Must(g => g is null || ValidationRules.GenderTargets.Contains(g))
            .WithMessage($"Gender target must be one of: {string.Join(", ", ValidationRules.GenderTargets)}.");
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0);
    }
}

public sealed class SaleRequestValidator : AbstractValidator<SaleRequest>
{
    public SaleRequestValidator()
    {
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndAt)
            .Must((request, end) => request.StartAt is null || end is null || request.StartAt < end)
            .WithMessage("Sale start must be before sale end.");
    }
}

public sealed class VariantRequestValidator : AbstractValidator<VariantRequest>
{
    public VariantRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Size).MaximumLength(20);
        RuleFor(x => x.Color).MaximumLength(50);
        RuleFor(x => x.ColorHex)
            .Must(c => c is null || ValidationRules.ColorHexRegex().IsMatch(c))
            .WithMessage("Color hex must look like #1A2B3C.");
        RuleFor(x => x.ExtraPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).When(x => x.StockQuantity is not null);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
    }
}

using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Features.Products;

public sealed record VariantInput(
    string Sku,
    string? Name,
    decimal Price,
    decimal? CompareAtPrice = null,
    IReadOnlyList<string>? Barcodes = null,
    bool SoldByWeight = false)
{
    internal VariantSpec ToSpec() => new(Sku, Name, Price, CompareAtPrice, Barcodes, SoldByWeight);
}

internal sealed class VariantInputValidator : AbstractValidator<VariantInput>
{
    public VariantInputValidator()
    {
        RuleFor(v => v.Sku).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("SKU may contain letters, digits, dot, dash and underscore.");
        RuleFor(v => v.Name).MaximumLength(100);
        RuleFor(v => v.Price).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(v => v.CompareAtPrice).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleForEach(v => v.Barcodes).NotEmpty().MaximumLength(64);
    }
}

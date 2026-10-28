using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Features.DeliveryZones;

/// <param name="FreeDeliveryThreshold">Order subtotal from which delivery is free; null when it never is.</param>
/// <param name="SortOrder">Display order for shoppers, lowest first.</param>
public sealed record DeliveryZoneInput(
    string Name,
    IReadOnlyList<string> Areas,
    decimal Fee,
    decimal? FreeDeliveryThreshold,
    bool IsActive = true,
    int SortOrder = 0)
{
    internal DeliveryZoneSpec ToSpec() => new(Name, Areas, Fee, FreeDeliveryThreshold, IsActive, SortOrder);

    internal sealed class Validator : AbstractValidator<DeliveryZoneInput>
    {
        public const int MaxAreas = 200;

        public Validator()
        {
            RuleFor(zone => zone.Name).NotEmpty().MaximumLength(100);
            RuleFor(zone => zone.Areas).NotNull().Must(areas => areas.Count <= MaxAreas).WithMessage($"A zone can list at most {MaxAreas} areas.");
            RuleForEach(zone => zone.Areas).NotEmpty().MaximumLength(100);
            RuleFor(zone => zone.Fee).GreaterThanOrEqualTo(0m).PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(zone => zone.FreeDeliveryThreshold!.Value).GreaterThan(0m).PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .OverridePropertyName(nameof(FreeDeliveryThreshold))
                .When(zone => zone.FreeDeliveryThreshold.HasValue);
            RuleFor(zone => zone.SortOrder).InclusiveBetween(0, 10_000);
        }
    }
}

namespace ClarioShop.Ordering.Domain;

/// <summary>Where the order goes, copied onto the order so later address edits never change it.</summary>
internal sealed record DeliveryAddress(string Line1, string? Line2, string Area, string City, string? PostalCode);

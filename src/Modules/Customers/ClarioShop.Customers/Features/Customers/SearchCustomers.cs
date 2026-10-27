using ClarioShop.Customers.Domain;
using ClarioShop.Customers.Persistence;

namespace ClarioShop.Customers.Features.Customers;

public static class SearchCustomers
{
    /// <param name="Search">Part of a name, email or phone number (any format, e.g. "01712" or "+8801712").</param>
    public sealed record Query(string? Search, int Page = 1, int PageSize = 25);

    public sealed record CustomerItem(Guid Id, string Phone, string? Name, string? Email, int AddressCount, DateTimeOffset CreatedAt);

    public sealed record Response(IReadOnlyList<CustomerItem> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
            RuleFor(query => query.Search).MaximumLength(100);
        }
    }

    internal sealed class Handler(CustomersDbContext db) : ICommandHandler<Query, Response>
    {
        /// <summary>Fewer digits than this match too many numbers to be a phone search.</summary>
        private const int MinPhoneDigits = 3;

        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var customers = db.Customers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                var pattern = $"%{EscapeLike(search)}%";
                var digits = PhoneNumber.SearchDigits(search);
                var phonePattern = digits.Length >= MinPhoneDigits ? $"%{digits}%" : null;
                customers = customers.Where(c =>
                    EF.Functions.ILike(c.Name!, pattern, "\\")
                    || EF.Functions.ILike(c.Email!, pattern, "\\")
                    || (phonePattern != null && EF.Functions.Like(c.Phone, phonePattern)));
            }

            var total = await customers.CountAsync(cancellationToken);
            var items = await customers
                .OrderByDescending(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(c => new CustomerItem(c.Id, c.Phone, c.Name, c.Email, c.Addresses.Count, c.CreatedAt))
                .ToListAsync(cancellationToken);

            return new Response(items, query.Page, query.PageSize, total);
        }

        private static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
    }
}

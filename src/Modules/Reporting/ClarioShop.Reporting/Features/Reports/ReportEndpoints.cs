using ClarioShop.Reporting.Contracts;

namespace ClarioShop.Reporting.Features.Reports;

internal sealed class ReportEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports");

        group.MapGet("/sales/daily", static async ([AsParameters] GetDailySales.Query query, GetDailySales.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("GetDailySales")
            .WithSummary("Sales per store day (orders confirmed that day, net of cancellations and returns), every day of the period listed.")
            .Produces<GetDailySales.Response>()
            .ProducesValidationProblem()
            .RequirePermission(ReportingPermissions.SalesView);

        group.MapGet("/sales/top-variants", static async ([AsParameters] GetTopVariants.Query query, GetTopVariants.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("GetTopVariants")
            .WithSummary("The best-selling variants of the period, by revenue or quantity.")
            .WithValidation<GetTopVariants.Query>()
            .Produces<GetTopVariants.Response>()
            .RequirePermission(ReportingPermissions.SalesView);

        group.MapGet("/orders/by-status", static async ([AsParameters] GetOrdersByStatus.Query query, GetOrdersByStatus.Handler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToHttpResult())
            .WithName("GetOrdersByStatus")
            .WithSummary("How many of the orders placed in the period are in each status now, and what they are worth.")
            .Produces<GetOrdersByStatus.Response>()
            .ProducesValidationProblem()
            .RequirePermission(ReportingPermissions.SalesView);
    }
}

using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class OrderStatusesContractTests
{
    [Theory]
    [InlineData(OrderStatuses.Shipped, "Shipped")]
    [InlineData(OrderStatuses.Delivered, "Delivered")]
    public void OrderStatuses_Constant_IsAnOrderStatusName(string published, string status) =>
        Assert.Equal(Enum.Parse<OrderStatus>(status).ToString(), published);
}

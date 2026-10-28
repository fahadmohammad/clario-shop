using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class OrderStatusesContractTests
{
    [Theory]
    [InlineData(OrderStatuses.PendingPayment, "PendingPayment")]
    [InlineData(OrderStatuses.Shipped, "Shipped")]
    [InlineData(OrderStatuses.Delivered, "Delivered")]
    public void OrderStatuses_Constant_IsAnOrderStatusName(string published, string status) =>
        Assert.Equal(Enum.Parse<OrderStatus>(status).ToString(), published);

    [Theory]
    [InlineData(OrderPaymentMethods.CashOnDelivery, "CashOnDelivery")]
    [InlineData(OrderPaymentMethods.SslCommerz, "SslCommerz")]
    public void OrderPaymentMethods_Constant_IsAPaymentMethodName(string published, string method) =>
        Assert.Equal(Enum.Parse<PaymentMethod>(method).ToString(), published);
}

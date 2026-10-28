using System.Net;
using System.Text;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Gateways.SslCommerz;
using ClarioShop.Settings.Contracts;
using ClarioShop.SharedKernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClarioShop.Payments.Tests.Gateways;

public sealed class SslCommerzGatewayTests
{
    private static readonly SslCommerzCredentials Sandbox = new("clario0test", "s3cret&pass", UseSandbox: true);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateSessionAsync_GatewayAccepts_PostsFormToSandboxAndReturnsPaymentPage()
    {
        var handler = new StubHandler(Json("""{"status":"SUCCESS","sessionkey":"SESS123","GatewayPageURL":"https://sandbox.sslcommerz.com/EasyCheckOut/testcdeSESS123"}"""));

        var session = await Gateway(handler, Sandbox).CreateSessionAsync(Request(), Cancel);

        Assert.Equal(new GatewaySession("SESS123", new Uri("https://sandbox.sslcommerz.com/EasyCheckOut/testcdeSESS123")), session.Value);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Post, "https://sandbox.sslcommerz.com/gwprocess/v4/api.php"), (sent.Method, sent.Uri.AbsoluteUri));
        var form = Form(sent.Body);
        Assert.Equal(("clario0test", "s3cret&pass", "1250.50", "BDT", "PAY-1"), (form["store_id"], form["store_passwd"], form["total_amount"], form["currency"], form["tran_id"]));
        Assert.Equal(("https://shop.test/ipn", "https://shop.test/success", "https://shop.test/fail", "https://shop.test/cancel"), (form["ipn_url"], form["success_url"], form["fail_url"], form["cancel_url"]));
        Assert.Equal(("Rahim Uddin", "+8801712345678", "rahim@shop.test", "House 12, Road 5", "Dhaka", "1205"), (form["cus_name"], form["cus_phone"], form["cus_email"], form["cus_add1"], form["cus_city"], form["cus_postcode"]));
        Assert.Equal(("3", "CS-000123", "Courier"), (form["num_of_item"], form["value_a"], form["shipping_method"]));
    }

    [Fact]
    public async Task CreateSessionAsync_LiveAccount_UsesLiveEndpoint()
    {
        var handler = new StubHandler(Json("""{"status":"SUCCESS","sessionkey":"S","GatewayPageURL":"https://securepay.sslcommerz.com/gwprocess/v4/gw.php?Q=pay"}"""));

        await Gateway(handler, Sandbox with { UseSandbox = false }).CreateSessionAsync(Request(), Cancel);

        Assert.Equal("https://securepay.sslcommerz.com/gwprocess/v4/api.php", Assert.Single(handler.Requests).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task CreateSessionAsync_NoPostalCode_LeavesPostcodeOut()
    {
        var handler = new StubHandler(Json("""{"status":"SUCCESS","sessionkey":"S","GatewayPageURL":"https://sandbox.sslcommerz.com/pay"}"""));
        var request = Request() with { Customer = Request().Customer with { PostalCode = null } };

        await Gateway(handler, Sandbox).CreateSessionAsync(request, Cancel);

        Assert.False(Form(Assert.Single(handler.Requests).Body).ContainsKey("cus_postcode"));
    }

    [Theory]
    [InlineData("""{"status":"FAILED","failedreason":"Store Credential Error Or Store is De-active"}""")]
    [InlineData("""{"status":"SUCCESS","sessionkey":"S"}""")]
    [InlineData("""{"status":"SUCCESS","sessionkey":"S","GatewayPageURL":"http://sandbox.sslcommerz.com/pay"}""")]
    public async Task CreateSessionAsync_GatewayRefusesOrAnswersIncompletely_ReturnsRefused(string body)
    {
        var session = await Gateway(new StubHandler(Json(body)), Sandbox).CreateSessionAsync(Request(), Cancel);

        Assert.Equal(("payments.gateway_refused", ErrorType.Unavailable), (session.Error.Code, session.Error.Type));
    }

    [Fact]
    public async Task CreateSessionAsync_NoMerchantAccount_ReturnsNotConfiguredWithoutCallingGateway()
    {
        var handler = new StubHandler(Json("{}"));

        var session = await Gateway(handler, credentials: null).CreateSessionAsync(Request(), Cancel);

        Assert.Equal(("payments.gateway_not_configured", ErrorType.Conflict), (session.Error.Code, session.Error.Type));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateSessionAsync_ServerError_ReturnsUnavailable()
    {
        var session = await Gateway(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)), Sandbox).CreateSessionAsync(Request(), Cancel);

        Assert.Equal("payments.gateway_unavailable", session.Error.Code);
    }

    [Fact]
    public async Task CreateSessionAsync_NotJson_ReturnsUnavailable()
    {
        var html = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>maintenance</html>") });

        var session = await Gateway(html, Sandbox).CreateSessionAsync(Request(), Cancel);

        Assert.Equal("payments.gateway_unavailable", session.Error.Code);
    }

    [Fact]
    public async Task CreateSessionAsync_ConnectionFails_ReturnsUnavailable()
    {
        var session = await Gateway(new StubHandler(_ => throw new HttpRequestException("connection refused")), Sandbox).CreateSessionAsync(Request(), Cancel);

        Assert.Equal("payments.gateway_unavailable", session.Error.Code);
    }

    [Fact]
    public async Task CreateSessionAsync_TimesOut_ReturnsUnavailable()
    {
        var session = await Gateway(new StubHandler(_ => throw new TaskCanceledException("timed out")), Sandbox).CreateSessionAsync(Request(), Cancel);

        Assert.Equal("payments.gateway_unavailable", session.Error.Code);
    }

    [Fact]
    public async Task CreateSessionAsync_CallerCancels_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Gateway(new StubHandler(Json("{}")), Sandbox).CreateSessionAsync(Request(), cancelled.Token));
    }

    [Fact]
    public async Task ValidateAsync_Valid_QueriesWithCredentialsAndMapsTransaction()
    {
        var handler = new StubHandler(Json("""
            {"status":"VALID","tran_date":"2026-10-28 15:30:01","tran_id":"PAY-1","val_id":"VAL 1","amount":"1250.50","store_amount":"1219.24",
             "currency":"BDT","bank_tran_id":"BANK-9","card_type":"BKASH-BKash","risk_level":"0","risk_title":"Safe"}
            """));

        var transaction = await Gateway(handler, Sandbox).ValidateAsync("VAL 1", Cancel);

        Assert.Equal(new GatewayTransaction("PAY-1", "VAL 1", GatewayTransactionStatus.Valid, 1250.50m, "BDT", "BANK-9", "BKASH-BKash", IsRisky: false), transaction.Value);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Get, "/validator/api/validationserverAPI.php"), (sent.Method, sent.Uri.AbsolutePath));
        Assert.Equal("?val_id=VAL%201&store_id=clario0test&store_passwd=s3cret%26pass&format=json", sent.Uri.Query);
    }

    [Theory]
    [InlineData("VALIDATED", nameof(GatewayTransactionStatus.AlreadyValidated))]
    [InlineData("INVALID_TRANSACTION", nameof(GatewayTransactionStatus.Invalid))]
    [InlineData("FAILED", nameof(GatewayTransactionStatus.Invalid))]
    public async Task ValidateAsync_Status_MapsToTransactionStatus(string status, string expected)
    {
        var handler = new StubHandler(Json($$"""{"status":"{{status}}","tran_id":"PAY-1","val_id":"V","amount":"100.00","currency":"BDT"}"""));

        var transaction = await Gateway(handler, Sandbox).ValidateAsync("V", Cancel);

        Assert.Equal(expected, transaction.Value.Status.ToString());
    }

    [Fact]
    public async Task ValidateAsync_OtherCurrencySession_ReportsRequestedCurrencyAndAmount()
    {
        var handler = new StubHandler(Json("""{"status":"VALID","tran_id":"PAY-1","val_id":"V","amount":"11800.00","currency":"BDT","currency_type":"USD","currency_amount":"100.00"}"""));

        var transaction = await Gateway(handler, Sandbox).ValidateAsync("V", Cancel);

        Assert.Equal((100.00m, "USD"), (transaction.Value.Amount, transaction.Value.Currency));
    }

    [Fact]
    public async Task ValidateAsync_RiskFlagged_IsRisky()
    {
        var handler = new StubHandler(Json("""{"status":"VALID","tran_id":"PAY-1","val_id":"V","amount":"100.00","currency":"BDT","risk_level":"1"}"""));

        var transaction = await Gateway(handler, Sandbox).ValidateAsync("V", Cancel);

        Assert.True(transaction.Value.IsRisky);
    }

    [Theory]
    [InlineData("""{"APIConnect":"INVALID_REQUEST"}""")]
    [InlineData("""{"status":"VALID","tran_id":"PAY-1","val_id":"V","currency":"BDT"}""")]
    public async Task ValidateAsync_RequestRefusedOrPaidWithoutAmount_ReturnsRefused(string body)
    {
        var transaction = await Gateway(new StubHandler(Json(body)), Sandbox).ValidateAsync("V", Cancel);

        Assert.Equal("payments.gateway_refused", transaction.Error.Code);
    }

    [Fact]
    public async Task ValidateAsync_NoMerchantAccount_ReturnsNotConfigured()
    {
        var handler = new StubHandler(Json("{}"));

        var transaction = await Gateway(handler, credentials: null).ValidateAsync("V", Cancel);

        Assert.Equal("payments.gateway_not_configured", transaction.Error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task FindAttemptsAsync_TriesFound_QueriesByTransactionIdAndMapsEachTry()
    {
        var handler = new StubHandler(Json("""
            {"APIConnect":"DONE","no_of_trans_found":4,"element":[
             {"val_id":"","status":"FAILED","tran_id":"PAY 1"},
             {"val_id":"","status":"CANCELLED","tran_id":"PAY 1"},
             {"val_id":"","status":"PENDING","tran_id":"PAY 1"},
             {"val_id":"VAL-9","status":"VALIDATED","tran_id":"PAY 1","amount":"1250.50"}]}
            """));

        var attempts = await Gateway(handler, Sandbox).FindAttemptsAsync("PAY 1", Cancel);

        Assert.Equal(
            [
                new GatewayAttempt(GatewayAttemptState.NotPaid, "FAILED", null),
                new GatewayAttempt(GatewayAttemptState.NotPaid, "CANCELLED", null),
                new GatewayAttempt(GatewayAttemptState.InProgress, "PENDING", null),
                new GatewayAttempt(GatewayAttemptState.Paid, "VALIDATED", "VAL-9"),
            ],
            attempts.Value);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Get, "/validator/api/merchantTransIDvalidationAPI.php"), (sent.Method, sent.Uri.AbsolutePath));
        Assert.Equal("?tran_id=PAY%201&store_id=clario0test&store_passwd=s3cret%26pass&format=json", sent.Uri.Query);
    }

    [Theory]
    [InlineData("""{"APIConnect":"DONE","no_of_trans_found":0}""")]
    [InlineData("""{"APIConnect":"DONE","no_of_trans_found":0,"element":[]}""")]
    public async Task FindAttemptsAsync_NoTries_ReturnsNone(string body)
    {
        var attempts = await Gateway(new StubHandler(Json(body)), Sandbox).FindAttemptsAsync("PAY-1", Cancel);

        Assert.Empty(attempts.Value);
    }

    [Fact]
    public async Task FindAttemptsAsync_PaidWithoutValidationId_IsInProgress()
    {
        var handler = new StubHandler(Json("""{"APIConnect":"DONE","element":[{"status":"VALID","tran_id":"PAY-1"}]}"""));

        var attempts = await Gateway(handler, Sandbox).FindAttemptsAsync("PAY-1", Cancel);

        Assert.Equal(GatewayAttemptState.InProgress, Assert.Single(attempts.Value).State);
    }

    [Theory]
    [InlineData("""{"APIConnect":"INVALID_REQUEST"}""")]
    [InlineData("""{"APIConnect":"INACTIVE"}""")]
    [InlineData("""{"element":[]}""")]
    public async Task FindAttemptsAsync_LookupRefused_ReturnsRefused(string body)
    {
        var attempts = await Gateway(new StubHandler(Json(body)), Sandbox).FindAttemptsAsync("PAY-1", Cancel);

        Assert.Equal("payments.gateway_refused", attempts.Error.Code);
    }

    [Fact]
    public async Task FindAttemptsAsync_ServerError_ReturnsUnavailable()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var attempts = await Gateway(handler, Sandbox).FindAttemptsAsync("PAY-1", Cancel);

        Assert.Equal(("payments.gateway_unavailable", ErrorType.Unavailable), (attempts.Error.Code, attempts.Error.Type));
    }

    [Fact]
    public async Task FindAttemptsAsync_NoMerchantAccount_ReturnsNotConfigured()
    {
        var handler = new StubHandler(Json("{}"));

        var attempts = await Gateway(handler, credentials: null).FindAttemptsAsync("PAY-1", Cancel);

        Assert.Equal("payments.gateway_not_configured", attempts.Error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Credentials_ToString_LeavesOutPassword() =>
        Assert.DoesNotContain("s3cret", Sandbox.ToString());

    private static SslCommerzGateway Gateway(StubHandler handler, SslCommerzCredentials? credentials) =>
        new(new HttpClient(handler), new FixedCredentials(credentials), Options.Create(new SslCommerzOptions()), NullLogger<SslCommerzGateway>.Instance);

    private static GatewaySessionRequest Request() => new(
        "PAY-1",
        1250.5m,
        "BDT",
        "CS-000123",
        3,
        new GatewayCustomer("Rahim Uddin", "+8801712345678", "rahim@shop.test", "House 12, Road 5", "Dhaka", "1205"),
        new GatewayCallbacks(new Uri("https://shop.test/success"), new Uri("https://shop.test/fail"), new Uri("https://shop.test/cancel"), new Uri("https://shop.test/ipn")));

    private static Func<HttpRequestMessage, HttpResponseMessage> Json(string body) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Dictionary<string, string> Form(string body) =>
        body.Split('&').Select(pair => pair.Split('=')).ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));

    private sealed class FixedCredentials(SslCommerzCredentials? credentials) : IPaymentGatewayCredentials
    {
        public Task<SslCommerzCredentials?> GetSslCommerzAsync(CancellationToken cancellationToken) => Task.FromResult(credentials);
    }

    private sealed record SentRequest(HttpMethod Method, Uri Uri, string Body);

    /// <summary>Answers every request with <paramref name="respond"/> and records what was sent; no network.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(request.Method, request.RequestUri!, body));
            return respond(request);
        }
    }
}

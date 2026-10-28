using System.Globalization;
using System.Text.Json;
using ClarioShop.Payments.Domain;
using ClarioShop.Settings.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClarioShop.Payments.Gateways.SslCommerz;

/// <summary>
/// SSLCommerz hosted checkout (API v4) with the store's own merchant account from Settings, so the money goes straight
/// to the store. Sandbox or live follows the account.
/// </summary>
internal sealed partial class SslCommerzGateway(
    HttpClient http,
    IPaymentGatewayCredentials credentials,
    IOptions<SslCommerzOptions> options,
    ILogger<SslCommerzGateway> logger) : IPaymentGateway
{
    public const string GatewayName = "SslCommerz";

    public string Name => GatewayName;

    public async Task<Result<GatewaySession>> CreateSessionAsync(GatewaySessionRequest request, CancellationToken cancellationToken)
    {
        if (await credentials.GetSslCommerzAsync(cancellationToken) is not { } account)
        {
            return PaymentsErrors.GatewayNotConfigured(GatewayName);
        }

        using var form = new FormUrlEncodedContent(SessionForm(account, request));
        var response = await SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUrl(account), "gwprocess/v4/api.php")) { Content = form }, cancellationToken);
        if (response.IsFailure)
        {
            return response.Error;
        }

        using var document = response.Value;
        var json = document.RootElement;
        if (Text(json, "status") != "SUCCESS"
            || Text(json, "sessionkey") is not { } sessionKey
            || !Uri.TryCreate(Text(json, "GatewayPageURL"), UriKind.Absolute, out var redirectUrl)
            || redirectUrl.Scheme != Uri.UriSchemeHttps)
        {
            LogSessionRefused(logger, request.TransactionId, Text(json, "failedreason") ?? "no reason given");
            return PaymentsErrors.GatewayRefused(GatewayName);
        }

        return new GatewaySession(sessionKey, redirectUrl);
    }

    public async Task<Result<GatewayTransaction>> ValidateAsync(string validationId, CancellationToken cancellationToken)
    {
        if (await credentials.GetSslCommerzAsync(cancellationToken) is not { } account)
        {
            return PaymentsErrors.GatewayNotConfigured(GatewayName);
        }

        var query = string.Join('&', new Dictionary<string, string>
        {
            ["val_id"] = validationId,
            ["store_id"] = account.StoreId,
            ["store_passwd"] = account.StorePassword,
            ["format"] = "json",
        }.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUrl(account), $"validator/api/validationserverAPI.php?{query}")), cancellationToken);
        if (response.IsFailure)
        {
            return response.Error;
        }

        using var document = response.Value;
        var json = document.RootElement;
        if (Text(json, "status") is not { } status)
        {
            // No transaction status at all: the request itself was refused, e.g. wrong credentials (APIConnect).
            LogValidationRefused(logger, validationId, Text(json, "APIConnect") ?? "no reason given");
            return PaymentsErrors.GatewayRefused(GatewayName);
        }

        var transactionStatus = status switch
        {
            "VALID" => GatewayTransactionStatus.Valid,
            "VALIDATED" => GatewayTransactionStatus.AlreadyValidated,
            _ => GatewayTransactionStatus.Invalid,
        };

        // currency_type and currency_amount are what the session asked for; amount and currency are what settles in BDT.
        var currency = Text(json, "currency_type") ?? Text(json, "currency") ?? string.Empty;
        var amount = Amount(Text(json, "currency_type") is null ? Text(json, "amount") : Text(json, "currency_amount"));
        if (transactionStatus != GatewayTransactionStatus.Invalid && amount is null)
        {
            LogValidationRefused(logger, validationId, "paid without a readable amount");
            return PaymentsErrors.GatewayRefused(GatewayName);
        }

        return new GatewayTransaction(
            Text(json, "tran_id") ?? string.Empty,
            Text(json, "val_id") ?? validationId,
            transactionStatus,
            amount ?? 0m,
            currency,
            Text(json, "bank_tran_id"),
            Text(json, "card_type"),
            Text(json, "risk_level") == "1");
    }

    private static Dictionary<string, string> SessionForm(SslCommerzCredentials account, GatewaySessionRequest request)
    {
        var customer = request.Customer;
        var form = new Dictionary<string, string>
        {
            ["store_id"] = account.StoreId,
            ["store_passwd"] = account.StorePassword,
            ["total_amount"] = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = request.Currency,
            ["tran_id"] = request.TransactionId,
            ["success_url"] = request.Callbacks.Success.AbsoluteUri,
            ["fail_url"] = request.Callbacks.Fail.AbsoluteUri,
            ["cancel_url"] = request.Callbacks.Cancel.AbsoluteUri,
            ["ipn_url"] = request.Callbacks.Ipn.AbsoluteUri,
            ["cus_name"] = customer.Name,
            ["cus_email"] = customer.Email,
            ["cus_phone"] = customer.Phone,
            ["cus_add1"] = customer.AddressLine,
            ["cus_city"] = customer.City,
            ["cus_country"] = "Bangladesh",
            ["shipping_method"] = "Courier",
            ["num_of_item"] = request.ItemCount.ToString(CultureInfo.InvariantCulture),
            ["ship_name"] = customer.Name,
            ["ship_add1"] = customer.AddressLine,
            ["ship_city"] = customer.City,
            ["ship_country"] = "Bangladesh",
            ["product_name"] = $"Order {request.OrderNumber}",
            ["product_category"] = "Retail",
            ["product_profile"] = "physical-goods",
            ["value_a"] = request.OrderNumber,
        };

        if (customer.PostalCode is { } postalCode)
        {
            form["cus_postcode"] = postalCode;
            form["ship_postcode"] = postalCode;
        }

        return form;
    }

    /// <summary>
    /// Sends the request and reads the JSON answer. Anything the HTTP stack throws (connection, timeout, the
    /// resilience pipeline giving up) or an answer that is not JSON means the gateway is unavailable; a cancelled
    /// request still cancels.
    /// </summary>
    private async Task<Result<JsonDocument>> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    LogUnavailable(logger, $"HTTP {(int)response.StatusCode}");
                    return PaymentsErrors.GatewayUnavailable(GatewayName);
                }

                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                if (json.RootElement.ValueKind != JsonValueKind.Object)
                {
                    json.Dispose();
                    LogUnavailable(logger, "the answer is not a JSON object");
                    return PaymentsErrors.GatewayUnavailable(GatewayName);
                }

                return json;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogUnavailable(logger, exception.GetType().Name);
                return PaymentsErrors.GatewayUnavailable(GatewayName);
            }
        }
    }

    private Uri BaseUrl(SslCommerzCredentials account) => account.UseSandbox ? options.Value.SandboxBaseUrl : options.Value.LiveBaseUrl;

    /// <summary>A property as text: SSLCommerz sends most values as strings, some as numbers; empty counts as missing.</summary>
    private static string? Text(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString()!.Trim(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    private static decimal? Amount(string? text) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ? amount : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSLCommerz refused to open a session for payment {TransactionId}: {Reason}")]
    private static partial void LogSessionRefused(ILogger logger, string transactionId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSLCommerz refused to validate transaction {ValidationId}: {Reason}")]
    private static partial void LogValidationRefused(ILogger logger, string validationId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSLCommerz is unavailable: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, string reason);
}

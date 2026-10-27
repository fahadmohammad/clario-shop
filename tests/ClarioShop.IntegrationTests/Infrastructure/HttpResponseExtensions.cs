namespace ClarioShop.IntegrationTests.Infrastructure;

internal static class HttpResponseExtensions
{
    /// <summary>Like EnsureSuccessStatusCode, but puts the ProblemDetails body in the failure message.</summary>
    public static async Task EnsureSuccessWithBodyAsync(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {body}");
        }
    }
}

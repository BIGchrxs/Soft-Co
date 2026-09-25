using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftCo.Services.ExchangeRates;

var passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
string Payload(string baseCode = "ZAR", decimal usd = 0.05m, long? updated = null) => JsonSerializer.Serialize(new {
    result = "success", base_code = baseCode, time_last_update_unix = updated ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    conversion_rates = new Dictionary<string, decimal> { ["ZAR"] = 1, ["USD"] = usd, ["CNY"] = 0.4m, ["EUR"] = 0.04m }
});
ExchangeRateService Service(FakeHandler handler, string key = "test-key") => new(new HttpClient(handler), Options.Create(new ExchangeRateOptions { ApiKey = key }), TimeProvider.System, NullLogger<ExchangeRateService>.Instance);
async Task Error(ExchangeRateService service, string currency, string code) {
    try { await service.GetZarRateAsync(currency, default); throw new Exception("Expected " + code); }
    catch (ExchangeRateException e) { Check(e.Code == code, "Expected safe error " + code); Check(!e.ToString().Contains("test-key"), "Key must not appear in errors"); }
}

var handler = new FakeHandler(() => new(HttpStatusCode.OK) { Content = new StringContent(Payload()) });
using (var service = Service(handler)) {
    var usd = await service.GetZarRateAsync("usd", default);
    Check(usd.Rate == 20m && usd.Currency == "USD" && usd.TargetCurrency == "ZAR", "Invert ZAR-base rate correctly");
    Check((await service.GetZarRateAsync("CNY", default)).Rate == 2.5m, "CNY conversion");
    Check((await service.GetZarRateAsync("EUR", default)).Rate == 25m, "EUR conversion");
    Check(handler.Calls == 1, "Reuse one provider snapshot across currencies");
    Check(handler.LastUri == "https://v6.exchangerate-api.com/v6/test-key/latest/ZAR", "Use fixed provider and ZAR base");
    await Error(service, "../USD", "unsupported_currency");
    Check(handler.Calls == 1, "Reject unsupported input before network");
}
using (var service = Service(new(() => throw new Exception("Network must not be used")), "")) {
    Check((await service.GetZarRateAsync("ZAR", default)).Rate == 1m, "ZAR needs no API key or network");
    await Error(service, "USD", "not_configured");
}
foreach (var bad in new[] { Payload("USD"), Payload(usd: 0), Payload(updated: 1), "not json" }) {
    using var service = Service(new(() => new(HttpStatusCode.OK) { Content = new StringContent(bad) }));
    await Error(service, "USD", "invalid_response");
}
foreach (var (providerCode, expected) in new[] { ("invalid-key", "invalid_key"), ("inactive-account", "inactive_account"), ("quota-reached", "quota_reached") }) {
    var failure = JsonSerializer.Serialize(new Dictionary<string,string> { ["result"] = "error", ["error-type"] = providerCode });
    using var service = Service(new(() => new(HttpStatusCode.Forbidden) { Content = new StringContent(failure) }));
    await Error(service, "USD", expected);
}
using (var service = Service(new(() => throw new HttpRequestException("secret URL test-key")))) await Error(service, "USD", "unavailable");
using (var service = Service(new(() => throw new TaskCanceledException("secret URL test-key")))) await Error(service, "USD", "unavailable");
var concurrent = new FakeHandler(() => new(HttpStatusCode.OK) { Content = new StringContent(Payload()) });
using (var service = Service(concurrent)) {
    var quotes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.GetZarRateAsync("USD", default)));
    Check(quotes.All(q => q.Rate == 20m) && concurrent.Calls == 1, "Concurrent requests share one fetch");
}
Console.WriteLine($"PASS: {passed} exchange-rate checks.");

sealed class FakeHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler {
    public int Calls;
    public string? LastUri;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Calls++; LastUri = request.RequestUri?.ToString(); return Task.FromResult(respond());
    }
}

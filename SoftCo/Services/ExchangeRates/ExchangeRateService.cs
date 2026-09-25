using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SoftCo.Services.ExchangeRates;

// One shared ZAR-base snapshot serves every supported currency, keeping request usage low.
public sealed class ExchangeRateService(HttpClient client, IOptions<ExchangeRateOptions> options, TimeProvider clock, ILogger<ExchangeRateService> logger) : IDisposable
{
    private static readonly HashSet<string> SupportedCurrencies = ["CNY", "USD", "EUR", "ZAR"];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProviderSnapshot? _snapshot;
    private DateTimeOffset _expiresAt;
    private ExchangeRateException? _lastFailure;
    private DateTimeOffset _retryAfter;

    public async Task<ExchangeRateQuote> GetZarRateAsync(string? currency, CancellationToken cancellationToken)
    {
        var code = currency?.Trim().ToUpperInvariant() ?? "";
        if (!SupportedCurrencies.Contains(code))
            throw new ExchangeRateException("unsupported_currency", "Choose CNY, USD, EUR or ZAR.");
        if (code == "ZAR") return new(code, "ZAR", 1m, clock.GetUtcNow(), "ZAR");
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new ExchangeRateException("not_configured", "Exchange-rate lookup is not configured. Enter an agreed rate manually.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is not null && clock.GetUtcNow() < _expiresAt)
                return Quote(_snapshot, code);
            if (_lastFailure is not null && clock.GetUtcNow() < _retryAfter)
                throw _lastFailure;

            try
            {
                var snapshot = await FetchAsync(cancellationToken);
                var quote = Quote(snapshot, code);
                _snapshot = snapshot;
                _expiresAt = clock.GetUtcNow().AddMinutes(Math.Clamp(options.Value.CacheMinutes, 1, 1440));
                // Do not serve a snapshot beyond the same freshness boundary used for validation.
                var freshUntil = quote.AsOfUtc.AddHours(72);
                if (_expiresAt > freshUntil) _expiresAt = freshUntil;
                _lastFailure = null;
                return quote;
            }
            catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException or ExchangeRateException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var failure = error as ExchangeRateException ?? new ExchangeRateException(
                    error is JsonException ? "invalid_response" : "unavailable",
                    "The rate could not be retrieved. Try again shortly or enter an agreed rate manually.");
                _lastFailure = failure;
                _retryAfter = clock.GetUtcNow().AddSeconds(30);
                // Provider URLs contain the key. Never log the URL, body or original exception.
                logger.LogWarning("Exchange-rate lookup failed ({Code}).", failure.Code);
                throw failure;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        var key = Uri.EscapeDataString(options.Value.ApiKey.Trim());
        using var response = await client.GetAsync($"https://v6.exchangerate-api.com/v6/{key}/latest/ZAR", cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new ExchangeRateException("quota_reached", "The exchange-rate request limit was reached. Enter an agreed rate manually.");
        if ((int)response.StatusCode >= 500)
            throw new ExchangeRateException("unavailable", "The exchange-rate provider is unavailable. Try again shortly.");
        var snapshot = await response.Content.ReadFromJsonAsync<ProviderSnapshot>(cancellationToken);
        if (snapshot?.Result == "error")
        {
            throw snapshot.ErrorType switch
            {
                "invalid-key" => new ExchangeRateException("invalid_key", "The exchange-rate key is invalid. Ask your administrator to check it."),
                "inactive-account" => new ExchangeRateException("inactive_account", "The exchange-rate account is inactive. Confirm the provider account email."),
                "quota-reached" => new ExchangeRateException("quota_reached", "The exchange-rate request limit was reached. Enter an agreed rate manually."),
                _ => new ExchangeRateException("unavailable", "The exchange-rate request failed. Try again shortly.")
            };
        }
        if (!response.IsSuccessStatusCode || snapshot is null || snapshot.Result != "success" || snapshot.BaseCode != "ZAR"
            || snapshot.Rates is null || !snapshot.Rates.TryGetValue("ZAR", out var baseRate) || baseRate != 1m
            || snapshot.UpdatedUnix <= 0 || snapshot.UpdatedUnix > clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds()
            || snapshot.UpdatedUnix <= clock.GetUtcNow().AddHours(-72).ToUnixTimeSeconds())
            throw InvalidResponse();
        return snapshot;
    }

    private static ExchangeRateQuote Quote(ProviderSnapshot snapshot, string currency)
    {
        if (snapshot.Rates is null || !snapshot.Rates.TryGetValue(currency, out var unitsPerRand) || unitsPerRand < 0.000001m)
            throw InvalidResponse();
        // Provider says units of foreign currency per Rand; the order stores Rand per unit.
        var zarPerUnit = Math.Round(1m / unitsPerRand, 6, MidpointRounding.AwayFromZero);
        if (zarPerUnit <= 0m || zarPerUnit > 1_000_000m) throw InvalidResponse();
        return new(currency, "ZAR", zarPerUnit, DateTimeOffset.FromUnixTimeSeconds(snapshot.UpdatedUnix), "ExchangeRate-API");
    }

    private static ExchangeRateException InvalidResponse() => new("invalid_response", "The provider returned an invalid or outdated rate. Enter an agreed rate manually.");

    public void Dispose() { _gate.Dispose(); client.Dispose(); }

    private sealed class ProviderSnapshot
    {
        [JsonPropertyName("result")] public string? Result { get; init; }
        [JsonPropertyName("error-type")] public string? ErrorType { get; init; }
        [JsonPropertyName("base_code")] public string? BaseCode { get; init; }
        [JsonPropertyName("time_last_update_unix")] public long UpdatedUnix { get; init; }
        [JsonPropertyName("conversion_rates")] public Dictionary<string, decimal>? Rates { get; init; }
    }
}

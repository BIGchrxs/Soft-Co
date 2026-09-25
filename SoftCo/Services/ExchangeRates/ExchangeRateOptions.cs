namespace SoftCo.Services.ExchangeRates;

public sealed class ExchangeRateOptions
{
    public const string SectionName = "ExchangeRates";
    public string ApiKey { get; set; } = "";
    public int CacheMinutes { get; set; } = 60;
}

public sealed record ExchangeRateQuote(string Currency, string TargetCurrency, decimal Rate, DateTimeOffset AsOfUtc, string Provider);

public sealed class ExchangeRateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

namespace MultiCountryFxImporter.Api;

public sealed class CurrencyRatesImportOptions
{
    public string CurrencyType { get; set; } = "1";
    public string RefCurrencyCode { get; set; } = "HUF";
    public int DefaultDirectCurrencyRateRound { get; set; } = 2;
}

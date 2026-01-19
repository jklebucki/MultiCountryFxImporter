namespace MultiCountryFxImporter.Worker;

public sealed class CurrencyRatesApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string CurrencyCodesEnvironment { get; set; } = "PROD";
    public string ImportEnvironment { get; set; } = "TEST";
}

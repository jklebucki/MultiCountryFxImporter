namespace MultiCountryFxImporter.Worker;

public sealed class CurrencyRatesApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string IfsEnvironment { get; set; } = "TEST";
}

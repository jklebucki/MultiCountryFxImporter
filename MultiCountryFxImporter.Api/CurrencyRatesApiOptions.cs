namespace MultiCountryFxImporter.Api;

public sealed class CurrencyRatesApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string DefaultIfsEnvironment { get; set; } = "TEST";
    public string[] AvailableEnvironments { get; set; } = new[] { "PROD", "TEST", "SZKOL" };
}

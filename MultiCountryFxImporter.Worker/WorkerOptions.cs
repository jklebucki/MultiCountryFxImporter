namespace MultiCountryFxImporter.Worker;

public sealed class WorkerOptions
{
    public string Company { get; set; } = string.Empty;
    public string CurrencyType { get; set; } = "1";
    public string RefCurrencyCode { get; set; } = "HUF";
    public int DefaultDirectCurrencyRateRound { get; set; } = 2;
    public TimeOnly RunAtLocalTime { get; set; } = new(2, 0);
}

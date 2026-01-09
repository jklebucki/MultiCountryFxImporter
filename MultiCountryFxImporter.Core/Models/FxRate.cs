namespace MultiCountryFxImporter.Core.Models;
public record FxRate
{
    public DateTime Date { get; init; }
    public string Bank { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public decimal RateUnit { get; init; }
}

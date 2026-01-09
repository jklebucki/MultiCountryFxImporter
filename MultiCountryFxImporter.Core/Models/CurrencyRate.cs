namespace MultiCountryFxImporter.Core.Models;
public class CurrencyRate
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime Date { get; set; }
}
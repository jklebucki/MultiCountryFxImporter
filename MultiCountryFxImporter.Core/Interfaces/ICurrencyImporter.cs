using MultiCountryFxImporter.Core.Models;

public interface ICurrencyImporter
{
    Task<IEnumerable<FxRate>> ImportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? currencyNames = null);
}

using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Core.Interfaces;

public interface ICurrencyRatesApiClient
{
    Task<IReadOnlyList<CompanyCurrencyDefinition>> GetCompanyCurrenciesAsync(
        string environment,
        string company,
        CancellationToken cancellationToken);

    Task<CurrencyRatesImportResponse> ImportRatesAsync(
        string environment,
        CurrencyRatesImportRequest request,
        CancellationToken cancellationToken);
}

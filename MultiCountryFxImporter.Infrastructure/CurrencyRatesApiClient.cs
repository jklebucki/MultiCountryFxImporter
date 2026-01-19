using System.Net.Http.Json;
using System.Text.Json;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Infrastructure;

public class CurrencyRatesApiClient : ICurrencyRatesApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public CurrencyRatesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CompanyCurrencyDefinition>> GetCompanyCurrenciesAsync(
        string environment,
        string company,
        CancellationToken cancellationToken)
    {
        var requestUri = $"api/CurrencyRates/currency-codes/{environment}?company={Uri.EscapeDataString(company)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("accept", "text/plain");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<CompanyCurrencyDefinition>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<CompanyCurrencyDefinition>>(content, JsonOptions);
            return parsed ?? new List<CompanyCurrencyDefinition>();
        }
        catch (JsonException)
        {
            var codes = content
                .Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(code => new CompanyCurrencyDefinition { CurrencyCode = code })
                .ToList();

            return codes;
        }
    }

    public async Task<CurrencyRatesImportResponse> ImportRatesAsync(
        string environment,
        CurrencyRatesImportRequest request,
        CancellationToken cancellationToken)
    {
        var requestUri = $"api/CurrencyRates/import-single/{environment}";
        using var response = await _httpClient.PostAsJsonAsync(requestUri, request, JsonOptions, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"CurrencyRates API returned {(int)response.StatusCode} with empty body.");
            }
            return new CurrencyRatesImportResponse();
        }

        var parsed = JsonSerializer.Deserialize<CurrencyRatesImportResponse>(content, JsonOptions);
        if (parsed is null)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"CurrencyRates API returned {(int)response.StatusCode}: {content}");
            }
            return new CurrencyRatesImportResponse();
        }

        return parsed;
    }
}

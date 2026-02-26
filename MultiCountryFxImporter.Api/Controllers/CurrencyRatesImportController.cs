using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CurrencyRatesImportController : ControllerBase
{
    private readonly ICurrencyImporterResolver _importerResolver;
    private readonly ICurrencyRatesApiClient _apiClient;
    private readonly CurrencyRatesApiOptions _apiOptions;
    private readonly CurrencyRatesImportOptions _importOptions;
    private readonly ILogger<CurrencyRatesImportController> _logger;

    public CurrencyRatesImportController(
        ICurrencyImporterResolver importerResolver,
        ICurrencyRatesApiClient apiClient,
        IOptions<CurrencyRatesApiOptions> apiOptions,
        IOptions<CurrencyRatesImportOptions> importOptions,
        ILogger<CurrencyRatesImportController> logger)
    {
        _importerResolver = importerResolver;
        _apiClient = apiClient;
        _apiOptions = apiOptions.Value;
        _importOptions = importOptions.Value;
        _logger = logger;
    }

    [HttpPost("current")]
    public async Task<IActionResult> ImportCurrent(
        [FromQuery] string company,
        [FromQuery] string? environment,
        [FromQuery] string? bankModule,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            return BadRequest("company is required");
        }

        return await ImportRatesAsync(company, null, ResolveEnvironment(environment), bankModule, cancellationToken);
    }

    [HttpPost("date")]
    public async Task<IActionResult> ImportForDate(
        [FromQuery] string company,
        [FromQuery] string date,
        [FromQuery] string? environment,
        [FromQuery] string? bankModule,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            return BadRequest("company is required");
        }

        if (string.IsNullOrWhiteSpace(date) ||
            !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return BadRequest("date must be in yyyy-MM-dd format");
        }

        return await ImportRatesAsync(company, parsedDate, ResolveEnvironment(environment), bankModule, cancellationToken);
    }

    private async Task<IActionResult> ImportRatesAsync(
        string company,
        DateTime? date,
        string environment,
        string? bankModule,
        CancellationToken cancellationToken)
    {
        IBankCurrencyImporter importer;
        try
        {
            importer = _importerResolver.Resolve(bankModule);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }

        IReadOnlyList<CompanyCurrencyDefinition> companyCurrencies;
        try
        {
            companyCurrencies = await _apiClient.GetCompanyCurrenciesAsync(
                environment,
                company,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load company currencies for {Company}.", company);
            return StatusCode(StatusCodes.Status502BadGateway, "Failed to load company currencies.");
        }

        var companyCurrencyMap = companyCurrencies
            .Where(item => !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (companyCurrencyMap.Count == 0)
        {
            return NotFound("Company currency list is empty.");
        }

        var currencyNames = string.Join(",", companyCurrencyMap.Keys);

        IEnumerable<FxRate> rates;
        try
        {
            rates = date.HasValue
                ? await importer.ImportAsync(date, date, currencyNames)
                : await importer.ImportAsync(null, null, currencyNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch rates from {BankModule}.", importer.ModuleDefinition.Code);
            return StatusCode(StatusCodes.Status502BadGateway, $"Failed to fetch rates from {importer.ModuleDefinition.Code}.");
        }

        var filteredRates = rates
            .Where(rate => companyCurrencyMap.ContainsKey(rate.Currency))
            .ToList();

        if (date.HasValue)
        {
            filteredRates = filteredRates
                .Where(rate => rate.Date.Date == date.Value.Date)
                .ToList();
        }

        if (filteredRates.Count == 0)
        {
            return NotFound("No matching rates found.");
        }

        var refCurrencyCode = !string.IsNullOrWhiteSpace(importer.ModuleDefinition.DefaultRefCurrencyCode)
            ? importer.ModuleDefinition.DefaultRefCurrencyCode
            : _importOptions.RefCurrencyCode;

        var results = new List<CurrencyRatesImportResult>();
        foreach (var group in filteredRates.GroupBy(rate => rate.Date))
        {
            var request = new CurrencyRatesImportRequest
            {
                Company = company,
                CurrencyType = _importOptions.CurrencyType,
                ValidFrom = group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Rates = group.Select(rate =>
                {
                    var definition = companyCurrencyMap[rate.Currency];
                    var round = definition.DecimalsInRate ?? _importOptions.DefaultDirectCurrencyRateRound;
                    var convFactor = definition.ConvFactor != 0 ? definition.ConvFactor : rate.RateUnit;

                    return new CurrencyRatesImportRate
                    {
                        CurrencyCode = rate.Currency,
                        CurrencyRate = rate.Rate,
                        ConvFactor = convFactor,
                        RefCurrencyCode = refCurrencyCode,
                        DirectCurrencyRate = rate.Rate,
                        DirectCurrencyRateRound = round,
                        CTableNo = importer.ModuleDefinition.Code
                    };
                }).ToList()
            };

            try
            {
                var response = await _apiClient.ImportRatesAsync(
                    environment,
                    request,
                    cancellationToken);

                LogImportResponse(response, request);
                results.Add(new CurrencyRatesImportResult(request.ValidFrom, response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import failed for {Company} on {Date}.", company, request.ValidFrom);
                return StatusCode(StatusCodes.Status502BadGateway, $"Import failed for {request.ValidFrom}.");
            }
        }

        return Ok(new CurrencyRatesImportBatchResponse(company, results));
    }

    private string ResolveEnvironment(string? environment)
    {
        var selected = string.IsNullOrWhiteSpace(environment)
            ? _apiOptions.DefaultIfsEnvironment
            : environment.Trim();

        if (_apiOptions.AvailableEnvironments.Length == 0)
        {
            return selected;
        }

        var match = _apiOptions.AvailableEnvironments
            .FirstOrDefault(value => string.Equals(value, selected, StringComparison.OrdinalIgnoreCase));

        return match ?? _apiOptions.DefaultIfsEnvironment;
    }

    private void LogImportResponse(CurrencyRatesImportResponse response, CurrencyRatesImportRequest request)
    {
        _logger.LogInformation(
            "Import response for {Company} {Date}: Total={Total} Success={Success} Failed={Failed}.",
            request.Company,
            request.ValidFrom,
            response.TotalRates,
            response.SuccessfulRates,
            response.FailedRates);

        if (response.Messages.Count > 0)
        {
            _logger.LogInformation("Import messages: {Messages}.", string.Join(" | ", response.Messages));
        }

        foreach (var error in response.Errors)
        {
            _logger.LogWarning("Import error for {Currency}: {Message}", error.CurrencyCode, error.Message);
        }
    }
}

public sealed record CurrencyRatesImportResult(string Date, CurrencyRatesImportResponse Response);

public sealed record CurrencyRatesImportBatchResponse(string Company, IReadOnlyList<CurrencyRatesImportResult> Results);

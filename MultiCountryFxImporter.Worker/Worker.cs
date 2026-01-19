using System.Globalization;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _workerOptions;
    private readonly CurrencyRatesApiOptions _apiOptions;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> workerOptions,
        IOptions<CurrencyRatesApiOptions> apiOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _workerOptions = workerOptions.Value;
        _apiOptions = apiOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation("Next run scheduled in {Delay}.", delay);
                await Task.Delay(delay, stoppingToken);
            }

            using var scope = _serviceProvider.CreateScope();
            var importer = scope.ServiceProvider.GetRequiredService<ICurrencyImporter>();
            var apiClient = scope.ServiceProvider.GetRequiredService<ICurrencyRatesApiClient>();

            IReadOnlyList<CompanyCurrencyDefinition> companyCurrencies;
            try
            {
                companyCurrencies = await apiClient.GetCompanyCurrenciesAsync(
                    _apiOptions.CurrencyCodesEnvironment,
                    _workerOptions.Company,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load company currencies for {Company}.", _workerOptions.Company);
                continue;
            }

            var companyCurrencyMap = companyCurrencies
                .Where(item => !string.IsNullOrWhiteSpace(item.CurrencyCode))
                .GroupBy(item => item.CurrencyCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            if (companyCurrencyMap.Count == 0)
            {
                _logger.LogWarning("Company currency list is empty for {Company}.", _workerOptions.Company);
                continue;
            }

            IEnumerable<FxRate> rates;
            try
            {
                rates = await importer.ImportAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch rates from source.");
                continue;
            }

            var filteredRates = rates
                .Where(rate => companyCurrencyMap.ContainsKey(rate.Currency))
                .ToList();

            if (filteredRates.Count == 0)
            {
                _logger.LogWarning("No matching rates found for company {Company}.", _workerOptions.Company);
                continue;
            }

            var missingCurrencies = companyCurrencyMap.Keys
                .Except(filteredRates.Select(rate => rate.Currency), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingCurrencies.Count > 0)
            {
                _logger.LogInformation(
                    "Missing rates for {Count} currencies: {Currencies}.",
                    missingCurrencies.Count,
                    string.Join(", ", missingCurrencies));
            }

            foreach (var group in filteredRates.GroupBy(rate => rate.Date))
            {
                var request = new CurrencyRatesImportRequest
                {
                    Company = _workerOptions.Company,
                    CurrencyType = _workerOptions.CurrencyType,
                    ValidFrom = group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Rates = group.Select(rate =>
                    {
                        var definition = companyCurrencyMap[rate.Currency];
                        var round = definition.DecimalsInRate ?? _workerOptions.DefaultDirectCurrencyRateRound;
                        var convFactor = definition.ConvFactor != 0 ? definition.ConvFactor : rate.RateUnit;

                        return new CurrencyRatesImportRate
                        {
                            CurrencyCode = rate.Currency,
                            CurrencyRate = rate.Rate,
                            ConvFactor = convFactor,
                            RefCurrencyCode = _workerOptions.RefCurrencyCode,
                            DirectCurrencyRate = rate.Rate,
                            DirectCurrencyRateRound = round,
                            CTableNo = string.Empty
                        };
                    }).ToList()
                };

                try
                {
                    var response = await apiClient.ImportRatesAsync(
                        _apiOptions.ImportEnvironment,
                        request,
                        stoppingToken);

                    LogImportResponse(response, request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Import failed for {Company} on {Date}.",
                        _workerOptions.Company,
                        request.ValidFrom);
                }
            }
        }
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var now = DateTimeOffset.Now;
        var target = now.Date.Add(_workerOptions.RunAtLocalTime.ToTimeSpan());
        if (target <= now)
        {
            target = target.AddDays(1);
        }

        return target - now;
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

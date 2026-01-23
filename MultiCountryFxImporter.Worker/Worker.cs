using System.Globalization;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;
using MultiCountryFxImporter.Worker.Services;

namespace MultiCountryFxImporter.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _workerOptions;
    private readonly CurrencyRatesApiOptions _apiOptions;
    private readonly IOptionsMonitor<WorkerScheduleOptions> _scheduleMonitor;
    private readonly WorkerRunStateStore _stateStore;
    private readonly Dictionary<string, DateOnly> _lastRunDates = new(StringComparer.OrdinalIgnoreCase);

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> workerOptions,
        IOptions<CurrencyRatesApiOptions> apiOptions,
        IOptionsMonitor<WorkerScheduleOptions> scheduleMonitor,
        WorkerRunStateStore stateStore)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _workerOptions = workerOptions.Value;
        _apiOptions = apiOptions.Value;
        _scheduleMonitor = scheduleMonitor;
        _stateStore = stateStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(30);
        var persistedState = await _stateStore.ReadAsync(stoppingToken);
        foreach (var entry in persistedState)
        {
            _lastRunDates[entry.Key] = entry.Value;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var schedule = _scheduleMonitor.CurrentValue?.Environments ?? new List<WorkerScheduleEntry>();
            var configuredKeys = new HashSet<string>(
                schedule.Select(BuildKey).Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.OrdinalIgnoreCase);

            var removed = false;
            foreach (var key in _lastRunDates.Keys.Where(key => !configuredKeys.Contains(key)).ToList())
            {
                _lastRunDates.Remove(key);
                removed = true;
            }
            if (removed)
            {
                await _stateStore.WriteAsync(_lastRunDates, stoppingToken);
            }

            if (schedule.Count == 0)
            {
                await Task.Delay(pollInterval, stoppingToken);
                continue;
            }

            var now = DateTimeOffset.Now;
            foreach (var entry in schedule)
            {
                if (string.IsNullOrWhiteSpace(entry.Environment))
                {
                    _logger.LogWarning("Worker schedule entry missing environment.");
                    continue;
                }

                if (!TimeOnly.TryParse(entry.RunAtLocalTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var runTime))
                {
                    _logger.LogWarning(
                        "Invalid RunAtLocalTime '{RunAtLocalTime}' for environment {Environment}.",
                        entry.RunAtLocalTime,
                        entry.Environment);
                    continue;
                }

                var scheduledToday = now.Date.Add(runTime.ToTimeSpan());
                if (now < scheduledToday)
                {
                    continue;
                }

                var today = DateOnly.FromDateTime(now.Date);
                var key = BuildKey(entry);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (_lastRunDates.TryGetValue(key, out var lastRun) && lastRun == today)
                {
                    continue;
                }

                await ProcessEnvironmentAsync(entry, stoppingToken);
                _lastRunDates[key] = today;
                await _stateStore.WriteAsync(_lastRunDates, stoppingToken);
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }

    private string BuildKey(WorkerScheduleEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Environment))
        {
            return string.Empty;
        }

        var company = !string.IsNullOrWhiteSpace(entry.Company) ? entry.Company : _workerOptions.Company;
        if (string.IsNullOrWhiteSpace(company))
        {
            return string.Empty;
        }

        return BuildKey(entry.Environment, company);
    }

    private static string BuildKey(string environment, string company)
        => $"{environment.Trim().ToUpperInvariant()}|{company.Trim().ToUpperInvariant()}";

    private async Task ProcessEnvironmentAsync(WorkerScheduleEntry entry, CancellationToken stoppingToken)
    {
        var environment = entry.Environment;
        var company = !string.IsNullOrWhiteSpace(entry.Company) ? entry.Company : _workerOptions.Company;
        if (string.IsNullOrWhiteSpace(company))
        {
            _logger.LogWarning("Company is missing for environment {Environment}.", environment);
            return;
        }

        _logger.LogInformation("Starting import for {Company} in {Environment}.", company, environment);

        using var scope = _serviceProvider.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<ICurrencyImporter>();
        var apiClient = scope.ServiceProvider.GetRequiredService<ICurrencyRatesApiClient>();

        IReadOnlyList<CompanyCurrencyDefinition> companyCurrencies;
        try
        {
            companyCurrencies = await apiClient.GetCompanyCurrenciesAsync(
                environment,
                company,
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load company currencies for {Company} ({Environment}).", company, environment);
            return;
        }

        var companyCurrencyMap = companyCurrencies
            .Where(item => !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (companyCurrencyMap.Count == 0)
        {
            _logger.LogWarning("Company currency list is empty for {Company} ({Environment}).", company, environment);
            return;
        }

        var currencyNames = string.Join(",", companyCurrencyMap.Keys);

        IEnumerable<FxRate> rates;
        try
        {
            rates = await importer.ImportAsync(null, null, currencyNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch rates from source for {Environment}.", environment);
            return;
        }

        var filteredRates = rates
            .Where(rate => companyCurrencyMap.ContainsKey(rate.Currency))
            .ToList();

        if (filteredRates.Count == 0)
        {
            _logger.LogWarning("No matching rates found for company {Company} ({Environment}).", company, environment);
            return;
        }

        var missingCurrencies = companyCurrencyMap.Keys
            .Except(filteredRates.Select(rate => rate.Currency), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingCurrencies.Count > 0)
        {
            _logger.LogInformation(
                "Missing rates for {Count} currencies ({Environment}): {Currencies}.",
                missingCurrencies.Count,
                environment,
                string.Join(", ", missingCurrencies));
        }

        foreach (var group in filteredRates.GroupBy(rate => rate.Date))
        {
            var request = new CurrencyRatesImportRequest
            {
                Company = company,
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
                        CTableNo = "MNB"
                    };
                }).ToList()
            };

            try
            {
                var response = await apiClient.ImportRatesAsync(
                    environment,
                    request,
                    stoppingToken);

                LogImportResponse(response, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Import failed for {Company} on {Date} ({Environment}).",
                    company,
                    request.ValidFrom,
                    environment);
            }
        }

        _logger.LogInformation("Finished import for {Company} in {Environment}.", company, environment);
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

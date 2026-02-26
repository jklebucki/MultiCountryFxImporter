using System.Globalization;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Infrastructure;

public sealed class EcbImporter : IBankCurrencyImporter
{
    private const string SeriesPrefix = "D";
    private const string BaseCurrency = "EUR";
    private const string ExrType = "SP00";
    private const string ExrSuffix = "A";
    private readonly HttpClient _httpClient;

    public EcbImporter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public BankModuleDefinition ModuleDefinition { get; } = new()
    {
        Code = BankModuleCatalog.EcbCode,
        DisplayName = "European Central Bank",
        DefaultRefCurrencyCode = BaseCurrency
    };

    public async Task<IEnumerable<FxRate>> ImportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? currencyNames = null)
    {
        var requestUri = BuildRequestUri(startDate, endDate, currencyNames);
        using var response = await _httpClient.GetAsync(requestUri);
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        WriteCsvSnapshot(content);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<FxRate>();
        }

        return ParseRates(content);
    }

    private static string BuildRequestUri(DateTime? startDate, DateTime? endDate, string? currencyNames)
    {
        var resolvedStart = startDate;
        var resolvedEnd = endDate;
        if (resolvedStart.HasValue || resolvedEnd.HasValue)
        {
            if (!resolvedStart.HasValue)
            {
                resolvedStart = resolvedEnd;
            }

            if (!resolvedEnd.HasValue)
            {
                resolvedEnd = resolvedStart;
            }
        }

        var currencyDimension = BuildCurrencyDimension(currencyNames);
        var path = string.IsNullOrWhiteSpace(currencyDimension)
            ? $"service/data/EXR/{SeriesPrefix}..{BaseCurrency}.{ExrType}.{ExrSuffix}"
            : $"service/data/EXR/{SeriesPrefix}.{currencyDimension}.{BaseCurrency}.{ExrType}.{ExrSuffix}";

        var queryParts = new List<string>
        {
            "format=csvdata"
        };

        if (resolvedStart.HasValue && resolvedEnd.HasValue)
        {
            queryParts.Add($"startPeriod={resolvedStart.Value:yyyy-MM-dd}");
            queryParts.Add($"endPeriod={resolvedEnd.Value:yyyy-MM-dd}");
        }
        else
        {
            queryParts.Add("lastNObservations=1");
        }

        return $"{path}?{string.Join("&", queryParts)}";
    }

    private static string BuildCurrencyDimension(string? currencyNames)
    {
        if (string.IsNullOrWhiteSpace(currencyNames))
        {
            return string.Empty;
        }

        var currencies = currencyNames
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currencies.Count == 0)
        {
            return string.Empty;
        }

        return string.Join('+', currencies);
    }

    private static IReadOnlyList<FxRate> ParseRates(string csvContent)
    {
        var rows = csvContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (rows.Count == 0)
        {
            return Array.Empty<FxRate>();
        }

        var header = ParseCsvRow(rows[0]);
        var timePeriodIndex = header.FindIndex(column => string.Equals(column, "TIME_PERIOD", StringComparison.OrdinalIgnoreCase));
        var currencyIndex = header.FindIndex(column => string.Equals(column, "CURRENCY", StringComparison.OrdinalIgnoreCase));
        var obsValueIndex = header.FindIndex(column => string.Equals(column, "OBS_VALUE", StringComparison.OrdinalIgnoreCase));

        if (timePeriodIndex < 0 || currencyIndex < 0 || obsValueIndex < 0)
        {
            throw new FormatException("ECB response does not contain expected CSV columns.");
        }

        var rates = new List<FxRate>();
        foreach (var row in rows.Skip(1))
        {
            var columns = ParseCsvRow(row);
            if (timePeriodIndex >= columns.Count || currencyIndex >= columns.Count || obsValueIndex >= columns.Count)
            {
                continue;
            }

            var dateRaw = columns[timePeriodIndex];
            var currencyRaw = columns[currencyIndex];
            var valueRaw = columns[obsValueIndex];

            if (string.IsNullOrWhiteSpace(dateRaw) ||
                string.IsNullOrWhiteSpace(currencyRaw) ||
                string.IsNullOrWhiteSpace(valueRaw))
            {
                continue;
            }

            if (!DateTime.TryParseExact(dateRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!decimal.TryParse(valueRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
            {
                continue;
            }

            rates.Add(new FxRate
            {
                Date = date,
                Bank = BankModuleCatalog.EcbCode,
                Currency = currencyRaw.Trim().ToUpperInvariant(),
                RateUnit = 1m,
                Rate = rate
            });
        }

        return rates;
    }

    private static List<string> ParseCsvRow(string row)
    {
        var result = new List<string>();
        var buffer = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < row.Length; i++)
        {
            var current = row[i];
            if (current == '"')
            {
                if (inQuotes && i + 1 < row.Length && row[i + 1] == '"')
                {
                    buffer.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (current == ',' && !inQuotes)
            {
                result.Add(buffer.ToString());
                buffer.Clear();
                continue;
            }

            buffer.Append(current);
        }

        result.Add(buffer.ToString());
        return result;
    }

    private static void WriteCsvSnapshot(string content)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);

        var fileName = $"ecb-response-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var filePath = Path.Combine(logsDirectory, fileName);
        File.WriteAllText(filePath, content);
    }
}

using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FxRateController : ControllerBase
{
    private readonly ICurrencyImporter _importer;

    public FxRateController(ICurrencyImporter importer)
    {
        _importer = importer;
    }

    [HttpGet("csv")]
    public async Task<IActionResult> GetCsv()
    {
        var rates = await _importer.ImportAsync();

        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture);
        config.Delimiter = ";";
        config.HasHeaderRecord = false;
        using var csv = new CsvWriter(writer, config);
        csv.Context.RegisterClassMap<FxRateMap>();
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "yyyy-MM-dd" };
        csv.WriteRecords(rates);
        writer.Flush();
        memoryStream.Position = 0;

        return File(memoryStream.ToArray(), "text/csv", "rates.csv");
    }

    [HttpGet("csv/date")]
    public async Task<IActionResult> GetCsvForDate([FromQuery] string date, [FromQuery] string? currencies)
    {
        if (string.IsNullOrWhiteSpace(date) ||
            !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return BadRequest("date must be in yyyy-MM-dd format");
        }

        var rates = await _importer.ImportAsync(parsedDate, parsedDate, currencies);
        if (!string.IsNullOrWhiteSpace(currencies))
        {
            var allowed = currencies
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            rates = rates.Where(rate => allowed.Contains(rate.Currency));
        }

        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture);
        config.Delimiter = ";";
        config.HasHeaderRecord = false;
        using var csv = new CsvWriter(writer, config);
        csv.Context.RegisterClassMap<FxRateMap>();
        csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "yyyy-MM-dd" };
        csv.WriteRecords(rates);
        writer.Flush();
        memoryStream.Position = 0;

        return File(memoryStream.ToArray(), "text/csv", $"rates-{parsedDate:yyyyMMdd}.csv");
    }
}

public class FxRateMap : ClassMap<FxRate>
{
    public FxRateMap()
    {
        Map(m => m.Currency).Index(0);
        Map(m => m.Rate).Index(1);
        Map(m => m.Date).Index(2);
        Map(m => m.Bank).Ignore();
        Map(m => m.RateUnit).Ignore();
    }
}

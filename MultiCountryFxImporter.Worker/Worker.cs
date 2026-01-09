using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Worker;

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



public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var importer = scope.ServiceProvider.GetRequiredService<ICurrencyImporter>();
            var rates = await importer.ImportAsync();

            using var writer = new StreamWriter($"rates_{DateTime.UtcNow:yyyyMMdd}.csv");
            var config = new Configuration();
            config.CultureInfo = CultureInfo.InvariantCulture;
            config.Delimiter = ";";
            config.HasHeaderRecord = false;
            using var csv = new CsvWriter(writer, config);
            csv.Configuration.RegisterClassMap<FxRateMap>();
            csv.Configuration.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "yyyy-MM-dd" };
            csv.WriteRecords(rates);
            writer.Close();

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

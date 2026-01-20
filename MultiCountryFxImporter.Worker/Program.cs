using MultiCountryFxImporter.MnbClient;
using MultiCountryFxImporter.Worker;
using MultiCountryFxImporter.Infrastructure;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var scheduleConfig = builder.Configuration.GetSection("WorkerScheduleConfig").Get<WorkerScheduleConfigOptions>()
    ?? new WorkerScheduleConfigOptions();
var schedulePath = Path.IsPathRooted(scheduleConfig.Path)
    ? scheduleConfig.Path
    : Path.Combine(builder.Environment.ContentRootPath, scheduleConfig.Path);
builder.Configuration.AddJsonFile(schedulePath, optional: true, reloadOnChange: true);

builder.Services.AddSerilog((_, configuration) =>
{
    configuration
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Month)
        .WriteTo.Console();
});

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<CurrencyRatesApiOptions>(builder.Configuration.GetSection("CurrencyRatesApi"));
builder.Services.Configure<WorkerScheduleOptions>(builder.Configuration.GetSection("WorkerSchedule"));

var apiOptions = builder.Configuration.GetSection("CurrencyRatesApi").Get<CurrencyRatesApiOptions>() ?? new CurrencyRatesApiOptions();
builder.Services.AddHttpClient<ICurrencyRatesApiClient, CurrencyRatesApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    }
});

builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<ICurrencyImporter, MnbImporter>();
builder.Services.AddScoped<MNBArfolyamServiceSoapClient>();

var host = builder.Build();
host.Run();

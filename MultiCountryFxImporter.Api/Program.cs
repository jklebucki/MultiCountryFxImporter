using MultiCountryFxImporter.Infrastructure;
using MultiCountryFxImporter.MnbClient;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Api;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.UseInlineDefinitionsForEnums();
});
builder.Services.AddScoped<ICurrencyImporter, MnbImporter>();
builder.Services.AddScoped<MNBArfolyamServiceSoapClient>();
builder.Services.Configure<CurrencyRatesApiOptions>(builder.Configuration.GetSection("CurrencyRatesApi"));
builder.Services.Configure<CurrencyRatesImportOptions>(builder.Configuration.GetSection("CurrencyRatesImport"));
builder.Services.Configure<WorkerScheduleConfigOptions>(builder.Configuration.GetSection("WorkerScheduleConfig"));
builder.Services.AddSingleton<MultiCountryFxImporter.Api.Services.WorkerScheduleStore>();

var apiOptions = builder.Configuration.GetSection("CurrencyRatesApi").Get<CurrencyRatesApiOptions>() ?? new CurrencyRatesApiOptions();
builder.Services.AddHttpClient<ICurrencyRatesApiClient, CurrencyRatesApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

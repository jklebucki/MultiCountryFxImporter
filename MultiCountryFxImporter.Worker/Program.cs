using MultiCountryFxImporter.MnbClient;
using MultiCountryFxImporter.Worker;
using MultiCountryFxImporter.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<ICurrencyImporter, MnbImporter>();
builder.Services.AddScoped<MNBArfolyamServiceSoapClient>();

var host = builder.Build();
host.Run();

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using MultiCountryFxImporter.Api;
using MultiCountryFxImporter.Api.Data;
using MultiCountryFxImporter.Api.Models;
using MultiCountryFxImporter.Api.Services;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Infrastructure;
using MultiCountryFxImporter.MnbClient;

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
builder.Services.AddScoped<MnbImporter>();
builder.Services.AddScoped<MNBArfolyamServiceSoapClient>();
builder.Services.Configure<CurrencyRatesApiOptions>(builder.Configuration.GetSection("CurrencyRatesApi"));
builder.Services.Configure<CurrencyRatesImportOptions>(builder.Configuration.GetSection("CurrencyRatesImport"));
builder.Services.Configure<WorkerScheduleConfigOptions>(builder.Configuration.GetSection("WorkerScheduleConfig"));
builder.Services.Configure<LogViewerOptions>(builder.Configuration.GetSection("LogViewer"));
builder.Services.Configure<EcbApiOptions>(builder.Configuration.GetSection("EcbApi"));
builder.Services.AddSingleton<MultiCountryFxImporter.Api.Services.WorkerScheduleStore>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

var scheduleConfig = builder.Configuration.GetSection("WorkerScheduleConfig").Get<WorkerScheduleConfigOptions>()
    ?? new WorkerScheduleConfigOptions();
var schedulePath = scheduleConfig.Path ?? "worker-schedule.json";
var scheduleFullPath = Path.IsPathRooted(schedulePath)
    ? schedulePath
    : Path.Combine(builder.Environment.ContentRootPath, schedulePath);
scheduleFullPath = Path.GetFullPath(scheduleFullPath);
var scheduleDirectory = Path.GetDirectoryName(scheduleFullPath) ?? builder.Environment.ContentRootPath;
var authDbPath = Path.Combine(scheduleDirectory, "auth.db");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite($"Data Source={authDbPath}");
});

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(2);
});

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

var apiOptions = builder.Configuration.GetSection("CurrencyRatesApi").Get<CurrencyRatesApiOptions>() ?? new CurrencyRatesApiOptions();
var ecbOptions = builder.Configuration.GetSection("EcbApi").Get<EcbApiOptions>() ?? new EcbApiOptions();
builder.Services.AddHttpClient<ICurrencyRatesApiClient, CurrencyRatesApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    }
});
builder.Services.AddHttpClient<EcbImporter>(client =>
{
    if (!string.IsNullOrWhiteSpace(ecbOptions.BaseUrl))
    {
        client.BaseAddress = new Uri(ecbOptions.BaseUrl);
    }
});
builder.Services.AddScoped<IBankCurrencyImporter>(serviceProvider => serviceProvider.GetRequiredService<MnbImporter>());
builder.Services.AddScoped<IBankCurrencyImporter>(serviceProvider => serviceProvider.GetRequiredService<EcbImporter>());
builder.Services.AddScoped<ICurrencyImporterResolver, CurrencyImporterResolver>();
builder.Services.AddScoped<ICurrencyImporter>(serviceProvider => serviceProvider.GetRequiredService<ICurrencyImporterResolver>().Resolve(null));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var mustReset = context.User.HasClaim(claim => claim.Type == "pwd_reset_required" && claim.Value == "true");
        if (mustReset)
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/account/change-password") ||
                path.StartsWithSegments("/account/logout") ||
                path.StartsWithSegments("/account/access-denied"))
            {
                await next();
                return;
            }

            if (path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Password reset required.");
                return;
            }

            context.Response.Redirect("/account/change-password");
            return;
        }
    }

    await next();
});
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    await IdentitySeed.EnsureMigratedAsync(scope.ServiceProvider);
    await IdentitySeed.EnsureRolesAndAdminAsync(scope.ServiceProvider);
}

app.Run();

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Api.Services;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Api.Controllers;

[ApiController]
[Route("api/worker-schedule")]
[Authorize(Roles = "Admin,PowerAdmin")]
public class WorkerScheduleController : ControllerBase
{
    private readonly WorkerScheduleStore _store;
    private readonly CurrencyRatesApiOptions _apiOptions;
    private readonly ICurrencyImporterResolver _importerResolver;

    public WorkerScheduleController(
        WorkerScheduleStore store,
        IOptions<CurrencyRatesApiOptions> apiOptions,
        ICurrencyImporterResolver importerResolver)
    {
        _store = store;
        _apiOptions = apiOptions.Value;
        _importerResolver = importerResolver;
    }

    [HttpGet]
    public async Task<ActionResult<WorkerScheduleFile>> Get(CancellationToken cancellationToken)
    {
        var data = await _store.ReadAsync(cancellationToken);
        foreach (var entry in data.WorkerSchedule?.Environments ?? new List<WorkerScheduleEntry>())
        {
            if (string.IsNullOrWhiteSpace(entry.BankModule))
            {
                entry.BankModule = _importerResolver.DefaultModuleCode;
            }
            else
            {
                entry.BankModule = entry.BankModule.Trim().ToUpperInvariant();
            }
        }

        return Ok(data);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] WorkerScheduleFile scheduleFile, CancellationToken cancellationToken)
    {
        var errors = Validate(scheduleFile);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        await _store.WriteAsync(scheduleFile, cancellationToken);
        return Ok(scheduleFile);
    }

    private List<string> Validate(WorkerScheduleFile scheduleFile)
    {
        var errors = new List<string>();
        var entries = scheduleFile.WorkerSchedule?.Environments ?? new List<WorkerScheduleEntry>();
        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var supportedBankModules = _importerResolver.GetAvailableModules()
            .Select(module => module.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Environment))
            {
                errors.Add("Environment is required.");
                continue;
            }

            if (_apiOptions.AvailableEnvironments.Length > 0 &&
                !_apiOptions.AvailableEnvironments.Any(env => string.Equals(env, entry.Environment, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Environment '{entry.Environment}' is not supported.");
            }

            if (string.IsNullOrWhiteSpace(entry.Company))
            {
                errors.Add($"Company is required for environment '{entry.Environment}'.");
            }

            if (!TimeOnly.TryParse(entry.RunAtLocalTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                errors.Add($"RunAtLocalTime '{entry.RunAtLocalTime}' is invalid for environment '{entry.Environment}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.BankModule))
            {
                entry.BankModule = _importerResolver.DefaultModuleCode;
            }
            else
            {
                entry.BankModule = entry.BankModule.Trim().ToUpperInvariant();
            }

            if (supportedBankModules.Count > 0 && !supportedBankModules.Contains(entry.BankModule))
            {
                errors.Add($"Bank module '{entry.BankModule}' is not supported for environment '{entry.Environment}'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Company))
            {
                continue;
            }

            var duplicateKey = string.Join(
                "|",
                entry.Environment.Trim().ToUpperInvariant(),
                entry.Company.Trim().ToUpperInvariant(),
                entry.BankModule.Trim().ToUpperInvariant());

            if (!seenEntries.Add(duplicateKey))
            {
                errors.Add(
                    $"Environment '{entry.Environment}', company '{entry.Company}', bank module '{entry.BankModule}' is duplicated.");
            }
        }

        return errors;
    }
}

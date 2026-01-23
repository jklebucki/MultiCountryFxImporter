using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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

    public WorkerScheduleController(WorkerScheduleStore store, IOptions<CurrencyRatesApiOptions> apiOptions)
    {
        _store = store;
        _apiOptions = apiOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<WorkerScheduleFile>> Get(CancellationToken cancellationToken)
    {
        var data = await _store.ReadAsync(cancellationToken);
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Environment))
            {
                errors.Add("Environment is required.");
                continue;
            }

            if (!seen.Add(entry.Environment))
            {
                errors.Add($"Environment '{entry.Environment}' is duplicated.");
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
        }

        return errors;
    }
}

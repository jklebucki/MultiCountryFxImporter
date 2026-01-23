using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Worker.Models;

namespace MultiCountryFxImporter.Worker.Services;

public class WorkerRunStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;

    public WorkerRunStateStore(IOptions<WorkerScheduleConfigOptions> options, IHostEnvironment environment)
    {
        var configuredPath = options.Value.Path ?? "worker-schedule.json";
        var schedulePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
        schedulePath = Path.GetFullPath(schedulePath);
        var directory = Path.GetDirectoryName(schedulePath) ?? environment.ContentRootPath;
        _path = Path.Combine(directory, "worker-run-state.json");
    }

    public async Task<Dictionary<string, DateOnly>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        }

        var content = await File.ReadAllTextAsync(_path, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<WorkerRunStateFile>(content, JsonOptions);
            if (parsed?.Entries is null)
            {
                return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in parsed.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Environment) || string.IsNullOrWhiteSpace(entry.Company))
                {
                    continue;
                }

                if (!DateOnly.TryParseExact(entry.LastRunDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }

                var key = BuildKey(entry.Environment, entry.Company);
                result[key] = date;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task WriteAsync(IReadOnlyDictionary<string, DateOnly> state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entries = state
            .Select(kvp => ParseKey(kvp.Key, kvp.Value))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToList();

        var payload = new WorkerRunStateFile { Entries = entries };
        var content = JsonSerializer.Serialize(payload, JsonOptions);
        var tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, _path, true);
    }

    private static string BuildKey(string environment, string company)
        => $"{environment.Trim().ToUpperInvariant()}|{company.Trim().ToUpperInvariant()}";

    private static WorkerRunStateEntry? ParseKey(string key, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var parts = key.Split('|', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        return new WorkerRunStateEntry
        {
            Environment = parts[0],
            Company = parts[1],
            LastRunDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
    }
}

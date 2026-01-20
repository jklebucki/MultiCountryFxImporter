using System.Text.Json;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Api.Services;

public class WorkerScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;

    public WorkerScheduleStore(IOptions<WorkerScheduleConfigOptions> options, IWebHostEnvironment environment)
    {
        var configuredPath = options.Value.Path ?? "worker-schedule.json";
        _path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public async Task<WorkerScheduleFile> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new WorkerScheduleFile();
        }

        var content = await File.ReadAllTextAsync(_path, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new WorkerScheduleFile();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<WorkerScheduleFile>(content, JsonOptions);
            return parsed ?? new WorkerScheduleFile();
        }
        catch (JsonException)
        {
            return new WorkerScheduleFile();
        }
    }

    public async Task WriteAsync(WorkerScheduleFile scheduleFile, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = JsonSerializer.Serialize(scheduleFile, JsonOptions);
        var tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, _path, true);
    }
}

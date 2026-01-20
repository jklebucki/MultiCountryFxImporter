namespace MultiCountryFxImporter.Core.Models;

public sealed class WorkerScheduleOptions
{
    public List<WorkerScheduleEntry> Environments { get; set; } = new();
}

public sealed class WorkerScheduleEntry
{
    public string Environment { get; set; } = "TEST";
    public string Company { get; set; } = "KFT";
    public string RunAtLocalTime { get; set; } = "02:00:00";
}

public sealed class WorkerScheduleFile
{
    public WorkerScheduleOptions WorkerSchedule { get; set; } = new();
}

namespace MultiCountryFxImporter.Worker.Models;

public sealed class WorkerRunStateFile
{
    public List<WorkerRunStateEntry> Entries { get; set; } = new();
}

public sealed class WorkerRunStateEntry
{
    public string Environment { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string LastRunDate { get; set; } = string.Empty;
}

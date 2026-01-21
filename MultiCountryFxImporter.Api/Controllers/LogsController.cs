using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MultiCountryFxImporter.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly LogViewerOptions _options;
    private readonly string _rootPath;

    public LogsController(IOptions<LogViewerOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _rootPath = Path.IsPathRooted(_options.Path)
            ? _options.Path
            : Path.Combine(environment.ContentRootPath, _options.Path);
    }

    [HttpGet]
    public IActionResult List()
    {
        if (!Directory.Exists(_rootPath))
        {
            return Ok(Array.Empty<LogFileDto>());
        }

        var pattern = string.IsNullOrWhiteSpace(_options.FilePattern) ? "*.log" : _options.FilePattern;
        var files = new DirectoryInfo(_rootPath)
            .GetFiles(pattern)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new LogFileDto(
                file.Name,
                file.LastWriteTime,
                file.Length));

        return Ok(files);
    }

    [HttpGet("{fileName}")]
    public IActionResult Read(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Missing file name.");
        }

        var safeName = Path.GetFileName(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, safeName));
        var rootFullPath = Path.GetFullPath(_rootPath);
        if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid file name.");
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, "text/plain");
    }

    public sealed record LogFileDto(string Name, DateTime LastWriteTime, long SizeBytes);
}

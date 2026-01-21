using Microsoft.AspNetCore.Mvc;

namespace MultiCountryFxImporter.Api.Controllers;

public class LogsUiController : Controller
{
    [HttpGet("/logs")]
    public IActionResult Index()
    {
        return View("~/Views/LogViewer/Index.cshtml");
    }
}

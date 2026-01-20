using Microsoft.AspNetCore.Mvc;

namespace MultiCountryFxImporter.Api.Controllers;

public class WorkerScheduleUiController : Controller
{
    [HttpGet("/worker-schedule")]
    public IActionResult Index()
    {
        return View("~/Views/WorkerSchedule/Index.cshtml");
    }
}

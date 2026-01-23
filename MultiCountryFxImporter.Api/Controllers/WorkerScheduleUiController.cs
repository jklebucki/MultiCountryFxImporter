using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MultiCountryFxImporter.Api.Controllers;

public class WorkerScheduleUiController : Controller
{
    [HttpGet("/worker-schedule")]
    [Authorize(Roles = "Admin,PowerAdmin")]
    public IActionResult Index()
    {
        return View("~/Views/WorkerSchedule/Index.cshtml");
    }
}

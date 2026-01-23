using Microsoft.AspNetCore.Mvc;

namespace MultiCountryFxImporter.Api.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("/about")]
    public IActionResult About()
    {
        return View();
    }
}

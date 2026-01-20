using Microsoft.AspNetCore.Mvc;

namespace MultiCountryFxImporter.Api.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return View();
    }
}

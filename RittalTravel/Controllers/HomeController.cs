using Microsoft.AspNetCore.Mvc;

namespace RittalTravel.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Dashboard");
    }

    public IActionResult Error() => View();
}

using Microsoft.AspNetCore.Mvc;

namespace RittalTravel.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return RedirectToAction("Login", "Account");
    }

    public IActionResult Error() => View();
}

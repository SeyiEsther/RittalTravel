using Microsoft.AspNetCore.Mvc;

namespace RittalTravel.Controllers;

public class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login() => RedirectToAction("Index", "Dashboard");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout() => RedirectToAction("Index", "Dashboard");

    [HttpGet]
    public IActionResult AccessDenied() => RedirectToAction("Index", "Dashboard");
}

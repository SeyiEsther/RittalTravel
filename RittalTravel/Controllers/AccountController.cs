using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace RittalTravel.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<IdentityUser> signIn, ILogger<AccountController> logger)
    {
        _signIn = signIn;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "Email and password are required.");
            return View();
        }
        var result = await _signIn.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in", email);
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Dashboard");
        }
        _logger.LogWarning("Failed login attempt for {Email}", email);
        ModelState.AddModelError("", "Invalid email or password.");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        _logger.LogInformation("User logged out");
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}

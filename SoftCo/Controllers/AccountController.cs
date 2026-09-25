using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SoftCo.Models;
using SoftCo.ViewModels;

namespace SoftCo.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
        => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // lockoutOnFailure: the 5-attempt / 15-minute lockout configured in Program.cs only
        // engages if failures are actually counted here.
        var result = await _signIn.PasswordSignInAsync(vm.Email, vm.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _users.FindByEmailAsync(vm.Email);
            if (user is { IsActive: false })
            {
                await _signIn.SignOutAsync();
                ModelState.AddModelError(string.Empty, "This account has been deactivated.");
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Orders");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Try again in 15 minutes.");
            return View(vm);
        }

        // Deliberately not "no such user" vs "wrong password" - that difference tells an
        // attacker which addresses are real.
        ModelState.AddModelError(string.Empty, "Incorrect email address or password.");
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Denied() => View();
}

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using MultiCountryFxImporter.Api.Models.Account;

namespace MultiCountryFxImporter.Api.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IEmailSender _emailSender;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }

    [HttpGet("/account/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Account/Login.cshtml");
    }

    [HttpPost("/account/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View("~/Views/Account/Login.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Login);
        if (user is null)
        {
            user = await _userManager.FindByNameAsync(model.Login);
        }

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View("~/Views/Account/Login.cshtml", model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View("~/Views/Account/Login.cshtml", model);
    }

    [HttpGet("/account/register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View("~/Views/Account/Register.cshtml");
    }

    [HttpPost("/account/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Account/Register.cshtml", model);
        }

        var user = new IdentityUser
        {
            UserName = model.UserName,
            Email = model.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("~/Views/Account/Register.cshtml", model);
        }

        await _userManager.AddToRoleAsync(user, "User");
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("/account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/account/forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View("~/Views/Account/ForgotPassword.cshtml");
    }

    [HttpPost("/account/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Account/ForgotPassword.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ViewData["Message"] = "If the email is registered, a reset link has been sent.";
            return View("~/Views/Account/ForgotPassword.cshtml");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var callbackUrl = Url.Action(
            "ResetPassword",
            "Account",
            new { email = model.Email, token },
            protocol: Request.Scheme) ?? string.Empty;

        var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl);
        var body = $"<p>Click the link to reset your password:</p><p><a href=\"{encodedUrl}\">Reset Password</a></p>";

        await _emailSender.SendEmailAsync(model.Email, "Reset your password", body);

        ViewData["Message"] = "If the email is registered, a reset link has been sent.";
        return View("~/Views/Account/ForgotPassword.cshtml");
    }

    [HttpGet("/account/reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email, string token)
    {
        var model = new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        };

        return View("~/Views/Account/ResetPassword.cshtml", model);
    }

    [HttpPost("/account/reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Account/ResetPassword.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Unable to reset password.");
            return View("~/Views/Account/ResetPassword.cshtml", model);
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("~/Views/Account/ResetPassword.cshtml", model);
        }

        return RedirectToAction("Login", "Account");
    }

    [HttpGet("/account/access-denied")]
    public IActionResult AccessDenied()
    {
        return View("~/Views/Account/AccessDenied.cshtml");
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiCountryFxImporter.Api.Models.Admin;

namespace MultiCountryFxImporter.Api.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private const string PasswordResetRequiredClaim = "pwd_reset_required";
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet("/admin/users")]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .OrderBy(user => user.UserName)
            .ToListAsync();

        var availableRoles = await _roleManager.Roles
            .Select(role => role.Name ?? string.Empty)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .OrderBy(role => role)
            .ToListAsync();

        var models = new List<AdminUserSummary>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var requireReset = claims.Any(claim => claim.Type == PasswordResetRequiredClaim && claim.Value == "true");

            models.Add(new AdminUserSummary
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                Roles = roles.OrderBy(role => role).ToList(),
                RequirePasswordReset = requireReset
            });
        }

        var viewModel = new AdminUsersViewModel
        {
            Users = models,
            AvailableRoles = availableRoles,
            StatusMessage = TempData["StatusMessage"] as string,
            StatusType = TempData["StatusType"] as string
        };

        return View("~/Views/Admin/Users.cshtml", viewModel);
    }

    [HttpPost("/admin/users/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string userName, string email, string password, string[] roles, bool requireReset)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("User name, email, and password are required.", "error");
            return RedirectToAction(nameof(Index));
        }

        var user = new IdentityUser
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            SetStatus(string.Join(", ", result.Errors.Select(error => error.Description)), "error");
            return RedirectToAction(nameof(Index));
        }

        await UpdateUserRolesAsync(user, roles);
        await SetPasswordResetRequiredAsync(user, requireReset);

        SetStatus("User created.", "success");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/users/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, string newPassword, bool requireReset)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            SetStatus("User not found.", "error");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            SetStatus("New password is required.", "error");
            return RedirectToAction(nameof(Index));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            SetStatus(string.Join(", ", result.Errors.Select(error => error.Description)), "error");
            return RedirectToAction(nameof(Index));
        }

        await SetPasswordResetRequiredAsync(user, requireReset);
        SetStatus("Password reset completed.", "success");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/users/require-reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequireReset(string userId, bool requireReset)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            SetStatus("User not found.", "error");
            return RedirectToAction(nameof(Index));
        }

        await SetPasswordResetRequiredAsync(user, requireReset);
        SetStatus("Password reset requirement updated.", "success");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/users/update-roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoles(string userId, string[] roles)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            SetStatus("User not found.", "error");
            return RedirectToAction(nameof(Index));
        }

        await UpdateUserRolesAsync(user, roles);
        SetStatus("Roles updated.", "success");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/users/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            SetStatus("User not found.", "error");
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == user.Id)
        {
            SetStatus("You cannot delete your own account.", "error");
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            SetStatus(string.Join(", ", result.Errors.Select(error => error.Description)), "error");
            return RedirectToAction(nameof(Index));
        }

        SetStatus("User deleted.", "success");
        return RedirectToAction(nameof(Index));
    }

    private async Task UpdateUserRolesAsync(IdentityUser user, IEnumerable<string> roles)
    {
        var validRoles = await _roleManager.Roles
            .Select(role => role.Name ?? string.Empty)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToListAsync();

        var desiredRoles = roles?.Where(role => validRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? new List<string>();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Where(role => !desiredRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
        var toAdd = desiredRoles.Where(role => !currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();

        if (toRemove.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, toRemove);
        }
        if (toAdd.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, toAdd);
        }
    }

    private async Task SetPasswordResetRequiredAsync(IdentityUser user, bool requireReset)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        var existing = claims.FirstOrDefault(claim => claim.Type == PasswordResetRequiredClaim);

        if (requireReset)
        {
            if (existing is null)
            {
                await _userManager.AddClaimAsync(user, new Claim(PasswordResetRequiredClaim, "true"));
            }
            else if (existing.Value != "true")
            {
                await _userManager.ReplaceClaimAsync(user, existing, new Claim(PasswordResetRequiredClaim, "true"));
            }
        }
        else if (existing is not null)
        {
            await _userManager.RemoveClaimAsync(user, existing);
        }
    }

    private void SetStatus(string message, string type)
    {
        TempData["StatusMessage"] = message;
        TempData["StatusType"] = type;
    }
}

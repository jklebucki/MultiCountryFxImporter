using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MultiCountryFxImporter.Api.Data;

public static class IdentitySeed
{
    public const string AdminEmail = "it@citronex.pl";
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Citro@123!";

    public static readonly string[] Roles = { "User", "PowerAdmin", "Admin" };

    public static async Task EnsureMigratedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public static async Task EnsureRolesAndAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminUser = await userManager.FindByNameAsync(AdminUserName)
            ?? await userManager.FindByEmailAsync(AdminEmail);
        if (adminUser is null)
        {
            adminUser = new IdentityUser
            {
                UserName = AdminUserName,
                Email = AdminEmail,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(adminUser, AdminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to create admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

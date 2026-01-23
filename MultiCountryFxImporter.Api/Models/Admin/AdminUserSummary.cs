namespace MultiCountryFxImporter.Api.Models.Admin;

public class AdminUserSummary
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool RequirePasswordReset { get; set; }
}

namespace MultiCountryFxImporter.Api.Models.Admin;

public class AdminUsersViewModel
{
    public List<AdminUserSummary> Users { get; set; } = new();
    public List<string> AvailableRoles { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? StatusType { get; set; }
}

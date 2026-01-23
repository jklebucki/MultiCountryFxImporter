using System.ComponentModel.DataAnnotations;

namespace MultiCountryFxImporter.Api.Models.Account;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace MultiCountryFxImporter.Api.Models.Account;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username or email")]
    public string Login { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

namespace MultiCountryFxImporter.Api.Models;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "MultiCountryFxImporter";
    public string? Username { get; set; }
    public string? Password { get; set; }
}

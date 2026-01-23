using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Api.Models;

namespace MultiCountryFxImporter.Api.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        var fromAddress = string.IsNullOrWhiteSpace(_options.FromAddress)
            ? _options.Username ?? string.Empty
            : _options.FromAddress;
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("SMTP FromAddress or Username must be configured.");
        }

        var from = new MailAddress(fromAddress, _options.FromName);
        var to = new MailAddress(email);

        using var message = new MailMessage(from, to)
        {
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password ?? string.Empty);
        }

        await client.SendMailAsync(message);
    }
}

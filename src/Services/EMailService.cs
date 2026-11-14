using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ASP_FINAL_PROJECT;

public class EmailService
{
    public async Task SendAsync(string email, string subject, string text)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            "Workshop",
            "noreply@workshop.local"));

        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("plain")
        {
            Text = text
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            "localhost",
            1025,
            SecureSocketOptions.None);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
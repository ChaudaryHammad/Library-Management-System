using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
namespace Book_Management_System.Services
{
    public class EmailService
    {
 


        private readonly string _smtpServer; 
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;


        public EmailService(IConfiguration configuration)
        {
            _smtpServer = configuration["AppEnv:SMTP_SERVER"];
            _smtpPort = int.Parse(configuration["AppEnv:SMTP_PORT"]);
            _smtpUser = configuration["AppEnv:SMTP_USERNAME"];
            _smtpPassword = configuration["AppEnv:SMTP_PASSWORD"];

        }

        public void SendEmail(string toEmail, string subject, string body)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Library", _smtpUser));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = body };
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.Connect(_smtpServer, _smtpPort, false);
                client.Authenticate(_smtpUser, _smtpPassword);
                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}

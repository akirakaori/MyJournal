using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MyJournal.Services
{
    /// <summary>
    /// Sends emails via Brevo SMTP server
    /// </summary>
    public class EmailService
    {
        private const string SMTP_HOST = "smtp-relay.brevo.com";
        private const int SMTP_PORT = 587;
        
        // Environment variable keys
        private const string SMTP_USERNAME_ENV = "BREVO_SMTP_USERNAME";
        private const string SMTP_PASSWORD_ENV = "BREVO_SMTP_PASSWORD";

        /// <summary>
        /// Sends OTP email to the specified address
        /// </summary>
        public async Task<(bool success, string errorMessage)> SendOTPEmailAsync(string recipientEmail, string otp)
        {
            try
            {
                // Try to get SMTP credentials from various environment variables for convenience
                var smtpUsername = Environment.GetEnvironmentVariable("BREVO_SMTP_USERNAME") 
                                  ?? Environment.GetEnvironmentVariable("GMAIL_SMTP_USERNAME");
                
                var smtpPassword = Environment.GetEnvironmentVariable("BREVO_SMTP_PASSWORD") 
                                  ?? Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");

                if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    return (false, "SMTP credentials not configured. Please set environment variables (BREVO_SMTP_USERNAME/PASSWORD or GMAIL_SMTP_USERNAME/PASSWORD).");
                }

                // Create email message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("MyJournal App", smtpUsername));
                message.To.Add(new MailboxAddress("", recipientEmail));
                message.Subject = "Your Password Reset OTP";

                // Create HTML email body
                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #512BD4; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
        .otp-box {{ background-color: white; border: 2px solid #512BD4; padding: 20px; text-align: center; font-size: 32px; font-weight: bold; letter-spacing: 8px; margin: 20px 0; border-radius: 8px; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Password Reset Request</h1>
        </div>
        <div class='content'>
            <p>Hello,</p>
            <p>You have requested to reset your PIN for MyJournal app. Please use the following One-Time Password (OTP) to complete the reset process:</p>
            
            <div class='otp-box'>{otp}</div>
            
            <p><strong>This OTP is valid for 10 minutes.</strong></p>
            
            <p>If you did not request this password reset, please ignore this email. Your account remains secure.</p>
            
            <p>Best regards,<br>MyJournal Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply.</p>
        </div>
    </div>
</body>
</html>"
                };

                message.Body = bodyBuilder.ToMessageBody();

                // Send email via SMTP
                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(SMTP_HOST, SMTP_PORT, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(smtpUsername, smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to send email: {ex.Message}");
            }
        }
    }
}

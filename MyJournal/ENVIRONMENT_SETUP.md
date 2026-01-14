# Environment Variables Configuration Guide

To enable email functionality for password reset, you need to set up environment variables for your SMTP credentials (Brevo or Gmail).

## Required Environment Variables

The app is currently configured to prefer **Brevo** but fallback to **Gmail** if configured.

### Using Brevo (Recommended for Production)
```bash
BREVO_SMTP_USERNAME=your_brevo_login_email
BREVO_SMTP_PASSWORD=your_brevo_smtp_key
```

### Using Gmail (Alternative)
```bash
GMAIL_SMTP_USERNAME=your_gmail_address@gmail.com
GMAIL_APP_PASSWORD=your_16_character_app_password
```

## How to Set Variables (Windows)

1. Press `Win + X` -> **System** -> **Advanced system settings**.
2. Click **Environment Variables**.
3. Under **User variables**, click **New**.
4. Add `BREVO_SMTP_USERNAME` and its value.
5. Add `BREVO_SMTP_PASSWORD` and its value.
6. **Restart Visual Studio** or your Terminal to apply changes.

---

## Technical Implementation
The app retrieves these at runtime using:
`Environment.GetEnvironmentVariable("BREVO_SMTP_USERNAME")`

This approach ensures that **no sensitive credentials are stored in the source code or committed to GitHub**.

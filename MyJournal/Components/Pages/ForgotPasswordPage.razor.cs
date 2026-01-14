using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;

namespace MyJournal.Components.Pages;

public partial class ForgotPasswordPage
{
    private MudForm? _form;

    private string _email = string.Empty;
    private bool _isBusy;
    private bool _emailSent;
    private string _errorMessage = string.Empty;

    private async Task HandleSendOTP()
    {
        _errorMessage = string.Empty;
        _isBusy = true;

        await _form!.Validate();

        if (!_form.IsValid)
        {
            _isBusy = false;
            return;
        }

        // Validate email matches stored email
        var isValidEmail = await AuthService.ValidateEmailAsync(_email);
        if (!isValidEmail)
        {
            _errorMessage = "Email address not found. Please check and try again.";
            _isBusy = false;
            return;
        }

        // Generate OTP
        var otp = await OTPService.GenerateOTPAsync();
        if (string.IsNullOrEmpty(otp))
        {
            _errorMessage = "Failed to generate reset code. Please try again.";
            _isBusy = false;
            return;
        }

        // Send OTP via email
        var (success, errorMessage) = await EmailService.SendOTPEmailAsync(_email, otp);
        if (!success)
        {
            _errorMessage = errorMessage;
            _isBusy = false;
            return;
        }

        _emailSent = true;
        _isBusy = false;
    }
}

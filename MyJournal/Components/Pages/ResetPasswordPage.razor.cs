using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;

namespace MyJournal.Components.Pages;

public partial class ResetPasswordPage
{
    private MudForm? _form;

    private string _otp = string.Empty;
    private string _newPin = string.Empty;
    private string _confirmPin = string.Empty;

    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    // PIN visibility toggles
    private bool _showPin;
    private InputType _pinInputType => _showPin ? InputType.Text : InputType.Password;
    private string _pinVisibilityIcon => _showPin ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;

    private bool _showConfirmPin;
    private InputType _confirmPinInputType => _showConfirmPin ? InputType.Text : InputType.Password;
    private string _confirmPinVisibilityIcon => _showConfirmPin ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;

    private void TogglePinVisibility() => _showPin = !_showPin;
    private void ToggleConfirmPinVisibility() => _showConfirmPin = !_showConfirmPin;

    private string? ValidatePin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return "New PIN is required";

        if (pin.Length != 4)
            return "PIN must be exactly 4 digits";

        if (!pin.All(char.IsDigit))
            return "PIN must contain only numbers";

        return null;
    }

    private string? ValidateConfirmPin(string confirmPin)
    {
        if (string.IsNullOrWhiteSpace(confirmPin))
            return "Please confirm your PIN";

        if (confirmPin != _newPin)
            return "PINs do not match";

        return null;
    }

    private async Task HandleResetPassword()
    {
        _errorMessage = string.Empty;
        _successMessage = string.Empty;
        _isBusy = true;

        await _form!.Validate();

        if (!_form.IsValid)
        {
            _isBusy = false;
            return;
        }

        // Validate OTP
        var (isValid, errorMessage) = await OTPService.ValidateOTPAsync(_otp);
        if (!isValid)
        {
            _errorMessage = errorMessage;
            _isBusy = false;
            return;
        }

        // Reset PIN
        var success = await AuthService.ResetPinAsync(_newPin);
        if (!success)
        {
            _errorMessage = "Failed to reset PIN. Please try again.";
            _isBusy = false;
            return;
        }

        // Clear OTP
        await OTPService.ClearOTPAsync();

        _successMessage = "PIN reset successful! Redirecting to login...";
        _isBusy = false;

        // Redirect to login after 2 seconds
        await Task.Delay(2000);
        NavigationManager.NavigateTo("/login", replace: true);
    }

    private async Task HandleResendOTP()
    {
        _errorMessage = string.Empty;
        _successMessage = string.Empty;
        _isBusy = true;

        // Get user email
        var email = await AuthService.GetEmailAsync();
        if (string.IsNullOrEmpty(email))
        {
            _errorMessage = "Could not find your email. Please start the password reset process again.";
            _isBusy = false;
            return;
        }

        // Generate new OTP (overwrites old one)
        var otp = await OTPService.GenerateOTPAsync();
        if (string.IsNullOrEmpty(otp))
        {
            _errorMessage = "Failed to generate new code. Please try again.";
            _isBusy = false;
            return;
        }

        // Send new OTP via email
        var (success, sendErrorMessage) = await EmailService.SendOTPEmailAsync(email, otp);
        if (!success)
        {
            _errorMessage = sendErrorMessage;
            _isBusy = false;
            return;
        }

        _successMessage = "New code sent! Check your email.";
        _isBusy = false;
    }
}
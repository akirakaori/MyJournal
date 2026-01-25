using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;
using System.Text.RegularExpressions;

namespace MyJournal.Components.Pages;

public partial class FirstTimeSetupPage
{
    private MudForm? _form;

    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _pin = string.Empty;
    private string _confirmPin = string.Empty;

    private bool _isBusy;
    private string _errorMessage = string.Empty;
    
    // PIN visibility toggles
    private bool _pinVisible = false;
    private InputType _pinInputType => _pinVisible ? InputType.Text : InputType.Password;
    private string _pinVisibilityIcon => _pinVisible ? Icons.Material.Filled.VisibilityOff : Icons.Material.Filled.Visibility;
    
    private bool _confirmPinVisible = false;
    private InputType _confirmPinInputType => _confirmPinVisible ? InputType.Text : InputType.Password;
    private string _confirmPinVisibilityIcon => _confirmPinVisible ? Icons.Material.Filled.VisibilityOff : Icons.Material.Filled.Visibility;

    protected override async Task OnInitializedAsync()
    {
        // Redirect to login if user already exists (single-user app)
        var userExists = await AuthService.UserExistsAsync();
        if (userExists)
        {
            NavManager.NavigateTo("/login", replace: true);
        }
    }

    private void TogglePinVisibility()
    {
        _pinVisible = !_pinVisible;
    }

    private void ToggleConfirmPinVisibility()
    {
        _confirmPinVisible = !_confirmPinVisible;
    }

    private string? ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "Email is required";

        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        return emailRegex.IsMatch(email) ? null : "Invalid email format";
    }

    private string? ValidatePin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return "PIN is required";

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

        if (confirmPin != _pin)
            return "PINs do not match";

        return null;
    }

    private async Task HandleSetup()
    {
        _errorMessage = string.Empty;
        _isBusy = true;

        await _form!.Validate();

        if (!_form.IsValid)
        {
            _isBusy = false;
            return;
        }

        var success = await AuthService.RegisterUserAsync(_username, _email, _pin);

        if (success)
        {
            NavManager.NavigateTo("/login", replace: true);
        }
        else
        {
            _errorMessage = "Failed to create account. Please try again.";
        }

        _isBusy = false;
    }
}

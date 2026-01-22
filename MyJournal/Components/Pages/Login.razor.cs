using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;

namespace MyJournal.Components.Pages;

public partial class Login
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    private MudForm? _form;

    private string _username = string.Empty;
    private string _pin = string.Empty;

    private bool _isBusy;
    private bool _invalidCredentials;

    protected override async Task OnInitializedAsync()
    {
        // Check if user exists, if not redirect to first-time setup
        var userExists = await AuthService.UserExistsAsync();
        if (!userExists)
        {
            NavManager.NavigateTo("/first-time-setup", replace: true);
        }
    }

    private async Task HandleLogin()
    {
        _invalidCredentials = false;
        _isBusy = true;

        await _form!.Validate();

        if (!_form.IsValid)
        {
            _isBusy = false;
            return;
        }

        var isValid = await AuthService.ValidateCredentialsAsync(_username, _pin);

        if (isValid)
        {
            AppState.Login(_username);
            NavManager.NavigateTo("/", replace: true);
        }
        else
        {
            _invalidCredentials = true;
        }

        _isBusy = false;
    }
}
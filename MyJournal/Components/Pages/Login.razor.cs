using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;

namespace MyJournal.Components.Pages;

public partial class Login
{
    private MudForm? _form;

    private string _username = string.Empty;
    private string _password = string.Empty;

    private bool _isBusy;
    private bool _invalidCredentials;

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

        if (_username == "1" && _password == "1")
        {
            AppState.Login();
            NavManager.NavigateTo("/dashboard", replace: true);
        }
        else
        {
            _invalidCredentials = true;
        }

        _isBusy = false;
    }
}
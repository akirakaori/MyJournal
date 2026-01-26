using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;
using System;

namespace MyJournal.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;

    private bool? _userExists;

    protected override async Task OnInitializedAsync()
    {
        // Check whether user account already exists
        _userExists = await AuthService.UserExistsAsync();

        // Listen to login/logout changes
        AppState.OnChange += HandleAppStateChanged;
    }

    private void HandleAppStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void GoLogin()
    {
        NavManager.NavigateTo("/login");
    }

    private void GoRegister()
    {
        NavManager.NavigateTo("/first-time-setup");
    }

    // Keeping your original navigation methods (even if not used on this page)
    private void GoToday() => NavManager.NavigateTo("/journalentry");
    private void GoCalendar() => NavManager.NavigateTo("/calendar");
    private void GoDashboard() => NavManager.NavigateTo("/dashboard");
    private void GoExport() => NavManager.NavigateTo("/export");

    public void Dispose()
    {
        AppState.OnChange -= HandleAppStateChanged;
    }
}
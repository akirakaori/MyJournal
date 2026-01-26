using Microsoft.AspNetCore.Components;
using MyJournal.Services;
using System;

namespace MyJournal.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    protected override void OnInitialized()
    {
        //  Fixes UI not refreshing when login state changes
        AppState.OnChange += HandleAppStateChanged;
    }

    private void HandleAppStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void GoLogin() => NavManager.NavigateTo("/login");
    private void GoToday() => NavManager.NavigateTo("/journalentry");
    private void GoCalendar() => NavManager.NavigateTo("/calendar");
    private void GoDashboard() => NavManager.NavigateTo("/dashboard");
    private void GoExport() => NavManager.NavigateTo("/export");

    public void Dispose()
    {
        AppState.OnChange -= HandleAppStateChanged;
    }
}

using Microsoft.AspNetCore.Components;
using MudBlazor;
using MyJournal.Services;
using JournalMaui.Services;
using System;

namespace MyJournal.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ProfileService ProfileService { get; set; } = default!;

    [Inject] private JournalDatabases JournalDb { get; set; } = default!;
    [Inject] private StreakService StreakService { get; set; } = default!;

    // UI data
    private JournalMaui.Models.StreakResult? streakData;
    private List<JournalMaui.Models.JournalEntries> RecentEntries = new();

    private bool? _userExists;
    private string? WelcomeName;

    protected override async Task OnInitializedAsync()
    {
        // Check whether user account already exists
        _userExists = await AuthService.UserExistsAsync();

        // Listen to login/logout changes
        AppState.OnChange += HandleAppStateChanged;
        if (AppState.IsLoggedIn)
        {
            await LoadWelcomeNameAsync();
            await LoadDashboardDataAsync();
        }
    }

    private void HandleAppStateChanged()
    {
        _ = InvokeAsync(async () =>
        {
            if (AppState.IsLoggedIn)
            {
                await LoadWelcomeNameAsync();
                await LoadDashboardDataAsync();
            }
            else
                WelcomeName = null;

            StateHasChanged();
        });
    }

    private async Task LoadWelcomeNameAsync()
    {
        try
        {
            var name = await ProfileService.GetDisplayNameAsync();
            WelcomeName = string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            WelcomeName = null;
        }
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            streakData = await StreakService.CalculateStreaksAsync();

            var recent = await JournalDb.GetRecentAsync(50);
            if (recent != null)
            {
                RecentEntries = recent.OrderByDescending(r => r.DateKey).Take(7).ToList();
            }
            else
            {
                RecentEntries = new();
            }
        }
        catch
        {
            streakData = null;
            RecentEntries = new();
        }
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
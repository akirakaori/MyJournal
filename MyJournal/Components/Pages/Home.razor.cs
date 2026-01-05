using Microsoft.AspNetCore.Components;
using MyJournal.Services;

namespace MyJournal.Components.Pages;

public partial class Home
{
    private void GoLogin() => NavManager.NavigateTo("/login");
    private void GoToday() => NavManager.NavigateTo("/entry/today");
    private void GoCalendar() => NavManager.NavigateTo("/calendar");
    private void GoTimeline() => NavManager.NavigateTo("/timeline");
    private void GoSearch() => NavManager.NavigateTo("/search");
    private void GoDashboard() => NavManager.NavigateTo("/dashboard");
    private void GoExport() => NavManager.NavigateTo("/export");
}
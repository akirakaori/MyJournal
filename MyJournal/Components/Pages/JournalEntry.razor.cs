using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JournalMaui.Services;
using JournalMaui.Models;

namespace MyJournal.Components.Pages;

public partial class JournalEntry : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private DateTime SelectedDate = DateTime.Today;
    private string Content = "";
    private bool IsBusy = false;
    private string Status = "";

    private DateTime? CreatedAt;
    private DateTime? UpdatedAt;

    // NEW: title state
    private string CurrentTitle = "";
    private bool ShowTitleModal = false;
    private string TitleInput = "";

    private string CreatedAtText => CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    private string UpdatedAtText => UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    protected override async Task OnInitializedAsync()
    {
        await Load();
    }

    private async Task Load()
    {
        IsBusy = true;
        Status = "";
        try
        {
            // loads latest entry for that date (date-only)
            var entry = await Db.GetByDateAsync(SelectedDate);
            if (entry is null)
            {
                Content = "";
                CurrentTitle = "";
                CreatedAt = null;
                UpdatedAt = null;
                Status = "No entry for this date. Start writing!";
            }
            else
            {
                Content = entry.Content ?? "";
                CurrentTitle = entry.Title ?? "";
                CreatedAt = entry.CreatedAt;
                UpdatedAt = entry.UpdatedAt;
                Status = "Loaded.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // NEW: open title prompt
    private Task StartSave()
    {
        Status = "";
        TitleInput = string.IsNullOrWhiteSpace(CurrentTitle) ? "" : CurrentTitle;
        ShowTitleModal = true;
        return Task.CompletedTask;
    }

    private void CloseTitleModal()
    {
        ShowTitleModal = false;
    }

    private async Task ConfirmSave()
    {
        var title = (TitleInput ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            Status = "Please enter a title.";
            return;
        }

        ShowTitleModal = false;
        await SaveWithTitle(title);
    }

    private async Task SaveWithTitle(string title)
    {
        IsBusy = true;
        Status = "";
        try
        {
            await Db.SaveAsync(SelectedDate, title, Content);

            // reload by date only
            var entry = await Db.GetByDateAsync(SelectedDate);

            CurrentTitle = entry?.Title ?? title;
            CreatedAt = entry?.CreatedAt;
            UpdatedAt = entry?.UpdatedAt;

            Status = "Saved.";
            Navigation.NavigateTo("/viewjournals");
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }



    private async Task Delete()
    {
        IsBusy = true;
        Status = "";
        try
        {
            await Db.DeleteAsync(SelectedDate);

            Content = "";
            CurrentTitle = "";
            CreatedAt = null;
            UpdatedAt = null;

            Status = "Deleted.";
        }
        finally
        {
            IsBusy = false;
        }
    }


    private async Task HandleTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await ConfirmSave();
        }
        else if (e.Key == "Escape")
        {
            CloseTitleModal();
        }
    }
}
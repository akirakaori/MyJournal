using JournalMaui.Models;
using JournalMaui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

namespace MyJournal.Components.Pages;

public partial class JournalEntry : ComponentBase
{
    [Inject] private JournalDatabases Db { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "date")]
    public string? Date { get; set; }

    private DateTime SelectedDate = DateTime.Today;

    private string Content = "";
    private string CurrentTitle = "";

    private bool IsBusy = false;
    private string Status = "";

    private DateTime? CreatedAt;
    private DateTime? UpdatedAt;

    private bool ShowTitleModal = false;
    private string TitleInput = "";

    private JournalEntries? _current;

    private string CreatedAtText => CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    private string UpdatedAtText => UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    protected override async Task OnParametersSetAsync()
    {
        SelectedDate = ParseQueryDateOrToday(Date);
        await LoadBySelectedDateAsync();
    }

    private static DateTime ParseQueryDateOrToday(string? date)
    {
        if (!string.IsNullOrWhiteSpace(date) &&
            DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }

        return DateTime.Today;
    }

    private async Task LoadBySelectedDateAsync()
    {
        IsBusy = true;
        Status = "";

        try
        {
            _current = await Db.GetByDateAsync(SelectedDate);

            if (_current is null)
            {
                Content = "";
                CurrentTitle = "";
                CreatedAt = null;
                UpdatedAt = null;
                Status = "No entry for this date. Start writing!";
            }
            else
            {
                Content = _current.Content ?? "";
                CurrentTitle = _current.Title ?? "";
                CreatedAt = _current.CreatedAt;
                UpdatedAt = _current.UpdatedAt;
                Status = "Loaded.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

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
        await SaveWithTitleAsync(title);
    }

    private async Task SaveWithTitleAsync(string title)
    {
        IsBusy = true;
        Status = "";

        try
        {
            await Db.SaveAsync(SelectedDate, title, Content);

            _current = await Db.GetByDateAsync(SelectedDate);

            CurrentTitle = _current?.Title ?? title;
            Content = _current?.Content ?? Content;
            CreatedAt = _current?.CreatedAt;
            UpdatedAt = _current?.UpdatedAt;

            Status = "Saved.";

            //  go back to calendar so marker appears immediately
            Navigation.NavigateTo("/viewjournals?refresh=1");
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

            _current = null;
            Content = "";
            CurrentTitle = "";
            CreatedAt = null;
            UpdatedAt = null;

            Status = "Deleted.";

            //  go back to calendar so marker disappears immediately
            Navigation.NavigateTo("/calendar?refresh=1");
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

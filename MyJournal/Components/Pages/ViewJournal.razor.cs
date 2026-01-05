using Microsoft.AspNetCore.Components;
using JournalMaui.Services;
using JournalMaui.Models;

namespace MyJournal.Components.Pages;

public partial class ViewJournal : ComponentBase
{
    private List<JournalEntries> Entries = new();
    private bool IsLoading = true;

    //  Search state (by Title)
    private string SearchTitle = "";

    //  Delete confirmation state
    private bool ShowDeleteConfirm = false;
    private string PendingDeleteDateKey = "";
    private bool IsDeleting = false;

    private List<JournalEntries> FilteredEntries =>
    string.IsNullOrWhiteSpace(SearchTitle)
        ? Entries
        : Entries.Where(e =>
            e.Title.Contains(SearchTitle, StringComparison.OrdinalIgnoreCase)
          ).ToList();


    protected override async Task OnInitializedAsync()
    {
        Entries = await Db.GetRecentAsync(500);
        IsLoading = false;
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        SearchTitle = e?.Value?.ToString() ?? "";
    }

   
    private void Edit(string dateKey)
    {
        var url = $"/journalentry?date={Uri.EscapeDataString(dateKey)}";
        NavigationManager.NavigateTo(url);
    }


    private void PromptDelete(string dateKey)
    {
        PendingDeleteDateKey = dateKey;
        ShowDeleteConfirm = true;
    }

    private void CancelDelete()
    {
        ShowDeleteConfirm = false;
        PendingDeleteDateKey = "";
    }

    private async Task ConfirmDelete()
    {
        if (string.IsNullOrWhiteSpace(PendingDeleteDateKey))
            return;

        IsDeleting = true;
        try
        {
            if (DateTime.TryParse(PendingDeleteDateKey, out var dt))
            {
                await Db.DeleteAsync(dt.Date);
                Entries = await Db.GetRecentAsync(500);
            }
        }
        finally
        {
            IsDeleting = false;
            ShowDeleteConfirm = false;
            PendingDeleteDateKey = "";
        }
    }

   
    private static string GetTitle(string? content)
    {
        content ??= "";
        var lines = content.Replace("\r", "").Split('\n');
        foreach (var raw in lines)
        {
            var line = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // remove markdown heading markers like "#", "##"
            while (line.StartsWith("#")) line = line.Substring(1).Trim();

            return string.IsNullOrWhiteSpace(line) ? "Untitled" : line;
        }
        return "Untitled";
    }

    private static string GetBody(string? content)
    {
        content ??= "";
        var lines = content.Replace("\r", "").Split('\n').ToList();

        // find title line index
        var titleIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = (lines[i] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            titleIndex = i;
            break;
        }

        if (titleIndex < 0) return "";

        // remove the title line
        lines.RemoveAt(titleIndex);

        // trim leading empty lines after removing title
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);

        return string.Join("\n", lines).Trim();
    }

    private static string Trunc(string? s, int n)
    {
        s ??= "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= n ? s : s.Substring(0, n) + "...";
    }
}
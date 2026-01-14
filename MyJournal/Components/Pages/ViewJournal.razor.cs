using Microsoft.AspNetCore.Components;
using JournalMaui.Services;
using JournalMaui.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyJournal.Components.Pages;

public partial class ViewJournal : ComponentBase
{
    [Inject] private JournalDatabases Db { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<JournalEntries> Entries = new();
    private bool IsLoading = true;

    private string SearchTitle = "";

    // delete confirmation
    private bool ShowDeleteConfirm = false;
    private string PendingDeleteDateKey = "";
    private bool IsDeleting = false;

    // detail view - NEW
    private bool ShowDetailView = false;
    private JournalEntries? SelectedEntry = null;

    private string SortColumn = nameof(JournalEntries.DateKey);
    private bool SortAscending = false;

    // paging
    private int PageSize = 5;
    private int CurrentPage = 1;
    private bool SortDescending = true;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IsLoading = true;
        try
        {
            Entries = await Db.GetRecentAsync(2000);
            CurrentPage = 1;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ---------- Detail View - NEW ----------
    private void ViewEntry(JournalEntries entry)
    {
        SelectedEntry = entry;
        ShowDetailView = true;
    }

    private void CloseDetailView()
    {
        ShowDetailView = false;
        SelectedEntry = null;
    }

    private void EditFromDetailView()
    {
        if (SelectedEntry != null)
        {
            ShowDetailView = false;
            Edit(SelectedEntry.DateKey);
        }
    }

    // ---------- Filtering + Sorting ----------
    private IEnumerable<JournalEntries> FilteredSorted()
    {
        IEnumerable<JournalEntries> q = Entries;

        if (!string.IsNullOrWhiteSpace(SearchTitle))
        {
            q = q.Where(e =>
                (e.Title ?? "").Contains(SearchTitle, StringComparison.OrdinalIgnoreCase));
        }

        q = SortColumn switch
        {
            nameof(JournalEntries.Title) =>
                SortAscending
                    ? q.OrderBy(e => e.Title)
                    : q.OrderByDescending(e => e.Title),

            _ =>
                SortAscending
                    ? q.OrderBy(e => ParseDateKey(e.DateKey))
                    : q.OrderByDescending(e => ParseDateKey(e.DateKey))
        };

        return q;
    }

    private void SortBy(string column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        CurrentPage = 1;
    }

    private MarkupString SortIcon(string column)
    {
        if (SortColumn != column)
            return new MarkupString("");

        return SortAscending
            ? new MarkupString(" ▲")
            : new MarkupString(" ▼");
    }

    private static DateTime ParseDateKey(string? dateKey)
    {
        if (!string.IsNullOrWhiteSpace(dateKey) &&
            DateTime.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return dt.Date;

        return DateTime.MinValue;
    }

    // ---------- Computed for UI ----------
    private int FilteredCount => FilteredSorted().Count();

    private int TotalPages
    {
        get
        {
            var count = FilteredCount;
            if (count == 0) return 1;
            return (int)Math.Ceiling(count / (double)PageSize);
        }
    }

    private List<JournalEntries> PagedEntries =>
        FilteredSorted()
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

    private int StartRow => FilteredCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    private int EndRow => Math.Min(CurrentPage * PageSize, FilteredCount);

    private bool IsFirstPage => CurrentPage <= 1;
    private bool IsLastPage => CurrentPage >= TotalPages;

    private IEnumerable<int> PageNumbersToShow
    {
        get
        {
            var total = TotalPages;
            if (total <= 7) return Enumerable.Range(1, total);

            var start = Math.Max(1, CurrentPage - 2);
            var end = Math.Min(total, CurrentPage + 2);

            while (end - start < 4)
            {
                if (start > 1) start--;
                else if (end < total) end++;
                else break;
            }

            return Enumerable.Range(start, end - start + 1);
        }
    }

    // ---------- UI handlers ----------
    private void OnSearchInput(ChangeEventArgs e)
    {
        SearchTitle = e?.Value?.ToString() ?? "";
        CurrentPage = 1;
    }

    private void OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e?.Value?.ToString(), out var size) && size > 0)
        {
            PageSize = size;
            CurrentPage = 1;
        }
    }

    private void ToggleSort()
    {
        SortDescending = !SortDescending;
        CurrentPage = 1;
    }

    private void PrevPage()
    {
        if (CurrentPage > 1) CurrentPage--;
    }

    private void NextPage()
    {
        if (CurrentPage < TotalPages) CurrentPage++;
    }

    private void GoToPage(int page)
    {
        if (page < 1) page = 1;
        if (page > TotalPages) page = TotalPages;
        CurrentPage = page;
    }

    // ---------- Edit ----------
    private void Edit(string dateKey)
    {
        var url = $"/journalentry?date={Uri.EscapeDataString(dateKey)}";
        NavigationManager.NavigateTo(url);
    }

    // ---------- Delete flow ----------
    private async Task PromptDelete(string dateKey)
    {
        PendingDeleteDateKey = dateKey;
        ShowDeleteConfirm = true;
        await Task.CompletedTask;
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
            if (DateTime.TryParseExact(PendingDeleteDateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            {
                await Db.DeleteAsync(dt.Date);
            }

            await ReloadAsync();
        }
        finally
        {
            IsDeleting = false;
            ShowDeleteConfirm = false;
            PendingDeleteDateKey = "";
        }
    }

    // ---------- helpers ----------
    private static string Trunc(string? s, int n)
    {
        s ??= "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= n ? s : s.Substring(0, n) + "...";
    }

    /// <summary>
    /// Strip HTML tags from content for plain text preview - NEW
    /// </summary>
    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        // Remove HTML tags
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        // Remove extra whitespace
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string GetBody(string? content)
    {
        content ??= "";
        return content.Trim();
    }

    private static string GetTitle(string? title)
    {
        title ??= "";
        return string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
    }
}
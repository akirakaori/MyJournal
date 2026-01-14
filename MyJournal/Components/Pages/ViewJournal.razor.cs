using Microsoft.AspNetCore.Components;
using JournalMaui.Services;
using JournalMaui.Models;
using System.Globalization;

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

    private string SortColumn = nameof(JournalEntries.DateKey);
    private bool SortAscending = false; // default: newest first


    // paging
    private int PageSize = 5;
    private int CurrentPage = 1; // 1-based
    private bool SortDescending = true; // newest first

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IsLoading = true;
        try
        {
            Entries = await Db.GetRecentAsync(2000); // load more, paging happens client-side
            CurrentPage = 1;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ---------- Filtering + Sorting ----------
    private IEnumerable<JournalEntries> FilteredSorted()
    {
        IEnumerable<JournalEntries> q = Entries;

        // Search
        if (!string.IsNullOrWhiteSpace(SearchTitle))
        {
            q = q.Where(e =>
                (e.Title ?? "").Contains(SearchTitle, StringComparison.OrdinalIgnoreCase));
        }

        // Sort
        q = SortColumn switch
        {
            nameof(JournalEntries.Title) =>
                SortAscending
                    ? q.OrderBy(e => e.Title)
                    : q.OrderByDescending(e => e.Title),

            _ => // DateKey
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
            // toggle direction
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
            ? new MarkupString(" ?")
            : new MarkupString(" ?");
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

    // show page numbers around current page
    private IEnumerable<int> PageNumbersToShow
    {
        get
        {
            var total = TotalPages;
            if (total <= 7) return Enumerable.Range(1, total);

            var start = Math.Max(1, CurrentPage - 2);
            var end = Math.Min(total, CurrentPage + 2);

            // expand to 5 pages if possible
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

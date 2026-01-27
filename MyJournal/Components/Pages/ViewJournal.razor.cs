// ViewJournals.razor.cs
using Microsoft.AspNetCore.Components;
using JournalMaui.Services;
using JournalMaui.Models;
using MudBlazor;
using MyJournal.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyJournal.Components.Pages;

public partial class ViewJournal : ComponentBase
{
    [Inject] private JournalDatabases Db { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private PinUnlockService PinUnlock { get; set; } = default!;
    [Inject] private PdfExportService PdfExportService { get; set; } = default!;

    private bool IsPinVerifying = false;

    private List<JournalEntries> Entries = new();
    private bool IsLoading = true;

    private string SearchTitle = "";
    private DateTime? FromDate = null;
    private DateTime? ToDate = null;

    // ✅ NEW: filter lists + selected
    private List<string> AllMoods = new();
    private List<string> AllTags = new();
    private HashSet<string> SelectedMoods = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> SelectedTags = new(StringComparer.OrdinalIgnoreCase);

    private int TotalCount = 0;

    private string SortColumn = nameof(JournalEntries.DateKey);
    private bool SortAscending = false;

    private int PageSize = 5;
    private int CurrentPage = 1;

    private bool ShowDeleteConfirm = false;
    private string PendingDeleteDateKey = "";
    private bool IsDeleting = false;

    private bool ShowDetailView = false;
    private JournalEntries? SelectedEntry = null;

    // Export PDF Dialog
    private bool ShowExportDialog = false;
    private string ExportStatus = "";
    private bool ExportSuccess = false;

    private void OpenExportDialog()
    {
        ShowExportDialog = true;
        ExportStatus = "";
        ExportSuccess = false;
    }

    private void CloseExportDialog()
    {
        ShowExportDialog = false;
    }

    private Task HandleExported(string path)
    {
        ExportStatus = $"PDF exported successfully! Saved to: {path}";
        ExportSuccess = true;
        StateHasChanged();

        _ = Task.Run(async () =>
        {
            await Task.Delay(10000);
            ExportStatus = "";
            await InvokeAsync(StateHasChanged);
        });

        return Task.CompletedTask;
    }

    private Task HandleExportError(string message)
    {
        ExportStatus = $"Export failed: {message}";
        ExportSuccess = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // PIN Prompt
    private bool ShowPinModal = false;
    private string PinInput = "";
    private string PinError = "";
    private string PendingPinDateKey = "";
    private JournalEntries? PendingPinEntry = null;

    private bool ShowForceDeleteConfirm = false;
    private bool IsForceDeleting = false;

    private enum PinAction { View, Edit, Delete }
    private PinAction PendingPinAction;

    protected override async Task OnInitializedAsync()
    {
        // ✅ load unique moods/tags once
        AllMoods = await Db.GetDistinctMoodsAsync();
        AllTags = await Db.GetDistinctTagsAsync();

        await ReloadAsync();
    }

    // ✅ Mood/Tag toggle
    private async Task ToggleMood(string mood)
    {
        if (SelectedMoods.Contains(mood)) SelectedMoods.Remove(mood);
        else SelectedMoods.Add(mood);

        CurrentPage = 1;
        await ReloadAsync();
    }

    private async Task ToggleTag(string tag)
    {
        if (SelectedTags.Contains(tag)) SelectedTags.Remove(tag);
        else SelectedTags.Add(tag);

        CurrentPage = 1;
        await ReloadAsync();
    }

    private async Task ClearMoodFilter()
    {
        SelectedMoods.Clear();
        CurrentPage = 1;
        await ReloadAsync();
    }

    private async Task ClearTagFilter()
    {
        SelectedTags.Clear();
        CurrentPage = 1;
        await ReloadAsync();
    }

    // ============================================================
    // Reload
    // ============================================================
    private async Task ReloadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await Db.SearchAsync(
                titleContains: string.IsNullOrWhiteSpace(SearchTitle) ? null : SearchTitle.Trim(),
                fromDate: FromDate,
                toDate: ToDate,
                moods: SelectedMoods.Count == 0 ? null : SelectedMoods.ToList(),
                tags: SelectedTags.Count == 0 ? null : SelectedTags.ToList(),
                sortColumn: SortColumn,
                sortAscending: SortAscending,
                page: CurrentPage,
                pageSize: PageSize
            );

            Entries = result.Items ?? new List<JournalEntries>();
            TotalCount = result.TotalCount;

            var totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Date helpers
    private string FromDateText => FromDate.HasValue ? FromDate.Value.ToString("yyyy-MM-dd") : "";
    private string ToDateText => ToDate.HasValue ? ToDate.Value.ToString("yyyy-MM-dd") : "";

    // Date range picker UI state (copied from Dashboard style)
    private DateRange _tempRange = default!;
    private bool _pickerOpen = false;

    private string RangeLabel =>
        FromDate.HasValue && ToDate.HasValue
            ? $"{FromDate.Value:dd MMM yyyy} → {ToDate.Value:dd MMM yyyy}"
            : "No range selected";

    private void TogglePicker()
    {
        // initialize temp range from current from/to
        _tempRange = new DateRange(FromDate, ToDate);
        _pickerOpen = !_pickerOpen;
    }

    private void ClosePicker()
    {
        _pickerOpen = false;
    }

    private async Task ApplyRange()
    {
        if (!_tempRange.Start.HasValue || !_tempRange.End.HasValue)
        {
            _pickerOpen = false;
            return;
        }

        FromDate = _tempRange.Start.Value.Date;
        ToDate = _tempRange.End.Value.Date;
        _pickerOpen = false;
        CurrentPage = 1;
        await ReloadAsync();
    }

    private async Task OnFromDateChanged(ChangeEventArgs e)
    {
        FromDate = ParseDateInput(e?.Value?.ToString());
        CurrentPage = 1;
        await ReloadAsync();
    }

    private async Task OnToDateChanged(ChangeEventArgs e)
    {
        ToDate = ParseDateInput(e?.Value?.ToString());
        CurrentPage = 1;
        await ReloadAsync();
    }

    private async Task ClearDates()
    {
        FromDate = null;
        ToDate = null;
        CurrentPage = 1;
        await ReloadAsync();
    }

    private static DateTime? ParseDateInput(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt))
        {
            return dt.Date;
        }

        return null;
    }

    // Sorting
    private async Task SortBy(string column)
    {
        if (SortColumn == column)
            SortAscending = !SortAscending;
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        CurrentPage = 1;
        await ReloadAsync();
    }

    private MarkupString SortIcon(string column)
    {
        if (SortColumn != column)
            return new MarkupString("");

        return SortAscending ? new MarkupString(" ▲") : new MarkupString(" ▼");
    }

    // Paging
    private int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    private int StartRow => TotalCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    private int EndRow => Math.Min(CurrentPage * PageSize, TotalCount);

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

    private async Task PrevPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await ReloadAsync();
        }
    }

    private async Task NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await ReloadAsync();
        }
    }

    private async Task GoToPage(int page)
    {
        if (page < 1) page = 1;
        if (page > TotalPages) page = TotalPages;
        if (page == CurrentPage) return;

        CurrentPage = page;
        await ReloadAsync();
    }

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e?.Value?.ToString(), out var size) && size > 0)
        {
            PageSize = size;
            CurrentPage = 1;
            await ReloadAsync();
        }
    }

    // Search
    private async Task OnSearchInput(ChangeEventArgs e)
    {
        SearchTitle = e?.Value?.ToString() ?? "";
        CurrentPage = 1;
        await ReloadAsync();
    }

    // Detail view
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

    // Protected wrappers
    private void RequestView(JournalEntries entry)
    {
        if (entry.HasPin)
        {
            OpenPinModal(entry, PinAction.View);
            return;
        }

        ViewEntry(entry);
    }

    private void RequestEdit(string dateKey)
    {
        var entry = Entries.FirstOrDefault(x => x.DateKey == dateKey);
        if (entry != null && entry.HasPin)
        {
            OpenPinModal(entry, PinAction.Edit);
            return;
        }

        Edit(dateKey);
    }

    private async Task RequestDelete(string dateKey)
    {
        var entry = Entries.FirstOrDefault(x => x.DateKey == dateKey);
        if (entry != null && entry.HasPin)
        {
            OpenPinModal(entry, PinAction.Delete);
            return;
        }

        await PromptDelete(dateKey);
    }

    private void OpenPinModal(JournalEntries entry, PinAction action)
    {
        PendingPinEntry = entry;
        PendingPinDateKey = entry.DateKey;
        PendingPinAction = action;

        PinInput = "";
        PinError = "";
        ShowPinModal = true;
        ShowForceDeleteConfirm = false;
    }

    private void CancelPin()
    {
        ShowPinModal = false;
        PinInput = "";
        PinError = "";
        PendingPinEntry = null;
        PendingPinDateKey = "";
        ShowForceDeleteConfirm = false;
        IsForceDeleting = false;
    }

    private async Task OnPinInputChanged(ChangeEventArgs e)
    {
        var raw = e?.Value?.ToString() ?? "";
        PinInput = NormalizePin(raw);
        PinError = "";

        if (PinInput.Length > 0 && PinInput.Length < 4)
        {
            PinError = "PIN must be exactly 4 characters.";
            return;
        }

        if (PinInput.Length == 4 && !IsPinVerifying)
        {
            IsPinVerifying = true;
            try
            {
                await ConfirmPin();
            }
            finally
            {
                IsPinVerifying = false;
            }
        }
    }

    private async Task ConfirmPin()
    {
        if (PendingPinEntry == null)
        {
            CancelPin();
            return;
        }

        var entered = NormalizePin(PinInput);
        if (string.IsNullOrWhiteSpace(entered) || entered.Length != 4)
        {
            PinError = "Enter the 4-character PIN.";
            return;
        }

        var saved = NormalizePin(PendingPinEntry.Pin ?? "");

        if (!string.Equals(entered, saved, StringComparison.Ordinal))
        {
            PinError = "Incorrect PIN.";
            return;
        }

        ShowPinModal = false;

        var action = PendingPinAction;
        var dateKey = PendingPinDateKey;

        // remember unlock for journalentry page
        PinUnlock.Unlock(dateKey, TimeSpan.FromMinutes(5));

        PendingPinEntry = null;
        PendingPinDateKey = "";
        PinInput = "";
        PinError = "";

        if (action == PinAction.View)
        {
            PinUnlock.Unlock(dateKey, TimeSpan.FromMinutes(5));
            await LoadAndPreviewAsync(dateKey);
        }
        else if (action == PinAction.Edit)
        {
            Edit(dateKey);
        }
        else if (action == PinAction.Delete)
        {
            await PromptDelete(dateKey);
        }
    }

    private async Task LoadAndPreviewAsync(string dateKey)
    {
        if (!DateTime.TryParseExact(
            dateKey,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dt))
            return;

        var full = await Db.GetByDateAsync(dt.Date);
        if (full == null) return;

        SelectedEntry = full;
        ShowDetailView = true;
    }

    // Force delete
    private void OpenForceDeleteConfirm()
    {
        if (PendingPinEntry == null || string.IsNullOrWhiteSpace(PendingPinDateKey))
            return;

        ShowForceDeleteConfirm = true;
    }

    private void CloseForceDeleteConfirm()
    {
        ShowForceDeleteConfirm = false;
        IsForceDeleting = false;
    }

    private async Task ForceDeleteWithoutPin()
    {
        if (string.IsNullOrWhiteSpace(PendingPinDateKey))
            return;

        IsForceDeleting = true;

        try
        {
            if (DateTime.TryParseExact(PendingPinDateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            {
                await Db.DeleteAsync(dt.Date);
            }

            ShowForceDeleteConfirm = false;
            ShowPinModal = false;

            PendingPinEntry = null;
            PendingPinDateKey = "";
            PinInput = "";
            PinError = "";

            await ReloadAsync();
        }
        finally
        {
            IsForceDeleting = false;
        }
    }

    private static string NormalizePin(string input)
    {
        var s = new string((input ?? "")
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray())
            .ToUpperInvariant();

        s = new string(s.Where(char.IsLetterOrDigit).ToArray());

        if (s.Length > 4) s = s.Substring(0, 4);

        return s;
    }

    // Edit + Delete flows
    private void Edit(string dateKey)
    {
        var url = $"/journalentry?date={Uri.EscapeDataString(dateKey)}";
        NavigationManager.NavigateTo(url);
    }

    private Task PromptDelete(string dateKey)
    {
        PendingDeleteDateKey = dateKey;
        ShowDeleteConfirm = true;
        return Task.CompletedTask;
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

    // Helpers
    private static string Trunc(string? s, int n)
    {
        s ??= "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= n ? s : s.Substring(0, n) + "...";
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string GetTitle(string? title)
    {
        title ??= "";
        return string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
    }
}

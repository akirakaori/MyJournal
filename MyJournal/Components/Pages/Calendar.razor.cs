using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JournalMaui.Services;
using JournalMaui.Models;
using MudBlazor;

namespace MyJournal.Components.Pages;

public partial class Calendar : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private CalendarDb CalendarDb { get; set; } = default!;
    [Inject] private JournalDatabases JournalDb { get; set; } = default!;

    // ✅ popup dialog service
    [Inject] private IDialogService DialogService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "refresh")]
    public string? Refresh { get; set; }

    protected string CalendarElementId { get; } = "fc-" + Guid.NewGuid().ToString("N");
    protected string? Error { get; set; }

    private DotNetObjectReference<Calendar>? _dotNetRef;
    private bool _initialized;

    protected bool ShowEditor { get; set; }
    protected bool IsEditingExisting { get; set; }
    protected string EditorTitle => IsEditingExisting ? "Edit event" : "New event";

    private string _id = Guid.NewGuid().ToString();
    protected string DraftTitle { get; set; } = "";
    protected string? DraftNotes { get; set; }
    protected bool DraftAllDay { get; set; } = true;

    private DateTime _start = DateTime.Now;
    private DateTime? _end = null;

    protected bool ShowFutureDateModal { get; set; }

    private void OpenFutureDateModal()
    {
        ShowFutureDateModal = true;
        StateHasChanged();
    }

    protected void CloseFutureDateModal()
    {
        ShowFutureDateModal = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);

            var events = await CalendarDb.GetAllAsync();
            var fcEvents = events.Select(ToFullCalendarDto).ToList();

            var journals = await JournalDb.GetRecentAsync(500);
            foreach (var j in journals)
            {
                if (DateTime.TryParseExact(j.DateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var d))
                {
                    var title = string.IsNullOrWhiteSpace(j.Title) ? "Untitled" : j.Title.Trim();

                    fcEvents.Add(new
                    {
                        id = "J-" + j.DateKey,
                        title = title,
                        start = d.Date.ToString("o"),
                        allDay = true,
                        classNames = j.HasPin
                            ? new[] { "journalDay", "pinned" }
                            : new[] { "journalDay" }
                    });
                }
            }

            await JS.InvokeVoidAsync("fullCalendarInterop.init", CalendarElementId, _dotNetRef, fcEvents);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            StateHasChanged();
        }
    }

    private static bool IsFutureDate(DateTime d) => d.Date > DateTime.Today;

    // ✅ single popup function
    private Task ShowFutureDateAlert()
    {
        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true
        };

        return DialogService.ShowMessageBox(
            title: "Not allowed",
            markupMessage: (MarkupString)"You can’t add or open journals for <b>future dates</b>.",
            yesText: "OK",
            options: options
        ) ?? Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnDateClick(string dateStr)
    {
        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            dt = DateTime.Today;

        if (IsFutureDate(dt))
        {
            OpenFutureDateModal();
            return Task.CompletedTask;
        }

        Nav.NavigateTo($"/journalentry?date={dt:yyyy-MM-dd}");
        return Task.CompletedTask;
    }


    [JSInvokable]
    public async Task OnEventClick(string eventId)
    {
        // Journal event
        if (eventId.StartsWith("J-", StringComparison.OrdinalIgnoreCase))
        {
            var dateKey = eventId.Substring(2);

            if (DateTime.TryParseExact(
                    dateKey,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var d)
                && IsFutureDate(d))
            {
                OpenFutureDateModal();
                return;
            }

            Nav.NavigateTo($"/journalentry?date={dateKey}");
            return;
        }

        // Normal calendar event (unchanged)
        var ev = await CalendarDb.GetByIdAsync(eventId);
        if (ev is null) return;

        _id = ev.Id;
        DraftTitle = ev.Title;
        DraftNotes = ev.Notes;
        DraftAllDay = ev.AllDay;

        _start = DateTime.Parse(ev.StartIso, null, DateTimeStyles.RoundtripKind);
        _end = string.IsNullOrWhiteSpace(ev.EndIso)
            ? null
            : DateTime.Parse(ev.EndIso!, null, DateTimeStyles.RoundtripKind);

        IsEditingExisting = true;
        ShowEditor = true;
        StateHasChanged();
    }


    protected void CloseEditor()
    {
        ShowEditor = false;
        Error = null;
    }

    protected async Task SaveEvent()
    {
        try
        {
            await CalendarDb.SaveAsync(_id, DraftTitle, _start, _end, DraftAllDay, DraftNotes);

            var saved = await CalendarDb.GetByIdAsync(_id);
            if (saved is not null)
                await JS.InvokeVoidAsync("fullCalendarInterop.addOrUpdateEvent", CalendarElementId, ToFullCalendarDto(saved));

            ShowEditor = false;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    protected async Task DeleteEvent()
    {
        try
        {
            await CalendarDb.DeleteAsync(_id);
            await JS.InvokeVoidAsync("fullCalendarInterop.removeEvent", CalendarElementId, _id);
            ShowEditor = false;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private static object ToFullCalendarDto(CalendarEvents ev) => new
    {
        id = ev.Id,
        title = ev.Title,
        start = ev.StartIso,
        end = ev.EndIso,
        allDay = ev.AllDay
    };

    public ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        return ValueTask.CompletedTask;
    }
}

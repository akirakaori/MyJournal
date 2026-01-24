using JournalMaui.Models;
using JournalMaui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Globalization;

namespace MyJournal.Components.Pages;

public partial class JournalEntry : ComponentBase, IAsyncDisposable
{
    [Inject] private JournalDatabases Db { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private PinUnlockService PinUnlock { get; set; } = default!;


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
    private DotNetObjectReference<JournalEntry>? _dotNetRef;
    private int CharacterCount = 0;

    private string CreatedAtText => CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    private string UpdatedAtText => UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    // ---------------------------
    // Mood Tracking (Feature 3)
    // ---------------------------
    private record MoodConfig(string Name, string Emoji, string Category);
    
    private static readonly List<MoodConfig> _moodConfigs = new()
    {
        // POSITIVE
        new("Happy", "😊", "Positive"),
        new("Excited", "🤩", "Positive"),
        new("Relaxed", "😌", "Positive"),
        new("Grateful", "🙏", "Positive"),
        new("Confident", "💪", "Positive"),
        
        // NEUTRAL
        new("Calm", "😐", "Neutral"),
        new("Thoughtful", "🤔", "Neutral"),
        new("Curious", "🧐", "Neutral"),
        new("Nostalgic", "🥺", "Neutral"),
        new("Bored", "😑", "Neutral"),
        
        // NEGATIVE
        new("Sad", "😢", "Negative"),
        new("Angry", "😠", "Negative"),
        new("Stressed", "😣", "Negative"),
        new("Lonely", "😔", "Negative"),
        new("Anxious", "😰", "Negative")
    };

    private List<MoodConfig> GetMoodConfig() => _moodConfigs;
    
    // Category filtering for UI
    private string SelectedPrimaryCategory = "Positive";
    private string SelectedSecondaryCategory = "Positive";
    
    private List<MoodConfig> GetMoodsByCategory(string category)
    {
        return _moodConfigs.Where(m => m.Category == category).ToList();
    }
    
    private void SelectPrimaryCategory(string category)
    {
        SelectedPrimaryCategory = category;
        // Clear primary mood and secondary moods when category changes for clean UX
        PrimaryMood = "";
        SecondaryMoods.Clear();
    }
    
    private void SelectSecondaryCategory(string category)
    {
        SelectedSecondaryCategory = category;
    }

    private string _primaryMood = "";
    private string PrimaryMood
    {
        get => _primaryMood;
        set
        {
            _primaryMood = value;
            MoodError = "";
            // Remove from secondary if it was selected
            if (SecondaryMoods.Contains(value))
            {
                SecondaryMoods.Remove(value);
            }
        }
    }

    private void SelectPrimaryMood(string mood)
    {
        // Selecting a new primary mood automatically replaces the previous one
        // and clears secondary moods to prevent conflicts
        if (_primaryMood != mood)
        {
            SecondaryMoods.Clear();
        }
        PrimaryMood = mood;
        MoodError = "";
    }

    private HashSet<string> SecondaryMoods = new();
    private string MoodError = "";

    private void OnSecondaryMoodToggle(string mood)
    {
        MoodError = "";

        // Cannot select primary mood as secondary
        if (mood.Equals(PrimaryMood, StringComparison.OrdinalIgnoreCase))
        {
            MoodError = "Secondary mood cannot be the same as primary mood.";
            return;
        }

        if (SecondaryMoods.Contains(mood))
        {
            SecondaryMoods.Remove(mood);
        }
        else
        {
            // Enforce maximum of 2 secondary moods
            if (SecondaryMoods.Count >= 2)
            {
                MoodError = "You can select at most 2 secondary moods.";
                return;
            }

            SecondaryMoods.Add(mood);
        }
    }

    // ---------------------------
    // Save PIN (optional)
    // ---------------------------
    private string PinInput = "";
    private bool PinMasked = true;
    private string PinError = "";
    private bool HasPin = false;

    // ---------------------------
    // Unlock gate
    // ---------------------------
    private bool IsLocked = false;
    private bool ShowUnlockModal = false;
    private string UnlockPinInput = "";
    private string UnlockError = "";

    // Hold protected data until unlock
    private string _lockedContent = "";
    private string _lockedTitle = "";

    // Eye icons (used only in SAVE modal)
    private const string EyeSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" fill=\"none\" viewBox=\"0 0 24 24\" stroke-width=\"2\" stroke=\"currentColor\">" +
        "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M2.458 12C3.732 7.943 7.523 5 12 5c4.477 0 8.268 2.943 9.542 7-1.274 4.057-5.065 7-9.542 7-4.477 0-8.268-2.943-9.542-7z\"/>" +
        "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M15 12a3 3 0 11-6 0 3 3 0 016 0z\"/>" +
        "</svg>";

    private const string EyeOffSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" fill=\"none\" viewBox=\"0 0 24 24\" stroke-width=\"2\" stroke=\"currentColor\">" +
        "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M3 3l18 18\"/>" +
        "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M10.477 10.48a3 3 0 104.243 4.243\"/>" +
        "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M9.88 5.098A10.477 10.477 0 0112 5c4.477 0 8.268 2.943 9.542 7a10.55 10.55 0 01-4.132 5.412\"/>" +
        "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"M6.228 6.228A10.45 10.45 0 002.458 12c1.274 4.057 5.065 7 9.542 7 1.109 0 2.176-.18 3.176-.512\"/>" +
        "</svg>";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("initQuill", "journal-description", _dotNetRef);

            // If locked, keep editor empty
            await JS.InvokeVoidAsync("setQuillHtml", IsLocked ? "" : (Content ?? ""));
        }
    }

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

            // reset lock state for new date
            IsLocked = false;
            ShowUnlockModal = false;
            UnlockPinInput = "";
            UnlockError = "";

            _lockedContent = "";
            _lockedTitle = "";

            if (_current is null)
            {
                Content = "";
                CurrentTitle = "";
                CreatedAt = null;
                UpdatedAt = null;
                CharacterCount = 0;

                HasPin = false;
                PinInput = "";
                PinError = "";

                // Reset moods
                _primaryMood = "";
                SecondaryMoods.Clear();
                MoodError = "";

                Status = "No entry for this date. Start writing!";

                if (_dotNetRef is not null)
                    await JS.InvokeVoidAsync("setQuillHtml", "");
            }
            else
            {
                CreatedAt = _current.CreatedAt;
                UpdatedAt = _current.UpdatedAt;
                HasPin = _current.HasPin;

                if (_current.HasPin && !PinUnlock.IsUnlocked(_current.DateKey))
                {
                    // LOCK (only when not already unlocked)
                    IsLocked = true;
                    ShowUnlockModal = true;

                    _lockedContent = _current.Content ?? "";
                    _lockedTitle = _current.Title ?? "";

                    Content = "";
                    CurrentTitle = _lockedTitle; // show title only
                    CharacterCount = 0;

                    if (_dotNetRef is not null)
                        await JS.InvokeVoidAsync("setQuillHtml", "");

                    Status = "Locked.";
                }
                else
                {
                    // Either not PIN protected OR already unlocked via PinUnlockService
                    IsLocked = false;
                    ShowUnlockModal = false;

                    Content = _current.Content ?? "";
                    CurrentTitle = _current.Title ?? "";

                    // Load moods
                    _primaryMood = _current.PrimaryMood ?? "";
                    SecondaryMoods.Clear();
                    if (!string.IsNullOrWhiteSpace(_current.SecondaryMoodsCsv))
                    {
                        var moods = _current.SecondaryMoodsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var mood in moods.Take(2))
                        {
                            SecondaryMoods.Add(mood.Trim());
                        }
                    }
                    MoodError = "";

                    if (_dotNetRef is not null)
                        await JS.InvokeVoidAsync("setQuillHtml", Content);

                    Status = "Loaded.";
                }

            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [JSInvokable]
    public void OnQuillContentChanged(string html, int length)
    {
        if (IsLocked) return;
        Content = html;
        CharacterCount = length;
        StateHasChanged();
    }

    // ---------------------------
    // Unlock modal
    // ---------------------------
    private void OnUnlockOverlayClick()
    {
        // keep strict: no close by clicking outside
    }

    private void CloseUnlockModal()
    {
        // user can cancel, but stays locked
        ShowUnlockModal = false;
        Navigation.NavigateTo("/dashboard");

    }

    private void OnUnlockPinInputChanged(ChangeEventArgs e)
    {
        UnlockPinInput = NormalizePin(e.Value?.ToString() ?? "");
        UnlockError = "";
    }

    private void ClearUnlockPin()
    {
        UnlockPinInput = "";
        UnlockError = "";
    }

    private async Task HandleUnlockKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await VerifyUnlockPin();
        else if (e.Key == "Escape")
            CloseUnlockModal();
    }


    private async Task VerifyUnlockPin()
    {
        if (_current is null || !_current.HasPin)
        {
            IsLocked = false;
            ShowUnlockModal = false;
            return;
        }

        UnlockError = "";
        var pin = NormalizePin(UnlockPinInput);

        if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4)
        {
            UnlockError = "Please enter a valid 4-character PIN.";
            return;
        }

        var stored = NormalizePin(_current.Pin ?? "");

        if (pin != stored)
        {
            UnlockError = "Incorrect PIN. Try again.";
            return;
        }

        // Unlock success
        IsLocked = false;
        ShowUnlockModal = false;

        Content = _lockedContent;
        CurrentTitle = _lockedTitle;

        if (_dotNetRef is not null)
            await JS.InvokeVoidAsync("setQuillHtml", Content);

        Status = "Unlocked.";
    }

    private async Task ForceDeleteLockedJournal()
    {
        IsBusy = true;
        Status = "";

        try
        {
            await Db.DeleteAsync(SelectedDate);

            _current = null;

            IsLocked = false;
            ShowUnlockModal = false;

            Content = "";
            CurrentTitle = "";
            CreatedAt = null;
            UpdatedAt = null;
            CharacterCount = 0;

            HasPin = false;
            PinInput = "";
            PinError = "";

            _lockedContent = "";
            _lockedTitle = "";

            UnlockPinInput = "";
            UnlockError = "";

            if (_dotNetRef is not null)
                await JS.InvokeVoidAsync("setQuillHtml", "");

            Status = "Deleted.";
            Navigation.NavigateTo("/calendar?refresh=1");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---------------------------
    // Save flow (title modal)
    // ---------------------------
    private async Task StartSave()
    {
        if (IsLocked) return;

        Content = await JS.InvokeAsync<string>("getQuillHtml");

        Status = "";
        TitleInput = string.IsNullOrWhiteSpace(CurrentTitle) ? "" : CurrentTitle;

        PinInput = "";
        PinError = "";
        PinMasked = true;

        ShowTitleModal = true;
    }

    private void CloseTitleModal() => ShowTitleModal = false;

    private async Task ConfirmSave()
    {
        if (IsLocked) return;

        var title = (TitleInput ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            Status = "Please enter a title.";
            return;
        }

        // Validate mood selection
        if (string.IsNullOrWhiteSpace(PrimaryMood))
        {
            Status = "Please select a primary mood.";
            return;
        }

        PinError = "";
        var pin = NormalizePin(PinInput);

        if (!string.IsNullOrEmpty(pin) && pin.Length != 4)
        {
            PinError = "PIN must be exactly 4 characters (or leave it empty).";
            return;
        }

        ShowTitleModal = false;
        await SaveWithTitleAsync(title, pin);
    }

    private async Task SaveWithTitleAsync(string title, string pin)
    {
        IsBusy = true;
        Status = "";

        try
        {
            Content = await JS.InvokeAsync<string>("getQuillHtml");

            var hasPin = !string.IsNullOrEmpty(pin);
            var pinToSave = hasPin ? pin : null;

            var secondaryMoodsList = SecondaryMoods.ToList();

            await Db.SaveAsync(SelectedDate, title, Content, hasPin, pinToSave, PrimaryMood, secondaryMoodsList);

            _current = await Db.GetByDateAsync(SelectedDate);

            CurrentTitle = _current?.Title ?? title;
            Content = _current?.Content ?? Content;
            CreatedAt = _current?.CreatedAt;
            UpdatedAt = _current?.UpdatedAt;
            HasPin = _current?.HasPin ?? hasPin;

            Status = "Saved.";
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
        if (IsLocked) return;

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
            CharacterCount = 0;

            HasPin = false;
            PinInput = "";
            PinError = "";

            Status = "Deleted.";

            if (_dotNetRef is not null)
                await JS.InvokeVoidAsync("setQuillHtml", "");

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
            await ConfirmSave();
        else if (e.Key == "Escape")
            CloseTitleModal();
    }

    // ---------------------------
    // PIN helpers (save modal)
    // ---------------------------
    private void TogglePinMask() => PinMasked = !PinMasked;

    private void ClearPin()
    {
        PinInput = "";
        PinError = "";
    }

    private void OnPinInputChanged(ChangeEventArgs e)
    {
        PinInput = NormalizePin(e.Value?.ToString() ?? "");

        if (!string.IsNullOrEmpty(PinInput) && PinInput.Length is > 0 and < 4)
            PinError = "PIN must be exactly 4 characters.";
        else
            PinError = "";
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

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await Task.CompletedTask;
    }
}

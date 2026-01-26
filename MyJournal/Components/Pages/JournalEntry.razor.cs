using JournalMaui.Models;
using JournalMaui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Globalization;
using Bogus;
using System.Net;
using System.Text;

namespace MyJournal.Components.Pages;

public partial class JournalEntry : ComponentBase, IAsyncDisposable
{
    [Inject] private JournalDatabases Db { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private PinUnlockService PinUnlock { get; set; } = default!;
    [Inject] private MyJournal.Services.CustomTagService TagService { get; set; } = default!;
    //[Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Faker _faker = new Faker("en_US");

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

    private string _titleInput = "";
    private string TitleInput
    {
        get => _titleInput;
        set
        {
            _titleInput = value ?? "";
            Status = "";
        }
    }

    private JournalEntries? _current;
    private DotNetObjectReference<JournalEntry>? _dotNetRef;
    private int CharacterCount = 0;

    private string CreatedAtText => CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    private string UpdatedAtText => UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-";

    // ---------------------------
    // Tags
    // ---------------------------
    private readonly List<string> PrebuiltTags = new()
    {
        "Work", "Career", "Studies", "Family", "Friends", "Relationships",
        "Health", "Fitness", "Personal Growth", "Self-care", "Hobbies",
        "Travel", "Nature", "Finance", "Spirituality",
        "Birthday", "Holiday", "Vacation", "Celebration",
        "Exercise", "Reading", "Writing", "Cooking",
        "Meditation", "Yoga", "Music", "Shopping",
        "Parenting", "Projects", "Planning", "Reflection"
    };

    private void ToggleTag(string tag)
    {
        tag = (tag ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tag)) return;

        if (SelectedTags.Contains(tag))
            SelectedTags.Remove(tag);
        else
            SelectedTags.Add(tag);
    }


    private List<MyJournal.Models.CustomTag> CustomTags = new();

    // Selected tags only live here; available lists are derived in razor
    private HashSet<string> SelectedTags = new(StringComparer.OrdinalIgnoreCase);

    private void AddSelectedTag(string tag)
    {
        tag = (tag ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tag)) return;
        SelectedTags.Add(tag);
    }

    private void RemoveSelectedTag(string tag)
    {
        tag = (tag ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tag)) return;
        SelectedTags.Remove(tag);
    }

    private async Task LoadCustomTagsAsync()
    {
        try
        {
            await TagService.InitAsync();
            CustomTags = await TagService.GetAllAsync();
        }
        catch
        {
            CustomTags = new();
        }
    }

    // ---------------------------
    // Mood Tracking
    // ---------------------------
    private record MoodConfig(string Name, string Emoji, string Category);

    private static readonly List<MoodConfig> _moodConfigs = new()
    {
        new("Happy", "😊", "Positive"),
        new("Excited", "🤩", "Positive"),
        new("Relaxed", "😌", "Positive"),
        new("Grateful", "🙏", "Positive"),
        new("Confident", "💪", "Positive"),

        new("Calm", "😐", "Neutral"),
        new("Thoughtful", "🤔", "Neutral"),
        new("Curious", "🧐", "Neutral"),
        new("Nostalgic", "🥺", "Neutral"),
        new("Bored", "😑", "Neutral"),

        new("Sad", "😢", "Negative"),
        new("Angry", "😠", "Negative"),
        new("Stressed", "😣", "Negative"),
        new("Lonely", "😔", "Negative"),
        new("Anxious", "😰", "Negative")
    };

    private string SelectedPrimaryCategory = "Positive";
    private string SelectedSecondaryCategory = "Positive";

    private List<MoodConfig> GetMoodsByCategory(string category)
        => _moodConfigs.Where(m => m.Category == category).ToList();

    private void SelectPrimaryCategory(string category)
    {
        SelectedPrimaryCategory = category;
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
            _primaryMood = value ?? "";
            MoodError = "";
            if (SecondaryMoods.Contains(_primaryMood))
                SecondaryMoods.Remove(_primaryMood);
        }
    }

    private void SelectPrimaryMood(string mood)
    {
        if (_primaryMood != mood)
            SecondaryMoods.Clear();

        PrimaryMood = mood;
        MoodError = "";
    }

    private HashSet<string> SecondaryMoods = new(StringComparer.OrdinalIgnoreCase);
    private string MoodError = "";

    private void OnSecondaryMoodToggle(string mood)
    {
        MoodError = "";

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
            if (SecondaryMoods.Count >= 2)
            {
                MoodError = "You can select at most 2 secondary moods.";
                return;
            }
            SecondaryMoods.Add(mood);
        }
    }

    // ---------------------------
    // PIN
    // ---------------------------
    private string _pinInput = "";
    private string PinInput
    {
        get => _pinInput;
        set
        {
            _pinInput = NormalizePin(value ?? "");
            if (!string.IsNullOrEmpty(_pinInput) && _pinInput.Length is > 0 and < 4)
                PinError = "PIN must be exactly 4 characters.";
            else
                PinError = "";
        }
    }

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

    private string _lockedContent = "";
    private string _lockedTitle = "";

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

    private async Task GenerateRandomEntry()
    {
        if (IsLocked) return;

        var paraCount = _faker.Random.Int(3, 7);
        var sb = new StringBuilder();

        for (var i = 0; i < paraCount; i++)
        {
            var p = _faker.Lorem.Paragraph(_faker.Random.Int(3, 7));
            sb.Append("<p>");
            sb.Append(WebUtility.HtmlEncode(p));
            sb.Append("</p>");
        }

        Content = sb.ToString();

        if (_dotNetRef is not null)
            await JS.InvokeVoidAsync("setQuillHtml", Content);

        CharacterCount = WebUtility.HtmlDecode(string.Concat(Content
            .Replace("<p>", "")
            .Replace("</p>", "")
            .Replace("&nbsp;", " ")
        )).Length;

        Status = "random text generated.";
    }

    private async Task LoadBySelectedDateAsync()
    {
        IsBusy = true;
        Status = "";

        try
        {
            _current = await Db.GetByDateAsync(SelectedDate);

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
                TitleInput = "";
                CreatedAt = null;
                UpdatedAt = null;
                CharacterCount = 0;

                HasPin = false;
                PinInput = "";
                PinError = "";

                _primaryMood = "";
                SecondaryMoods.Clear();
                MoodError = "";

                SelectedTags.Clear();

                if (_dotNetRef is not null)
                    await JS.InvokeVoidAsync("setQuillHtml", "");

                Status = "No entry for this date. Start writing!";
                return;
            }

            CreatedAt = _current.CreatedAtDateTime;
            UpdatedAt = _current.UpdatedAtDateTime;
            HasPin = _current.HasPin;

            // Load moods
            _primaryMood = _current.PrimaryMood ?? "";
            SecondaryMoods.Clear();
            if (!string.IsNullOrWhiteSpace(_current.SecondaryMoodsCsv))
            {
                foreach (var m in _current.SecondaryMoodsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Take(2))
                    SecondaryMoods.Add(m.Trim());
            }

            // Load category (fallback to Positive if empty)
            SelectedPrimaryCategory = string.IsNullOrWhiteSpace(_current.PrimaryCategory) ? "Positive" : _current.PrimaryCategory!;
            SelectedSecondaryCategory = SelectedPrimaryCategory;

            // Load tags
            SelectedTags.Clear();
            if (!string.IsNullOrWhiteSpace(_current.TagsCsv))
            {
                foreach (var t in _current.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = (t ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned))
                        SelectedTags.Add(cleaned);
                }
            }

            if (_current.HasPin && !PinUnlock.IsUnlocked(_current.DateKey))
            {
                IsLocked = true;
                ShowUnlockModal = true;

                _lockedContent = _current.Content ?? "";
                _lockedTitle = _current.Title ?? "";

                Content = "";
                CurrentTitle = _lockedTitle;
                TitleInput = _lockedTitle;
                CharacterCount = 0;

                if (_dotNetRef is not null)
                    await JS.InvokeVoidAsync("setQuillHtml", "");

                Status = "Locked.";
                return;
            }

            IsLocked = false;
            ShowUnlockModal = false;

            Content = _current.Content ?? "";
            CurrentTitle = _current.Title ?? "";
            TitleInput = CurrentTitle;

            if (_dotNetRef is not null)
                await JS.InvokeVoidAsync("setQuillHtml", Content);

            Status = "Loaded.";
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
    private void OnUnlockOverlayClick() { }

    private void CloseUnlockModal()
    {
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

        IsLocked = false;
        ShowUnlockModal = false;

        Content = _lockedContent;
        CurrentTitle = _lockedTitle;
        TitleInput = _lockedTitle;

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
            TitleInput = "";
            CreatedAt = null;
            UpdatedAt = null;
            CharacterCount = 0;

            HasPin = false;
            PinInput = "";
            PinError = "";

            _primaryMood = "";
            SecondaryMoods.Clear();
            MoodError = "";

            SelectedTags.Clear();

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
    // Save flow
    // ---------------------------
    private async Task StartSave()
    {
        if (IsLocked) return;

        await LoadCustomTagsAsync(); // for available custom tags list

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
            Status = "Please enter a title before saving.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PrimaryMood))
        {
            Status = "Please select a primary mood.";
            return;
        }

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
            var tagsList = SelectedTags.OrderBy(x => x).ToList();

            await Db.SaveAsync(
                date: SelectedDate,
                title: title,
                content: Content,
                hasPin: hasPin,
                pin: pinToSave,
                primaryMood: PrimaryMood,
                secondaryMoods: secondaryMoodsList,
                primaryCategory: SelectedPrimaryCategory,
                tags: tagsList
            );

            _current = await Db.GetByDateAsync(SelectedDate);

            CurrentTitle = _current?.Title ?? title;
            Content = _current?.Content ?? Content;
            CreatedAt = _current?.CreatedAtDateTime;
            UpdatedAt = _current?.UpdatedAtDateTime;
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
            TitleInput = "";

            CreatedAt = null;
            UpdatedAt = null;
            CharacterCount = 0;

            HasPin = false;
            PinInput = "";
            PinError = "";

            _primaryMood = "";
            SecondaryMoods.Clear();
            MoodError = "";

            SelectedTags.Clear();

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

    private async Task HandleTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await ConfirmSave();
        else if (e.Key == "Escape")
            CloseTitleModal();
    }

    // ---------------------------
    // PIN helpers
    // ---------------------------
    private void TogglePinMask() => PinMasked = !PinMasked;

    private void ClearPin()
    {
        PinInput = "";
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

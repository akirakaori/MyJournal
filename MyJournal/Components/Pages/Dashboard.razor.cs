using Microsoft.AspNetCore.Components;
using MudBlazor;
using JournalMaui.Services;
using JournalMaui.Models;
using MyJournal.Services;
using System.Text.RegularExpressions;

namespace MyJournal.Components.Pages;

public partial class Dashboard : ComponentBase
{
    [Inject] private JournalDatabases Db { get; set; } = default!;
    [Inject] private DashboardState State { get; set; } = default!;

    private bool _loading = true;

    private DateRange _range = default!;
    private DateRange _tempRange = default!;

    private bool _pickerOpen;

    private int _positiveCount;
    private int _neutralCount;
    private int _negativeCount;
    private int _total;

    private int _positivePct;
    private int _neutralPct;
    private int _negativePct;

    private string _mostFrequentMood = "";
    private int _mostFrequentMoodCount;
    private int _mostFrequentMoodPct;

    private int _moodTotal;
    private List<MoodBar> _topMoods = new();

    private int _maxTagCount;
    private List<TagStat> _topTags = new();
    private List<TagStat> _topTagsByEntries = new();

    // ✅ Pie chart inputs (MudChart uses InputData/InputLabels)
    private double[] TopTagsPieData => _topTags.Take(5).Select(t => (double)t.Count).ToArray();
    private string[] TopTagsPieLabels => _topTags.Take(5).Select(t => t.Name).ToArray();

    private string RangeLabel =>
        _range.Start.HasValue && _range.End.HasValue
            ? $"{_range.Start.Value:dd MMM yyyy} → {_range.End.Value:dd MMM yyyy}"
            : "No range selected";

    // ============================
    // ✅ NEW: Word Count Trends
    // ============================
    private bool _wcLoading = true;
    private List<int> _wcYears = new();
    private int _wcYear = DateTime.Today.Year;

    private readonly string[] _wcLabels = new[]
    {
        "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"
    };

    private List<ChartSeries> _wcSeries = new()
    {
        new ChartSeries { Name = "Avg words", Data = new double[12] }
    };

    private sealed class MoodBar
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public int Percent { get; set; }
    }

    private sealed class TagStat
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public int Percent { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        _range = State.SelectedRange ?? new DateRange(DateTime.Today.AddDays(-30), DateTime.Today);
        _tempRange = new DateRange(_range.Start, _range.End);

        await LoadAsync();          // existing range analytics
        await InitWordCountAsync(); // ✅ new independent year analytics
    }

    private void TogglePicker()
    {
        _tempRange = new DateRange(_range.Start, _range.End);
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

        _range = new DateRange(_tempRange.Start.Value.Date, _tempRange.End.Value.Date);
        State.SelectedRange = _range;

        _pickerOpen = false;
        await LoadAsync();
    }

    // ============================
    // Existing loader (range-based)
    // ============================
    private async Task LoadAsync()
    {
        _loading = true;
        StateHasChanged();

        if (!_range.Start.HasValue || !_range.End.HasValue)
        {
            Reset();
            return;
        }

        var entries = await Db.GetEntriesByDateRangeAsync(_range.Start.Value, _range.End.Value);

        _positiveCount = 0;
        _neutralCount = 0;
        _negativeCount = 0;

        var moodCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tagMentionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tagEntryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var taggedEntries = 0;

        foreach (var e in entries)
        {
            var category = (e.PrimaryCategory ?? "").Trim();

            if (category.Equals("Positive", StringComparison.OrdinalIgnoreCase))
                _positiveCount++;
            else if (category.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
                _neutralCount++;
            else if (category.Equals("Negative", StringComparison.OrdinalIgnoreCase))
                _negativeCount++;

            var mood = (e.PrimaryMood ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(mood))
            {
                if (moodCounts.TryGetValue(mood, out var c))
                    moodCounts[mood] = c + 1;
                else
                    moodCounts[mood] = 1;
            }

            var tagsCsv = (e.TagsCsv ?? "").Trim();
            if (string.IsNullOrWhiteSpace(tagsCsv))
                continue;

            taggedEntries++;

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = raw.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;

                if (tagMentionCounts.TryGetValue(t, out var mc))
                    tagMentionCounts[t] = mc + 1;
                else
                    tagMentionCounts[t] = 1;

                unique.Add(t);
            }

            foreach (var t in unique)
            {
                if (tagEntryCounts.TryGetValue(t, out var ec))
                    tagEntryCounts[t] = ec + 1;
                else
                    tagEntryCounts[t] = 1;
            }
        }

        _total = _positiveCount + _neutralCount + _negativeCount;

        if (_total == 0)
        {
            Reset();
            return;
        }

        _positivePct = (int)Math.Round(_positiveCount * 100.0 / _total);
        _neutralPct = (int)Math.Round(_neutralCount * 100.0 / _total);
        _negativePct = 100 - _positivePct - _neutralPct;

        _moodTotal = moodCounts.Values.Sum();

        if (_moodTotal == 0)
        {
            _mostFrequentMood = "";
            _mostFrequentMoodCount = 0;
            _mostFrequentMoodPct = 0;
            _topMoods = new();
        }
        else
        {
            var ordered = moodCounts.OrderByDescending(x => x.Value).ToList();
            var top = ordered.First();

            _mostFrequentMood = top.Key;
            _mostFrequentMoodCount = top.Value;
            _mostFrequentMoodPct = (int)Math.Round(_mostFrequentMoodCount * 100.0 / _moodTotal);

            _topMoods = ordered
                .Take(8)
                .Select(x => new MoodBar
                {
                    Name = x.Key,
                    Count = x.Value,
                    Percent = (int)Math.Round(x.Value * 100.0 / _moodTotal)
                })
                .ToList();

            var sumPct = _topMoods.Take(5).Sum(x => x.Percent);
            if (sumPct != 100 && _topMoods.Count > 0)
                _topMoods[0].Percent += (100 - sumPct);
        }

        if (tagMentionCounts.Count == 0)
        {
            _topTags = new();
            _maxTagCount = 0;
        }
        else
        {
            _topTags = tagMentionCounts
                .OrderByDescending(x => x.Value)
                .Select(x => new TagStat { Name = x.Key, Count = x.Value, Percent = 0 })
                .ToList();

            _maxTagCount = _topTags.Max(x => x.Count);
        }

        if (tagEntryCounts.Count == 0 || taggedEntries == 0)
        {
            _topTagsByEntries = new();
        }
        else
        {
            _topTagsByEntries = tagEntryCounts
                .OrderByDescending(x => x.Value)
                .Take(8)
                .Select(x => new TagStat
                {
                    Name = x.Key,
                    Count = x.Value,
                    Percent = (int)Math.Round(x.Value * 100.0 / taggedEntries)
                })
                .ToList();

            var sumPct = _topTagsByEntries.Take(5).Sum(x => x.Percent);
            if (sumPct != 100 && _topTagsByEntries.Count > 0)
                _topTagsByEntries[0].Percent += (100 - sumPct);
        }

        _loading = false;
        StateHasChanged();
    }

    // ============================
    // ✅ NEW: Word Count Trends logic (independent)
    // ============================
    private async Task InitWordCountAsync()
    {
        _wcLoading = true;
        StateHasChanged();

        // we only need dates + content, so load a wide range once (min year -> today).
        // If you have entries older than 10 years, adjust the start year.
        var start = new DateTime(DateTime.Today.Year - 10, 1, 1);
        var end = DateTime.Today;

        var entries = await Db.GetEntriesByDateRangeAsync(start, end);

        _wcYears = entries
            .Select(e => e.DateKey)
            .Select(TryParseDateKey)
            .Where(d => d.HasValue)
            .Select(d => d!.Value.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        if (_wcYears.Count == 0)
            _wcYears = new List<int> { DateTime.Today.Year };

        if (!_wcYears.Contains(_wcYear))
            _wcYear = _wcYears[0];

        await LoadWordCountYearAsync(_wcYear);

        _wcLoading = false;
        StateHasChanged();
    }

    private async Task OnWordCountYearChanged(int year)
    {
        _wcYear = year;
        await LoadWordCountYearAsync(_wcYear);
    }

    private async Task LoadWordCountYearAsync(int year)
    {
        _wcLoading = true;
        StateHasChanged();

        var start = new DateTime(year, 1, 1);
        var end = new DateTime(year, 12, 31);

        var entries = await Db.GetEntriesByDateRangeAsync(start, end);

        var totals = new int[12];
        var counts = new int[12];

        foreach (var e in entries)
        {
            var dt = TryParseDateKey(e.DateKey);
            if (!dt.HasValue) continue;

            var m = dt.Value.Month - 1; // 0..11
            if (m < 0 || m > 11) continue;

            var words = CountWords(e.Content ?? "");
            totals[m] += words;
            counts[m] += 1;
        }

        var avg = new double[12];
        for (var i = 0; i < 12; i++)
            avg[i] = counts[i] == 0 ? 0 : Math.Round((double)totals[i] / counts[i], 0);

        _wcSeries = new List<ChartSeries>
        {
            new ChartSeries { Name = "Avg words", Data = avg }
        };

        _wcLoading = false;
        StateHasChanged();
    }

    private static DateTime? TryParseDateKey(string? dateKey)
    {
        if (string.IsNullOrWhiteSpace(dateKey)) return null;
        if (DateTime.TryParse(dateKey, out var dt)) return dt.Date;
        return null;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Regex.Matches(text, @"\b[\p{L}\p{N}']+\b").Count;
    }

    // ============================
    // Existing helpers
    // ============================


    private string SliceColor(int index)
    {
        var palette = new[]
        {
            "#4CAF50",
            "#2196F3",
            "#FFC107",
            "#9C27B0",
            "#FF5722",
            "#00BCD4",
            "#E91E63",
            "#8BC34A",
            "#FF9800",
            "#3F51B5"
        };

        if (index < 0) index = 0;
        return palette[index % palette.Length];
    }

    private void Reset()
    {
        _positiveCount = 0;
        _neutralCount = 0;
        _negativeCount = 0;
        _total = 0;

        _positivePct = 0;
        _neutralPct = 0;
        _negativePct = 0;

        _mostFrequentMood = "";
        _mostFrequentMoodCount = 0;
        _mostFrequentMoodPct = 0;

        _moodTotal = 0;
        _topMoods = new();

        _maxTagCount = 0;
        _topTags = new();
        _topTagsByEntries = new();

        _loading = false;
        StateHasChanged();
    }
}

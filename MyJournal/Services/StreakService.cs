using JournalMaui.Models;

namespace JournalMaui.Services;

public class StreakService
{
    private readonly JournalDatabases _journalDb;

    public StreakService(JournalDatabases journalDb)
    {
        _journalDb = journalDb;
    }

    /// <summary>
    /// Calculate streak statistics based on journal entry dates.
    /// </summary>
    public async Task<StreakResult> CalculateStreaksAsync()
    {
        var allEntries = await GetAllEntriesOrderedByDateAsync();

        if (allEntries.Count == 0)
        {
            return new StreakResult
            {
                CurrentStreak = 0,
                LongestStreak = 0,
                MissedDays = 0
            };
        }

        var entryDates = allEntries
            .Select(e => ParseDateKey(e.DateKey))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        int currentStreak = CalculateCurrentStreak(entryDates);
        int longestStreak = CalculateLongestStreak(entryDates);
        int missedDays = CalculateMissedDays(entryDates);

        return new StreakResult
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            MissedDays = missedDays
        };
    }

    /// <summary>
    /// Get all journal entries ordered by DateKey.
    /// </summary>
    private async Task<List<JournalEntries>> GetAllEntriesOrderedByDateAsync()
    {
        // Updated call: pass moods/tags as null (no filtering)
        var result = await _journalDb.SearchAsync(
            titleContains: null,
            fromDate: null,
            toDate: null,
            moods: null,
            tags: null,
            sortColumn: nameof(JournalEntries.DateKey),
            sortAscending: true,
            page: 1,
            pageSize: 10000
        );

        return result.Items;
    }

    /// <summary>
    /// Calculate current streak: consecutive days with entries up to today.
    /// </summary>
    private int CalculateCurrentStreak(List<DateTime> entryDates)
    {
        if (entryDates.Count == 0)
            return 0;

        var today = DateTime.Today;
        var lastEntry = entryDates[^1];

        // If last entry is not today or yesterday, streak is broken
        if (lastEntry < today.AddDays(-1))
            return 0;

        int streak = 0;
        var checkDate = today;

        for (int i = entryDates.Count - 1; i >= 0; i--)
        {
            var entryDate = entryDates[i];

            if (entryDate == checkDate)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else if (entryDate < checkDate)
            {
                if (streak == 0 && checkDate == today)
                {
                    checkDate = today.AddDays(-1);
                    if (entryDate == checkDate)
                    {
                        streak++;
                        checkDate = checkDate.AddDays(-1);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        return streak;
    }

    /// <summary>
    /// Calculate longest streak: maximum consecutive days with entries in history.
    /// </summary>
    private int CalculateLongestStreak(List<DateTime> entryDates)
    {
        if (entryDates.Count == 0)
            return 0;

        int maxStreak = 1;
        int currentStreak = 1;

        for (int i = 1; i < entryDates.Count; i++)
        {
            var prevDate = entryDates[i - 1];
            var currDate = entryDates[i];

            if (currDate == prevDate.AddDays(1))
            {
                currentStreak++;
                maxStreak = Math.Max(maxStreak, currentStreak);
            }
            else
            {
                currentStreak = 1;
            }
        }

        return maxStreak;
    }

    /// <summary>
    /// Calculate missed days: total days between entries where no journal exists.
    /// </summary>
    private int CalculateMissedDays(List<DateTime> entryDates)
    {
        if (entryDates.Count <= 1)
            return 0;

        int missedDays = 0;

        for (int i = 1; i < entryDates.Count; i++)
        {
            var prevDate = entryDates[i - 1];
            var currDate = entryDates[i];

            int daysBetween = (int)(currDate - prevDate).TotalDays - 1;
            if (daysBetween > 0)
                missedDays += daysBetween;
        }

        return missedDays;
    }

    /// <summary>
    /// Parse DateKey (yyyy-MM-dd) to DateTime.
    /// </summary>
    private DateTime ParseDateKey(string dateKey)
    {
        return DateTime.ParseExact(dateKey, "yyyy-MM-dd", null);
    }
}

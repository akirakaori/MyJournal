using SQLite;
using JournalMaui.Models;

namespace JournalMaui.Services;

public class CalendarDb
{
    private readonly SQLiteAsyncConnection _db;

    public CalendarDb(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitAsync()
    {
        await _db.CreateTableAsync<CalendarEvents>();
    }

    // Helper: DateTime <-> ISO "o"
    private static string ToIso(DateTime dt) => dt.ToString("o");

    private static DateTime FromIso(string iso)
    {
        // ISO "o" parses reliably
        return DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public async Task<CalendarEvents?> GetByIdAsync(string id)
    {
        return await _db.Table<CalendarEvents>()
                  .Where(x => x.Id == id)
                  .FirstOrDefaultAsync();
    }

    public async Task<List<CalendarEvents>> GetAllAsync()
    {
        return await _db.Table<CalendarEvents>()
                        .OrderBy(x => x.StartIso)
                        .ToListAsync();
    }

    // Useful for "month view" performance
    public async Task<List<CalendarEvents>> GetRangeAsync(DateTime fromInclusive, DateTime toExclusive)
    {
        var fromIso = ToIso(fromInclusive);
        var toIso = ToIso(toExclusive);

        // StartIso in [from, to)
        return await _db.Table<CalendarEvents>()
                        .Where(x => x.StartIso.CompareTo(fromIso) >= 0 && x.StartIso.CompareTo(toIso) < 0)
                        .OrderBy(x => x.StartIso)
                        .ToListAsync();
    }

    // Upsert by Id (same style as your SaveAsync)
    public async Task SaveAsync(
        string? id,
        string title,
        DateTime start,
        DateTime? end,
        bool allDay,
        string? notes)
    {
        var now = DateTime.UtcNow; // Use UTC for consistent timestamp storage

        title = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var safeId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;

        // Convert HTML notes to plain text before saving
        var plainTextNotes = string.IsNullOrWhiteSpace(notes) ? null : TextSanitizer.ConvertHtmlToPlainText(notes);

        var existing = await _db.Table<CalendarEvents>()
                                .Where(x => x.Id == safeId)
                                .FirstOrDefaultAsync();

        if (existing is null)
        {
            var ev = new CalendarEvents
            {
                Id = safeId,
                Title = title,
                StartIso = ToIso(start),
                EndIso = end is null ? null : ToIso(end.Value),
                AllDay = allDay,
                Notes = plainTextNotes,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _db.InsertAsync(ev);
        }
        else
        {
            existing.Title = title;
            existing.StartIso = ToIso(start);
            existing.EndIso = end is null ? null : ToIso(end.Value);
            existing.AllDay = allDay;
            existing.Notes = plainTextNotes;
            existing.UpdatedAt = now;

            await _db.UpdateAsync(existing);
        }
    }

    public Task<int> DeleteAsync(string id)
    {
        return _db.Table<CalendarEvents>()
                  .DeleteAsync(x => x.Id == id);
    }
}

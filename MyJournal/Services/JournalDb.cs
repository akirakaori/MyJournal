using SQLite;
using JournalMaui.Models;

namespace JournalMaui.Services;

public class JournalDatabases
{
    private readonly SQLiteAsyncConnection _db;

    public JournalDatabases(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitAsync()
    {
        await _db.CreateTableAsync<JournalEntries>();
    }

    private static string Key(DateTime date) => date.Date.ToString("yyyy-MM-dd");

    // One-per-day: load by DateKey only
    public Task<JournalEntries?> GetByDateAsync(DateTime date)
    {
        var k = Key(date);
        return _db.Table<JournalEntries>()
                  .Where(x => x.DateKey == k)
                  .FirstOrDefaultAsync();
    }

    // One-per-day: save/upsert by DateKey only (renaming title updates same row)
    public async Task SaveAsync(DateTime date, string title, string content)
    {
        var k = Key(date);
        var now = DateTime.Now;

        title = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var existing = await _db.Table<JournalEntries>()
                                .Where(x => x.DateKey == k)
                                .FirstOrDefaultAsync();

        if (existing is null)
        {
            var entry = new JournalEntries
            {
                DateKey = k,
                Title = title,
                Content = content ?? "",
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.InsertAsync(entry);
        }
        else
        {
            // Update SAME row (no new row)
            existing.Title = title;
            existing.Content = content ?? "";
            existing.UpdatedAt = now;
            await _db.UpdateAsync(existing);
        }
    }

    // One-per-day: delete by DateKey only
    public Task<int> DeleteAsync(DateTime date)
    {
        var k = Key(date);
        return _db.Table<JournalEntries>()
                  .DeleteAsync(x => x.DateKey == k);
    }

    public async Task<List<JournalEntries>> GetRecentAsync(int take = 20)
    {
        return await _db.Table<JournalEntries>()
                        .OrderByDescending(x => x.UpdatedAt)
                        .Take(take)
                        .ToListAsync();
    }
}

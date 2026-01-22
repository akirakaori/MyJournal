using SQLite;
using JournalMaui.Models;
using System.Text;

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

    public Task<JournalEntries?> GetByDateAsync(DateTime date)
    {
        var k = Key(date);
        return _db.Table<JournalEntries>()
                  .Where(x => x.DateKey == k)
                  .FirstOrDefaultAsync();
    }

    public async Task SaveAsync(DateTime date, string title, string content, bool hasPin, string? pin)
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
                HasPin = hasPin,
                Pin = hasPin ? pin : null,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _db.InsertAsync(entry);
        }
        else
        {
            existing.Title = title;
            existing.Content = content ?? "";
            existing.HasPin = hasPin;
            existing.Pin = hasPin ? pin : null;
            existing.UpdatedAt = now;

            await _db.UpdateAsync(existing);
        }
    }

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

    // ============================================================
    // ✅ Backend filtering + date range + sorting + paging (NO LINQ Coalesce)
    // ============================================================

    public class JournalSearchResult
    {
        public List<JournalEntries> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public async Task<JournalSearchResult> SearchAsync(
        string? titleContains,
        DateTime? fromDate,
        DateTime? toDate,
        string sortColumn,
        bool sortAscending,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        // DateKey is yyyy-MM-dd, so lexicographic compare works for ranges.
        string? fromKey = fromDate?.Date.ToString("yyyy-MM-dd");
        string? toKey = toDate?.Date.ToString("yyyy-MM-dd");

        // Whitelist sorting columns to prevent SQL injection via ORDER BY
        var sortColSql = sortColumn switch
        {
            nameof(JournalEntries.Title) => "Title",
            nameof(JournalEntries.UpdatedAt) => "UpdatedAt",
            nameof(JournalEntries.CreatedAt) => "CreatedAt",
            _ => "DateKey" // default
        };

        var sortDirSql = sortAscending ? "ASC" : "DESC";

        var where = new StringBuilder(" WHERE 1=1 ");
        var args = new List<object>();

        // Title filter (NULL-safe): if Title is NULL, it just won't match LIKE anyway
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            where.Append(" AND Title LIKE ? ");
            args.Add("%" + titleContains.Trim() + "%");
        }

        // Date range filter
        if (!string.IsNullOrWhiteSpace(fromKey))
        {
            where.Append(" AND DateKey >= ? ");
            args.Add(fromKey!);
        }

        if (!string.IsNullOrWhiteSpace(toKey))
        {
            where.Append(" AND DateKey <= ? ");
            args.Add(toKey!);
        }

        // Total count
        var countSql = "SELECT COUNT(*) FROM JournalEntries" + where;
        var total = await _db.ExecuteScalarAsync<int>(countSql, args.ToArray());

        // Paged items
        int offset = (page - 1) * pageSize;

        var selectSql =
            "SELECT * FROM JournalEntries" +
            where +
            $" ORDER BY {sortColSql} {sortDirSql} " +
            " LIMIT ? OFFSET ?";

        var selectArgs = new List<object>(args)
        {
            pageSize,
            offset
        };

        var items = await _db.QueryAsync<JournalEntries>(selectSql, selectArgs.ToArray());

        // Optional: hide content for locked entries so list NEVER leaks content
        foreach (var it in items)
        {
            if (it.HasPin)
                it.Content = "";
        }

        return new JournalSearchResult
        {
            TotalCount = total,
            Items = items
        };
    }
}

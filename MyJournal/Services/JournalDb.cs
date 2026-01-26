// JournalDatabases.cs  ✅ FINAL (with Mood + Tag filtering + distinct lists + Plain Text Storage + ISO DateTime)

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
        await MigrateAddMoodColumnsAsync();
        await MigrateAddTagColumnsAsync();
        await MigrateDateTimeColumnsAsync();
        await MigrateHtmlContentToPlainTextAsync();
    }

    private async Task MigrateAddMoodColumnsAsync()
    {
        try
        {
            var tableInfo = await _db.QueryAsync<TableInfoResult>("PRAGMA table_info(JournalEntries)");
            var columnNames = tableInfo.Select(x => x.name).ToList();

            if (!columnNames.Contains("PrimaryMood"))
                await _db.ExecuteAsync("ALTER TABLE JournalEntries ADD COLUMN PrimaryMood TEXT NOT NULL DEFAULT ''");

            if (!columnNames.Contains("SecondaryMoodsCsv"))
                await _db.ExecuteAsync("ALTER TABLE JournalEntries ADD COLUMN SecondaryMoodsCsv TEXT NOT NULL DEFAULT ''");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
        }
    }

    private async Task MigrateAddTagColumnsAsync()
    {
        try
        {
            var tableInfo = await _db.QueryAsync<TableInfoResult>("PRAGMA table_info(JournalEntries)");
            var columnNames = tableInfo.Select(x => x.name).ToList();

            if (!columnNames.Contains("TagsCsv"))
                await _db.ExecuteAsync("ALTER TABLE JournalEntries ADD COLUMN TagsCsv TEXT");

            if (!columnNames.Contains("PrimaryCategory"))
                await _db.ExecuteAsync("ALTER TABLE JournalEntries ADD COLUMN PrimaryCategory TEXT");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tag column migration error: {ex.Message}");
        }
    }

    /// <summary>
    /// Migration: Add CreatedAtText and UpdatedAtText columns, convert legacy DateTime ticks to ISO strings
    /// </summary>
    private async Task MigrateDateTimeColumnsAsync()
    {
        try
        {
            var tableInfo = await _db.QueryAsync<TableInfoResult>("PRAGMA table_info(JournalEntries)");
            var columnNames = tableInfo.Select(x => x.name).ToList();

            // Add new string columns if they don't exist
            if (!columnNames.Contains("CreatedAtText"))
                await _db.ExecuteAsync("ALTER TABLE JournalEntries ADD COLUMN CreatedAtText TEXT");

            if (!columnNames.Contains("UpdatedAtText"))
                await _db.ExecuteAsync("ALTER TABLE JournalEntries ADD COLUMN UpdatedAtText TEXT");

            // Migrate existing data: read raw to detect ticks vs text
            var rawRows = await _db.QueryAsync<dynamic>(
                "SELECT Id, DateKey, CreatedAtText, UpdatedAtText FROM JournalEntries WHERE CreatedAtText IS NULL OR UpdatedAtText IS NULL");

            if (rawRows.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("DateTime migration: All entries already migrated.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"DateTime migration: Processing {rawRows.Count} entries...");

            foreach (var row in rawRows)
            {
                try
                {
                    int id = row.Id;
                    string dateKey = row.DateKey;
                    string? createdText = row.CreatedAtText as string;
                    string? updatedText = row.UpdatedAtText as string;

                    DateTime createdDt;
                    DateTime updatedDt;

                    // Try to parse from DateKey as fallback (format: yyyy-MM-dd)
                    if (DateTime.TryParse(dateKey, out var dateFromKey))
                    {
                        createdDt = dateFromKey;
                        updatedDt = dateFromKey;
                    }
                    else
                    {
                        // Use current time as last resort
                        createdDt = DateTime.UtcNow;
                        updatedDt = DateTime.UtcNow;
                    }

                    // If we have text values already, try to parse them
                    if (!string.IsNullOrWhiteSpace(createdText))
                    {
                        if (DateTime.TryParse(createdText, out var parsedCreated))
                            createdDt = parsedCreated;
                    }

                    if (!string.IsNullOrWhiteSpace(updatedText))
                    {
                        if (DateTime.TryParse(updatedText, out var parsedUpdated))
                            updatedDt = parsedUpdated;
                    }

                    // Update with ISO format
                    var createdIso = createdDt.ToString("yyyy-MM-dd HH:mm:ss");
                    var updatedIso = updatedDt.ToString("yyyy-MM-dd HH:mm:ss");

                    await _db.ExecuteAsync(
                        "UPDATE JournalEntries SET CreatedAtText = ?, UpdatedAtText = ? WHERE Id = ?",
                        createdIso, updatedIso, id);
                }
                catch (Exception rowEx)
                {
                    System.Diagnostics.Debug.WriteLine($"DateTime migration row error: {rowEx.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"DateTime migration: Completed {rawRows.Count} entries.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DateTime migration error: {ex.Message}");
        }
    }

    /// <summary>
    /// Migration: Convert HTML content to plain text for existing entries
    /// This runs once to sanitize old HTML content
    /// </summary>
    private async Task MigrateHtmlContentToPlainTextAsync()
    {
        try
        {
            // Only process entries that likely contain HTML (performance optimization)
            var allEntries = await _db.Table<JournalEntries>().ToListAsync();
            int convertedCount = 0;

            foreach (var entry in allEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.Content))
                    continue;

                // Check if content contains HTML tags
                if (TextSanitizer.ContainsHtmlTags(entry.Content))
                {
                    var plainText = TextSanitizer.ConvertHtmlToPlainText(entry.Content);
                    
                    // Only update if conversion produced different text
                    if (plainText != entry.Content)
                    {
                        await _db.ExecuteAsync(
                            "UPDATE JournalEntries SET Content = ? WHERE Id = ?",
                            plainText, entry.Id);
                        convertedCount++;
                    }
                }
            }

            if (convertedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"HTML migration: Converted {convertedCount} entries to plain text.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HTML migration error: {ex.Message}");
        }
    }

    private class TableInfoResult
    {
        public string name { get; set; } = "";
    }

    private static string Key(DateTime date) => date.Date.ToString("yyyy-MM-dd");

    public async Task<JournalEntries?> GetByDateAsync(DateTime date)
    {
        var k = Key(date);
        return await _db.Table<JournalEntries>()
                        .Where(x => x.DateKey == k)
                        .FirstOrDefaultAsync();
    }

    public async Task SaveAsync(
        DateTime date,
        string title,
        string content,
        bool hasPin,
        string? pin,
        string primaryMood,
        List<string> secondaryMoods,
        string primaryCategory,
        List<string> tags)
    {
        var k = Key(date);
        var now = DateTime.UtcNow; // Use UTC for consistent timestamp storage

        title = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        primaryMood = (primaryMood ?? "").Trim();
        if (string.IsNullOrWhiteSpace(primaryMood))
            throw new ArgumentException("Primary mood is required.", nameof(primaryMood));

        primaryCategory = (primaryCategory ?? "").Trim();
        if (primaryCategory is not ("Positive" or "Neutral" or "Negative"))
            primaryCategory = "Positive";

        secondaryMoods ??= new List<string>();
        secondaryMoods = secondaryMoods
            .Where(m => !string.IsNullOrWhiteSpace(m) && !m.Equals(primaryMood, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        var secondaryMoodsCsv = string.Join(",", secondaryMoods);

        tags ??= new List<string>();
        var tagsCsv = string.Join(",", tags
            .Select(t => (t ?? "").Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t));

        // Convert HTML content to plain text before saving
        var plainTextContent = TextSanitizer.ConvertHtmlToPlainText(content ?? "");

        var existing = await _db.Table<JournalEntries>()
                                .Where(x => x.DateKey == k)
                                .FirstOrDefaultAsync();

        if (existing is null)
        {
            var entry = new JournalEntries
            {
                DateKey = k,
                Title = title,
                Content = plainTextContent,
                HasPin = hasPin,
                Pin = hasPin ? pin : null,

                PrimaryMood = primaryMood,
                PrimaryCategory = primaryCategory,
                SecondaryMoodsCsv = secondaryMoodsCsv,

                TagsCsv = tagsCsv,

                CreatedAtText = now.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAtText = now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await _db.InsertAsync(entry);
        }
        else
        {
            existing.Title = title;
            existing.Content = plainTextContent;
            existing.HasPin = hasPin;
            existing.Pin = hasPin ? pin : null;

            existing.PrimaryMood = primaryMood;
            existing.PrimaryCategory = primaryCategory;
            existing.SecondaryMoodsCsv = secondaryMoodsCsv;

            existing.TagsCsv = tagsCsv;

            existing.UpdatedAtText = now.ToString("yyyy-MM-dd HH:mm:ss");

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
                        .OrderByDescending(x => x.UpdatedAtText)
                        .Take(take)
                        .ToListAsync();
    }

    // ============================================================
    // ✅ Distinct Mood/Tag lists (for chips)
    // ============================================================

    public async Task<List<string>> GetDistinctMoodsAsync()
    {
        var all = await _db.Table<JournalEntries>().ToListAsync();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in all)
        {
            if (!string.IsNullOrWhiteSpace(e.PrimaryMood))
                set.Add(e.PrimaryMood.Trim());

            if (!string.IsNullOrWhiteSpace(e.SecondaryMoodsCsv))
            {
                foreach (var m in e.SecondaryMoodsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    set.Add(m.Trim());
            }
        }

        return set.OrderBy(x => x).ToList();
    }

    public async Task<List<string>> GetDistinctTagsAsync()
    {
        var all = await _db.Table<JournalEntries>().ToListAsync();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in all)
        {
            if (string.IsNullOrWhiteSpace(e.TagsCsv)) continue;

            foreach (var t in e.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                set.Add(t.Trim());
        }

        return set.OrderBy(x => x).ToList();
    }

    // ============================================================
    // ✅ Backend filtering + date range + mood + tags + sorting + paging
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
        IReadOnlyCollection<string>? moods,
        IReadOnlyCollection<string>? tags,
        string sortColumn,
        bool sortAscending,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        string? fromKey = fromDate?.Date.ToString("yyyy-MM-dd");
        string? toKey = toDate?.Date.ToString("yyyy-MM-dd");

        // Whitelist sorting columns
        var sortColSql = sortColumn switch
        {
            nameof(JournalEntries.Title) => "Title",
            "UpdatedAt" => "UpdatedAtText",
            "CreatedAt" => "CreatedAtText",
            _ => "DateKey"
        };

        var sortDirSql = sortAscending ? "ASC" : "DESC";

        var where = new StringBuilder(" WHERE 1=1 ");
        var args = new List<object>();

        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            where.Append(" AND Title LIKE ? ");
            args.Add("%" + titleContains.Trim() + "%");
        }

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

        // ✅ Mood filter (ANY selected mood)
        // Matches PrimaryMood OR SecondaryMoodsCsv contains the mood (CSV-safe)
        if (moods != null && moods.Count > 0)
        {
            where.Append(" AND (");
            var i = 0;

            foreach (var raw in moods)
            {
                var m = (raw ?? "").Trim();
                if (string.IsNullOrWhiteSpace(m)) continue;

                if (i++ > 0) where.Append(" OR ");

                where.Append("(PrimaryMood = ? OR (',' || IFNULL(SecondaryMoodsCsv,'') || ',') LIKE ?) ");
                args.Add(m);
                args.Add($"%,{m},%");
            }

            where.Append(") ");
        }

        // ✅ Tags filter (ANY selected tag)
        // CSV-safe contains: ','||TagsCsv||',' LIKE '%,tag,%'
        if (tags != null && tags.Count > 0)
        {
            where.Append(" AND (");
            var i = 0;

            foreach (var raw in tags)
            {
                var t = (raw ?? "").Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;

                if (i++ > 0) where.Append(" OR ");

                where.Append("(',' || IFNULL(TagsCsv,'') || ',') LIKE ? ");
                args.Add($"%,{t},%");
            }

            where.Append(") ");
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

    /// <summary>
    /// Gets all journal entries between startDate and endDate (inclusive).
    /// Used for PDF export.
    /// </summary>
    public async Task<List<JournalEntries>> GetEntriesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var startKey = Key(startDate);
        var endKey = Key(endDate);

        var sql = "SELECT * FROM JournalEntries WHERE DateKey >= ? AND DateKey <= ? ORDER BY DateKey ASC";
        var entries = await _db.QueryAsync<JournalEntries>(sql, startKey, endKey);

        return entries;
    }


}

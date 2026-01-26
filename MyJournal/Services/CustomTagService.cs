using SQLite;
using MyJournal.Models;

namespace MyJournal.Services
{
    public class CustomTagService
    {
        private readonly SQLiteAsyncConnection _db;

        public CustomTagService(SQLiteAsyncConnection db)
        {
            _db = db;
        }

        // Call this once on startup
        public async Task InitAsync()
        {
            await _db.CreateTableAsync<CustomTag>();

            // Use the actual table name consistently:
            await _db.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomTags_NameNormalized ON CustomTags(NameNormalized);"
            );
        }


        public async Task<List<CustomTag>> GetAllAsync()
        {
            return await _db.Table<CustomTag>()
                            .OrderBy(t => t.Name)
                            .ToListAsync();
        }

        public async Task<bool> AddAsync(string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return false;

            var normalized = Normalize(name);

            // Check duplicate (fast due to index)
            var exists = await _db.Table<CustomTag>()
                                  .Where(t => t.NameNormalized == normalized)
                                  .FirstOrDefaultAsync();

            if (exists != null) return false;

            await _db.InsertAsync(new CustomTag
    {
        Name = name,
        NameNormalized = normalized,
        CreatedAtText = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
    });

            return true;
        }

        public async Task DeleteAsync(int id)
        {
            await _db.DeleteAsync<CustomTag>(id);
        }

        public async Task DeleteByNameAsync(string name)
        {
            var normalized = Normalize(name);
            await _db.ExecuteAsync("DELETE FROM CustomTags WHERE NameNormalized = ?", normalized);
        }

        private static string Normalize(string input)
            => input.Trim().ToLowerInvariant();
    }
}

using SQLite;
using JournalMaui.Models;
using MyJournal.Services;

namespace JournalMaui.Services;

public class ProfileService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly AuthService _authService;

    public ProfileService(SQLiteAsyncConnection db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    public async Task InitAsync()
    {
        await _db.CreateTableAsync<UserProfile>();
    }

    public async Task<UserProfile> GetAsync()
    {
        await InitAsync();
        
        var profile = await _db.Table<UserProfile>().FirstOrDefaultAsync();
        
        if (profile == null)
        {
            profile = new UserProfile
            {
                Id = 1,
                TimeZoneId = TimeZoneInfo.Local.Id,
                ThemePreference = "System",
                Language = "English",
                StreakWarningEnabled = true
            };
            
            await _db.InsertAsync(profile);
        }
        
        return profile;
    }

    public async Task<bool> SaveAsync(UserProfile profile)
    {
        try
        {
            await InitAsync();
            
            profile.Id = 1;
            
            var existing = await _db.Table<UserProfile>().FirstOrDefaultAsync();
            
            if (existing != null)
            {
                await _db.UpdateAsync(profile);
            }
            else
            {
                await _db.InsertAsync(profile);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProfileService.SaveAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GetDisplayNameAsync()
    {
        return await _authService.GetUsernameAsync();
    }

    public async Task<string> GetEmailAsync()
    {
        return await _authService.GetEmailAsync();
    }

    public async Task<DateTime> GetJoinDateAsync()
    {
        return await _authService.GetRegistrationDateAsync();
    }

    public async Task<bool> UpdateUserInfoAsync(string displayName, string email)
    {
        return await _authService.UpdateUserInfoAsync(displayName, email);
    }

    public async Task<int> GetTotalEntriesCountAsync(JournalDatabases journalDb)
    {
        try
        {
            var result = await journalDb.SearchAsync(
                titleContains: null,
                fromDate: null,
                toDate: null,
                moods: null,
                tags: null,
                sortColumn: nameof(JournalEntries.DateKey),
                sortAscending: true,
                page: 1,
                pageSize: 1
            );
            
            return result.TotalCount;
        }
        catch
        {
            return 0;
        }
    }
}

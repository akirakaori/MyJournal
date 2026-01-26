using SQLite;

namespace JournalMaui.Models;

public class UserProfile
{
    [PrimaryKey]
    public int Id { get; set; } = 1;
    
    public string? AvatarBase64 { get; set; }
    
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    
    public string ThemePreference { get; set; } = "System";
    
    public string Language { get; set; } = "English";
    
    public bool AppLockEnabled { get; set; } = false;
    
    public string? DailyReminderTime { get; set; }
    
    public bool StreakWarningEnabled { get; set; } = true;
}

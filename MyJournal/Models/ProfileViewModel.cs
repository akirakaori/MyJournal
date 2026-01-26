namespace JournalMaui.Models;

public class ProfileViewModel
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime JoinDate { get; set; }
    
    public string? AvatarBase64 { get; set; }
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public string ThemePreference { get; set; } = "System";
    public string Language { get; set; } = "English";
    public bool AppLockEnabled { get; set; } = false;
    public string? DailyReminderTime { get; set; }
    public bool StreakWarningEnabled { get; set; } = true;
}

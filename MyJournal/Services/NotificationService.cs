namespace JournalMaui.Services;

public class NotificationService
{
    public Task ScheduleStreakWarningAsync(string timeZoneId, bool enabled)
    {
        if (!enabled)
        {
            return CancelStreakWarningAsync();
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var nowInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var endOfDayInTz = nowInTz.Date.AddDays(1);
            var warningTimeInTz = endOfDayInTz.AddHours(-2);

            if (warningTimeInTz <= nowInTz)
            {
                warningTimeInTz = warningTimeInTz.AddDays(1);
            }

            System.Diagnostics.Debug.WriteLine($"Streak warning scheduled for {warningTimeInTz} in timezone {timeZoneId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NotificationService.ScheduleStreakWarningAsync error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task CancelStreakWarningAsync()
    {
        System.Diagnostics.Debug.WriteLine("Streak warning cancelled");
        return Task.CompletedTask;
    }

    public Task ScheduleDailyReminderAsync(string? time, string timeZoneId, bool enabled)
    {
        if (!enabled || string.IsNullOrEmpty(time))
        {
            return CancelDailyReminderAsync();
        }

        System.Diagnostics.Debug.WriteLine($"Daily reminder scheduled for {time} in timezone {timeZoneId}");
        return Task.CompletedTask;
    }

    public Task CancelDailyReminderAsync()
    {
        System.Diagnostics.Debug.WriteLine("Daily reminder cancelled");
        return Task.CompletedTask;
    }
}

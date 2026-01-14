using SQLite;

namespace JournalMaui.Models;

public class CalendarEvents
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string Title { get; set; } = "";

    // Store in ISO for easy FullCalendar + sorting
    [Indexed]
    public string StartIso { get; set; } = "";   // DateTime.ToString("o")
    public string? EndIso { get; set; }          // nullable
    public bool AllDay { get; set; } = true;

    public string? Notes { get; set; }

    [Indexed]
    public DateTime CreatedAt { get; set; }
    [Indexed]
    public DateTime UpdatedAt { get; set; }
}

using SQLite;

namespace JournalMaui.Models;

public class JournalEntries
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string DateKey { get; set; } = "";

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";

    public bool HasPin { get; set; } = false;
    public string? Pin { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Mood tracking (Feature 3)
    public string PrimaryMood { get; set; } = "";
    public string SecondaryMoodsCsv { get; set; } = "";

    public string? TagsCsv { get; set; }
    public string? PrimaryCategory { get; set; }

}

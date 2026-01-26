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

    // ========================================================================
    // DateTime Storage: Using ISO 8601 format strings (yyyy-MM-dd HH:mm:ss)
    // ========================================================================
    
    /// <summary>
    /// CreatedAt timestamp stored as ISO string (yyyy-MM-dd HH:mm:ss)
    /// Use CreatedAtDateTime property for DateTime access
    /// </summary>
    [Indexed]
    public string? CreatedAtText { get; set; }
    
    /// <summary>
    /// UpdatedAt timestamp stored as ISO string (yyyy-MM-dd HH:mm:ss)
    /// Use UpdatedAtDateTime property for DateTime access
    /// </summary>
    [Indexed]
    public string? UpdatedAtText { get; set; }

    // Legacy columns (kept for migration compatibility, not used after migration)
    [Ignore]
    public DateTime CreatedAt { get; set; }
    
    [Ignore]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Helper property: Get/Set CreatedAt as DateTime
    /// Converts to/from ISO string format
    /// </summary>
    [Ignore]
    public DateTime CreatedAtDateTime
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CreatedAtText))
                return DateTime.MinValue;
            
            if (DateTime.TryParse(CreatedAtText, out var dt))
                return dt;
            
            return DateTime.MinValue;
        }
        set
        {
            CreatedAtText = value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// Helper property: Get/Set UpdatedAt as DateTime
    /// Converts to/from ISO string format
    /// </summary>
    [Ignore]
    public DateTime UpdatedAtDateTime
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UpdatedAtText))
                return DateTime.MinValue;
            
            if (DateTime.TryParse(UpdatedAtText, out var dt))
                return dt;
            
            return DateTime.MinValue;
        }
        set
        {
            UpdatedAtText = value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    // Mood tracking (Feature 3)
    public string PrimaryMood { get; set; } = "";
    public string SecondaryMoodsCsv { get; set; } = "";

    public string? TagsCsv { get; set; }
    public string? PrimaryCategory { get; set; }

}

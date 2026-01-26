using SQLite;

namespace MyJournal.Models
{
    [Table("CustomTags")]
    public class CustomTag
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull]
        public string Name { get; set; } = "";

        [NotNull]
        public string NameNormalized { get; set; } = "";
//  Stored in SQLite as TEXT (ISO-like)
        [Indexed]
        public string? CreatedAtText { get; set; }
        [Ignore]
        public DateTime CreatedAtUtc
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CreatedAtText)) return DateTime.MinValue;

                if (DateTime.TryParse(CreatedAtText, out var dt))
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                return DateTime.MinValue;
            }
            set
            {
                // store UTC consistently
                var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
                CreatedAtText = utc.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }
}

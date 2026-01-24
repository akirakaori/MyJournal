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

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_TimeZones")]
    public class TimeZones
	{
        [Column("id")]
        public int Id { get; set; }

        [Column("value", TypeName = "nvarchar(250)")]
        public string Value { get; set; }

        [Column("lastUpdate", TypeName = "datetime")]
        public DateTime? LastUpdate { get; set; }
    }
}

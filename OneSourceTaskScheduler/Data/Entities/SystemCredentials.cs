using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_SystemCredentials")]
    public class SystemCredentials
    {
        [Column("id")]
        public int Id { get; set; }

        public string SystemLogin { get; set; }

        public string SystemPassword { get; set; }

        [Column("lastUpdate", TypeName = "datetime2")]
        public DateTime? LastUpdate { get; set; }
    }
}

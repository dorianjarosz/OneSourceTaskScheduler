using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_Scripts")]
    public class Scripts
    {
        public int Id { get; set; }
        public string? TaskName { get; set; }
        public string? Script { get; set; }

        [Column("lastUpdate")]
        public DateTime? LastUpdate { get; set; }
    }
}

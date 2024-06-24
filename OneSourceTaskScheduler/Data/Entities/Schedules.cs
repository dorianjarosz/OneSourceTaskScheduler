using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_Schedules")]
    public class Schedules
    {
        [Column("id")]
        public int Id { get; set; }


        [Column("taskName")]

        public string taskName { get; set; }


        [Column("Source")]
        public string Source { get; set; }

        [Column("Start")]
        public string Start { get; set; }

        [Column("Recurrence")]
        public string Recurrence { get; set; }
        [Column("End")]
        public string End { get; set; }
        [Column("Active")]
        public bool? Active { get; set; }
        [Column("LastUpdate")]
        public DateTime? LastUpdate { get; set; }

        public int TaskId { get; set; }

        public Tasks Task { get; set; }
    }
}

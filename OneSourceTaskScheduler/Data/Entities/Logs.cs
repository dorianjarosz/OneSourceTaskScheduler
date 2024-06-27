using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{

    [Table("sys_Schedules_Log")]
    public class Logs
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("taskTitle")]
        public string? taskTitle { get; set; }
        [Column("application")]
        public string? application { get; set; }

        [Column("customer")]
        public string? customer { get; set; }

        [Column("startTime", TypeName = "datetime")]
        public DateTime? startTime { get; set; }

        [Column("endTime", TypeName = "datetime")]
        public DateTime? endTime { get; set; }

        [Column("message")]
        public string? message { get; set; }

        [Column("processedTicketsCount")]
        public string? ProcessedTicketsCount { get; set; }

        [Column("status")]
        public string? status { get; set; }

        [Column("lastUpdate", TypeName = "datetime")]
        public DateTime? LastUpdate { get; set; }

        [Column("exceptionMessage")]
        public string? ExceptionMessage { get; set; }

        [Column("stackTrace")]
        public string? StackTrace { get; set; }
    }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_Configuration")]
    public class Configuration
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("varName")]
        public string Name { get; set; }

        [Column("varValue")]
        public string Value { get; set; }

        [Column("lastUpdate")]
        public DateTime? LastUpdate { get; set; }
    }

}

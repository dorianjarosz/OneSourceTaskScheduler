using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_SNOW_ApiFilters")]
    public class SnowApiFilter
    {
        [Column("id")]
        public int Id { get; set; }

        public string? SnowTableTechnical { get; set; }

        public string? SnowTableLabel { get; set; }

        public string? FilterName { get; set; }

        public string? Query { get; set; }
        public string? CustomerName { get; set; }

        public bool FilterFlag { get; set; }

        [Column("lastUpdate", TypeName = "datetime")]
        public DateTime? LastUpdate { get; set; }
    }
}

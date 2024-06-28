using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_SNOW_ApiColumnConfigurations")]
    public class SNOWApiColumnConfiguration
    {
        [Column("id")]
        public int Id { get; set; }

        public string? ColumnVariableName { get; set; }

        public string? SqlColumnName { get; set; }

        public string? SqlColumnDataType { get; set; }

        public int SnowTableConfigurationId { get; set; }

        [Column("lastUpdate", TypeName ="datetime")]
        public DateTime? LastUpdate { get; set; }

        public SNOWApiTableConfiguration SnowTableConfiguration { get; set; }
    }
}

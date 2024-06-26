using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_SNOW_ApiTableConfigurations")]
    public class SNOWApiTableConfiguration
    {
        [Column("id")]
        public int Id { get; set; }

        public string TableNameVariable { get; set; }

        public string SqlTableName { get; set; }

        public string TechnicalTableName { get; set; }

        public string TableNameLabel { get; set; }

        public bool Param { get; set; }

        [Column("lastUpdate", TypeName = "datetime")]
        public DateTime? LastUpdate { get; set; }

        public ICollection<SNOWApiColumnConfiguration> SnowTableColumnConfigurations { get; set; }
    }
}

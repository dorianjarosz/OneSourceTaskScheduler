using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_Systems")]
    public class Systems
    {
        [Column("id")]
        public int Id { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? CustomerName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? System { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? SystemType { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? RootURL { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? SystemLogin { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? SystemPassword { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? OneSourceLogin { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? OneSourcePassword { get; set; }

        public DateTime? LastUpdate { get; set; }


    }
}

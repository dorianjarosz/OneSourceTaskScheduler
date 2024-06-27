using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneSource.Data.Entities
{
    [Table("sys_Customers")]
    public class Customers
    {
        [Column("id")]
        public int Id { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? CustomerName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? OneSourceURL { get; set; }

        [Column("lastUpdate", TypeName = "datetime2")]
        public DateTime? LastUpdate { get; set; }


    }
}

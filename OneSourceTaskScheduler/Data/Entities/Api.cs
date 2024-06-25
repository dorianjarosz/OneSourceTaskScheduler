using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_Api")]
    public class Api
    {
        [Column("id")]
        public int Id { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string CustomerName { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string System { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string SystemType { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string ApiName { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string ClientId { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string FolderId { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string EndPointPath { get; set; }

        public DateTime? LastUpdate { get; set; }

    }
}

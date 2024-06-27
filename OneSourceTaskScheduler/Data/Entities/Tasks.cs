using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{
    [Table("sys_Tasks")]
    public class Tasks
    {
        [Column("id")]
        public int Id { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? taskName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? CustomerName { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? APIName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? Credential { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? SystemType { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? EnvironmentNames { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? SourceTable { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? DestinationTable { get; set; }


        [Column(TypeName = "nvarchar(100)")]
        public string? FilterName { get; set; }


        [Column(TypeName = "nvarchar(MAX)")]
        public string? FieldsList { get; set; }

        [Column(TypeName = "nvarchar(MAX)")]
        public string? fromDate { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? endDate { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? DatesList { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? DateOperation { get; set; }

        [Column(TypeName = "int")]
        public int? BatchSize { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? additionalQuery { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? FavouriteQuery { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? FolderID { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? ClientID { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? Param { get; set; }

        [Column(TypeName = "int")]
        public int? TimeRange { get; set; }

        [Column(TypeName = "nvarchar(20)")]
        public string? TimeRangeType { get; set; }

        [Column(TypeName = "nvarchar(250)")]
        public string? TimeZone { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? QueryDomain { get; set; }
        [Column(TypeName = "nvarchar(200)")]
        public string? DestinationServiceOption { get; set; }
        [Column(TypeName = "nvarchar(200)")]
        public string? CustomServiceUrl { get; set; }
        [Column(TypeName = "nvarchar(200)")]
        public string? ServiceAPIEndpoint { get; set; }
        [Column(TypeName = "nvarchar(100)")]
        public string? OneSourceLogin { get; set; }
        [Column(TypeName = "nvarchar(100)")]
        public string? OneSourcePassword { get; set; }
        [Column("lastUpdate", TypeName = "datetime2")]
        public DateTime? LastUpdate { get; set; }

        public ICollection<Schedules> Schedules { get; set; }
    }
}

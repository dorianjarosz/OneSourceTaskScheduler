using System.ComponentModel.DataAnnotations.Schema;

namespace OneSourceTaskScheduler.Data.Entities
{

    [Table("sys_users")]
    public class OneSourceUser
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("select_Role")]
        public string Role { get; set; }

        [Column("userLevel")]
        public int? UserLevel { get; set; }

        [Column("imgUrl")]
        public string ImgUrl { get; set; }

        [Column("firstName")]
        public string FirstName { get; set; }

        [Column("lastName")]
        public string LastName { get; set; }

        public string Organization { get; set; }

        public string Position { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("login")]
        public string Login { get; set; }

        [Column("password")]
        public string Password { get; set; }

        [Column("passwordExpirationDate")]
        public DateTime? PasswordExpiryDate { get; set; }

        [Column("failedAuthenticationCount")]
        public int FailedAuthenticationCount { get; set; }

        [Column("token")]
        public Guid? Token { get; set; }

        [Column("address")]
        public string Address { get; set; }

        public string PostalCode { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public string Phone { get; set; }

        [Column("bool_quickSwitch")]
        public bool? QuickSwitch { get; set; }

        [Column("userIP")]
        public string UserIP { get; set; }

        [Column("userAgent")]
        public string UserAgent { get; set; }

        [Column("lastUpdate")]
        public DateTime? LastUpdate { get; set; }

        [Column("select_IndividualPermissions")]
        public string IndividualPermissions { get; set; }

        public string NotificationChannels { get; set; }

        public int ThemeId { get; set; }
    }
}

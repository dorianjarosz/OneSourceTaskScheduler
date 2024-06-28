using Microsoft.EntityFrameworkCore;
using OneSource.Data.Entities;
using OneSourceTaskScheduler.Data.Entities;

namespace OneSourceTaskScheduler.Data
{
    public class OneSourceContext : DbContext
    {
        public OneSourceContext(DbContextOptions<OneSourceContext> options)
            : base(options)
        {
        }

        public DbSet<Api> Apis { get; set; }

        public DbSet<Schedules> Schedules { get; set; }

        public DbSet<Systems> Systems { get; set; }

        public DbSet<Customers> Customers { get; set; }

        public DbSet<Tasks> Tasks { get; set; }

        public DbSet<Logs> Logs { get; set; }

        public DbSet<Scripts> Scripts { get; set; }

        public DbSet<TimeZones> TimeZones { get; set; }

        public DbSet<SNOWApiTableConfiguration> SNOWApiTableConfigurations { get; set; }

        public DbSet<SNOWApiColumnConfiguration> SNOWApiColumnConfigurations { get; set; }

        public DbSet<Configuration> Configurations { get; set; }

        public DbSet<SystemCredentials> SystemCredentials { get; set; }

        public DbSet<SnowApiFilter> SnowApiFilters { get; set; }

        public DbSet<OneSourceUser> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<SNOWApiTableConfiguration>()
                .Property(b => b.LastUpdate)
                .HasDefaultValueSql("getdate()");

            builder.Entity<SNOWApiColumnConfiguration>()
               .Property(b => b.LastUpdate)
               .HasDefaultValueSql("getdate()");
        }
    }
}

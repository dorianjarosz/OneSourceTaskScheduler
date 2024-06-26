using Microsoft.EntityFrameworkCore;
using OneSourceTaskScheduler.Data.Entities;

namespace OneSourceTaskScheduler.Data
{
    public class OneSourceContext : DbContext
    {
        public OneSourceContext(DbContextOptions<OneSourceContext> options)
            : base(options)
        {
        }

        public DbSet<Schedules> Schedules { get; set; }

        public DbSet<Systems> Systems { get; set; }

        public DbSet<Tasks> Tasks { get; set; }

        public DbSet<Logs> Logs { get; set; }

        public DbSet<Scripts> Scripts { get; set; }

        public DbSet<TimeZones> TimeZones { get; set; }

        public DbSet<SNOWApiTableConfiguration> SNOWApiTableConfigurations { get; set; }

        public DbSet<SNOWApiColumnConfiguration> SNOWApiColumnConfigurations { get; set; }

        public DbSet<Configuration> Configurations { get; set; }

        public DbSet<SystemCredentials> SystemCredentials { get; set; }

        public DbSet<SnowApiFilter> SnowApiFilters { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
        }
    }
}

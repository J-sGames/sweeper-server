using Microsoft.EntityFrameworkCore;
using SweeperServer.Models;

namespace SweeperServer.Data
{
    public class SweeperDbContext : DbContext
    {
        public SweeperDbContext(DbContextOptions<SweeperDbContext> options)
            : base(options)
        {
        }

        public DbSet<PlayLog> PlayLogs => Set<PlayLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PlayLog>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .ValueGeneratedOnAdd();
            });
        }
    }
}
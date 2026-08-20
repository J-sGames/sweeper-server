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
        public DbSet<User> Users => Set<User>();
        public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
        public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PlayLog>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Nickname).HasMaxLength(20).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(320);
                entity.HasIndex(x => x.Nickname).IsUnique();
            });

            modelBuilder.Entity<UserCredential>(entity =>
            {
                entity.HasKey(x => x.UserId);
                entity.Property(x => x.LoginId).HasMaxLength(30).IsRequired();
                entity.Property(x => x.NormalizedLoginId).HasMaxLength(30).IsRequired();
                entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
                entity.HasIndex(x => x.NormalizedLoginId).IsUnique();
                entity.HasOne(x => x.User).WithOne(x => x.Credential)
                    .HasForeignKey<UserCredential>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ExternalLogin>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Provider).HasMaxLength(30).IsRequired();
                entity.Property(x => x.ProviderUserId).HasMaxLength(255).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(320);
                entity.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
                entity.HasOne(x => x.User).WithMany(x => x.ExternalLogins)
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
                entity.HasIndex(x => x.TokenHash).IsUnique();
                entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens)
                    .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

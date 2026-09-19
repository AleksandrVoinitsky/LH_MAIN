using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LH.Main.Backend.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.NormalizedUsername).HasMaxLength(64).IsRequired();
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.Property(user => user.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<PasswordCredential>(entity =>
        {
            entity.ToTable("password_credentials");
            entity.HasKey(credential => credential.UserId);
            entity.Property(credential => credential.PasswordHash).IsRequired();
            entity.HasOne(credential => credential.User)
                .WithOne(user => user.PasswordCredential)
                .HasForeignKey<PasswordCredential>(credential => credential.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("player_profiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.Username).HasMaxLength(64).IsRequired();
            entity.Property(profile => profile.CreatedAtUtc).IsRequired();
            entity.HasOne(profile => profile.User)
                .WithOne(user => user.Profile)
                .HasForeignKey<PlayerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

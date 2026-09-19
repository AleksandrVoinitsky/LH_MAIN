using LH.Main.Backend.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LH.Main.Backend.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    public DbSet<GameServerSlot> GameServerSlots => Set<GameServerSlot>();

    public DbSet<MatchQueueEntry> MatchQueueEntries => Set<MatchQueueEntry>();

    public DbSet<MatchSession> Matches => Set<MatchSession>();

    public DbSet<MatchTicket> MatchTickets => Set<MatchTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(64).IsRequired();
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.Property(user => user.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        });

        modelBuilder.Entity<PasswordCredential>(entity =>
        {
            entity.ToTable("password_credentials");
            entity.HasKey(credential => credential.UserId);
            entity.Property(credential => credential.UserId).HasColumnName("user_id");
            entity.Property(credential => credential.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.HasOne(credential => credential.User)
                .WithOne(user => user.PasswordCredential)
                .HasForeignKey<PasswordCredential>(credential => credential.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("player_profiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.UserId).HasColumnName("user_id");
            entity.Property(profile => profile.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
            entity.Property(profile => profile.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.HasOne(profile => profile.User)
                .WithOne(user => user.Profile)
                .HasForeignKey<PlayerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameServerSlot>(entity =>
        {
            entity.ToTable("game_server_slots");
            entity.HasKey(slot => slot.ServerId).HasName("pk_game_server_slots");
            entity.Property(slot => slot.ServerId).HasColumnName("server_id").HasMaxLength(128);
            entity.Property(slot => slot.PublicHost).HasColumnName("public_host").HasMaxLength(255).IsRequired();
            entity.Property(slot => slot.PublicPort).HasColumnName("public_port").IsRequired();
            entity.Property(slot => slot.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(slot => slot.CurrentMatchId).HasColumnName("current_match_id");
            entity.Property(slot => slot.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(slot => slot.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.HasIndex(slot => slot.Status).HasDatabaseName("ix_game_server_slots_status");
        });

        modelBuilder.Entity<MatchQueueEntry>(entity =>
        {
            entity.ToTable("match_queue_entries");
            entity.HasKey(entry => entry.Id).HasName("pk_match_queue_entries");
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.PlayerId).HasColumnName("player_id").IsRequired();
            entity.Property(entry => entry.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(entry => entry.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(entry => entry.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(entry => entry.AssignedMatchId).HasColumnName("assigned_match_id");
            entity.HasIndex(entry => entry.AssignedMatchId).HasDatabaseName("ix_match_queue_entries_assigned_match_id");
            entity.HasIndex(entry => new { entry.PlayerId, entry.Status }).HasDatabaseName("ix_match_queue_entries_player_id_status");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(entry => entry.PlayerId)
                .HasConstraintName("fk_match_queue_entries_users_player_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MatchSession>()
                .WithMany()
                .HasForeignKey(entry => entry.AssignedMatchId)
                .HasConstraintName("fk_match_queue_entries_matches_assigned_match_id")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchSession>(entity =>
        {
            entity.ToTable("matches");
            entity.HasKey(match => match.Id).HasName("pk_matches");
            entity.Property(match => match.Id).HasColumnName("id");
            entity.Property(match => match.PlayerId).HasColumnName("player_id").IsRequired();
            entity.Property(match => match.ServerId).HasColumnName("server_id").HasMaxLength(128).IsRequired();
            entity.Property(match => match.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(match => match.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(match => match.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.HasIndex(match => match.ServerId).HasDatabaseName("ix_matches_server_id");
            entity.HasIndex(match => new { match.PlayerId, match.Status }).HasDatabaseName("ix_matches_player_id_status");
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(match => match.PlayerId)
                .HasConstraintName("fk_matches_users_player_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<GameServerSlot>()
                .WithMany()
                .HasForeignKey(match => match.ServerId)
                .HasConstraintName("fk_matches_game_server_slots_server_id")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchTicket>(entity =>
        {
            entity.ToTable("match_tickets");
            entity.HasKey(ticket => ticket.Id).HasName("pk_match_tickets");
            entity.Property(ticket => ticket.Id).HasColumnName("id");
            entity.Property(ticket => ticket.MatchId).HasColumnName("match_id").IsRequired();
            entity.Property(ticket => ticket.PlayerId).HasColumnName("player_id").IsRequired();
            entity.Property(ticket => ticket.ServerId).HasColumnName("server_id").HasMaxLength(128).IsRequired();
            entity.Property(ticket => ticket.TicketHash).HasColumnName("ticket_hash").HasMaxLength(128).IsRequired();
            entity.Property(ticket => ticket.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
            entity.Property(ticket => ticket.ConsumedAtUtc).HasColumnName("consumed_at_utc");
            entity.Property(ticket => ticket.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.HasIndex(ticket => ticket.MatchId).HasDatabaseName("ix_match_tickets_match_id");
            entity.HasIndex(ticket => ticket.PlayerId).HasDatabaseName("ix_match_tickets_player_id");
            entity.HasIndex(ticket => ticket.ServerId).HasDatabaseName("ix_match_tickets_server_id");
            entity.HasIndex(ticket => ticket.TicketHash).HasDatabaseName("ix_match_tickets_ticket_hash").IsUnique();
            entity.HasOne<MatchSession>()
                .WithMany()
                .HasForeignKey(ticket => ticket.MatchId)
                .HasConstraintName("fk_match_tickets_matches_match_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(ticket => ticket.PlayerId)
                .HasConstraintName("fk_match_tickets_users_player_id")
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<GameServerSlot>()
                .WithMany()
                .HasForeignKey(ticket => ticket.ServerId)
                .HasConstraintName("fk_match_tickets_game_server_slots_server_id")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

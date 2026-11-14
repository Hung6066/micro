using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace His.Hope.Messaging.Sql.Migrations;

[DbContext(typeof(SqlMessagingDbContext))]
partial class SqlMessagingDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable  provider
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.10")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        SqlMessagingDbContextModelSnapshot.ConfigureOutbox(modelBuilder);
        SqlMessagingDbContextModelSnapshot.ConfigureInbox(modelBuilder);
        SqlMessagingDbContextModelSnapshot.ConfigureIdempotency(modelBuilder);
    }

    private static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlOutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.ToTable("his_hope_outbox");
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.EventJson).IsRequired();
            entity.Property(x => x.AvailableAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.PublishedAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.PublishedAt, x.AvailableAt });
        });
    }

    private static void ConfigureInbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlInboxMessage>(entity =>
        {
            entity.HasKey(x => new { x.EventId, x.Consumer });
            entity.ToTable("his_hope_inbox");
            entity.Property(x => x.EventId).ValueGeneratedNever();
            entity.Property(x => x.Consumer).HasMaxLength(200);
            entity.Property(x => x.ProcessingAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        });
    }

    private static void ConfigureIdempotency(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlIdempotencyRecord>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.ToTable("his_hope_idempotency");
            entity.Property(x => x.Key).HasMaxLength(255);
            entity.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        });
    }
}

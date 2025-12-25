using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GTasks.Core.Models;

namespace GTasks.Data;

public class AppDbContext : DbContext
{
    public DbSet<TaskList> TaskLists => Set<TaskList>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskLink> TaskLinks => Set<TaskLink>();
    public DbSet<PendingChange> PendingChanges => Set<PendingChange>();

    private readonly string _dbPath;

    public AppDbContext()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTasks");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "gtasks.db");
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTasks");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "gtasks.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure DateTimeOffset to store as ticks for proper SQLite sorting
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

        // TaskList
        modelBuilder.Entity<TaskList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(1024);
            entity.HasIndex(e => e.GoogleId).IsUnique();
            entity.Property(e => e.UpdatedAt).HasConversion(dateTimeOffsetConverter);

            entity.HasMany(e => e.Tasks)
                .WithOne()
                .HasForeignKey(t => t.TaskListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskItem
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(1024);
            entity.Property(e => e.Notes).HasMaxLength(8192);
            entity.HasIndex(e => e.GoogleId);
            entity.HasIndex(e => e.TaskListId);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.Due);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Due).HasConversion(nullableDateTimeOffsetConverter);
            entity.Property(e => e.Completed).HasConversion(nullableDateTimeOffsetConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(dateTimeOffsetConverter);
            entity.Property(e => e.CreatedAt).HasConversion(dateTimeOffsetConverter);

            // Self-referencing for subtasks
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Subtasks)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Links)
                .WithOne(l => l.Task)
                .HasForeignKey(l => l.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskLink
        modelBuilder.Entity<TaskLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).HasMaxLength(2048);
        });

        // PendingChange
        modelBuilder.Entity<PendingChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.CreatedAt).HasConversion(dateTimeOffsetConverter);
        });
    }
}

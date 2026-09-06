using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Persistence;

public sealed class OvermindDbContext : DbContext
{
    public OvermindDbContext(DbContextOptions<OvermindDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ContextEntry> ContextEntries => Set<ContextEntry>();
    public DbSet<SkillFile> SkillFiles => Set<SkillFile>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EntryTag> EntryTags => Set<EntryTag>();
    public DbSet<EntryVersion> EntryVersions => Set<EntryVersion>();
    public DbSet<SkillKit> SkillKits => Set<SkillKit>();
    public DbSet<SkillKitEntry> SkillKitEntries => Set<SkillKitEntry>();
    public DbSet<ProjectKit> ProjectKits => Set<ProjectKit>();
    public DbSet<MaterializedFile> MaterializedFiles => Set<MaterializedFile>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=overmind.db");
        }

        optionsBuilder.AddInterceptors(WalPragmaInterceptor.Instance);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(builder =>
        {
            builder.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<ContextEntry>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.SkillFiles)
                .WithOne()
                .HasForeignKey(f => f.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Versions)
                .WithOne()
                .HasForeignKey(v => v.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EntryTag>(builder =>
        {
            builder.HasKey(e => new { e.EntryId, e.TagId });

            builder.HasOne(e => e.Entry)
                .WithMany(e => e.EntryTags)
                .HasForeignKey(e => e.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Tag)
                .WithMany(t => t.EntryTags)
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EntryVersion>(builder =>
        {
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.HasIndex(v => new { v.EntryId, v.Version }).IsUnique();
        });

        modelBuilder.Entity<SkillKitEntry>(builder =>
        {
            builder.HasKey(e => new { e.KitId, e.EntryId });

            builder.HasOne(e => e.Kit)
                .WithMany(k => k.SkillKitEntries)
                .HasForeignKey(e => e.KitId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Entry)
                .WithMany()
                .HasForeignKey(e => e.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectKit>(builder =>
        {
            builder.HasKey(e => new { e.ProjectId, e.KitId });

            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<SkillKit>()
                .WithMany()
                .HasForeignKey(e => e.KitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaterializedFile>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<SkillKit>()
                .WithMany()
                .HasForeignKey(m => m.KitId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(m => m.ProjectId);
        });
    }
}

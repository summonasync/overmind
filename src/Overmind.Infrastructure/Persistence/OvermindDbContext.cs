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
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<PlanningFeature> PlanningFeatures => Set<PlanningFeature>();
    public DbSet<PlanningFeatureDependency> PlanningFeatureDependencies => Set<PlanningFeatureDependency>();
    public DbSet<PlanningArtifact> PlanningArtifacts => Set<PlanningArtifact>();
    public DbSet<PlanningArtifactVersion> PlanningArtifactVersions => Set<PlanningArtifactVersion>();
    public DbSet<PlanningTaskDependency> PlanningTaskDependencies => Set<PlanningTaskDependency>();
    public DbSet<PlanningMaterializedFile> PlanningMaterializedFiles => Set<PlanningMaterializedFile>();
    public DbSet<FrontmatterKey> FrontmatterKeys => Set<FrontmatterKey>();
    public DbSet<AgentNotification> AgentNotifications => Set<AgentNotification>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();

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
        modelBuilder.Entity<AppSetting>(builder =>
        {
            builder.HasKey(s => s.Key);
            builder.Property(s => s.Key).HasMaxLength(200);
        });

        modelBuilder.Entity<Project>(builder =>
        {
            builder.HasIndex(p => p.Name).IsUnique();

            builder.Property(p => p.ClaimStaleThresholdMinutes)
                .HasDefaultValue(Project.DefaultClaimStaleThresholdMinutes)
                .HasSentinel(Project.DefaultClaimStaleThresholdMinutes);
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

        modelBuilder.Entity<Tag>(builder =>
        {
            builder.Property(t => t.Color).HasMaxLength(16);
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

        modelBuilder.Entity<SkillKit>(builder =>
        {
            builder.HasIndex(k => k.Name).IsUnique();
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

        modelBuilder.Entity<PlanningFeature>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(f => f.Artifacts)
                .WithOne()
                .HasForeignKey(a => a.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(f => new { f.ProjectId, f.Slug }).IsUnique();

            builder.Property(f => f.ClaimedByAlias).HasMaxLength(AgentSession.MaxIdentityLength);
            builder.Property(f => f.ClaimedByVendor).HasMaxLength(AgentSession.MaxIdentityLength);
            builder.Property(f => f.ClaimedByHuman).HasDefaultValue(false);
            builder.Property(f => f.Priority).HasDefaultValue(FeaturePriority.P2).HasSentinel(FeaturePriority.P2);
            builder.Property(f => f.QueuePosition).HasDefaultValue(0);
        });

        // Deleting either endpoint of a dependency link must drop the link row; SQLite accepts
        // the self-referencing cascade pair, SQL Server would not.
        modelBuilder.Entity<PlanningFeatureDependency>(builder =>
        {
            builder.HasKey(d => new { d.FeatureId, d.BlockedByFeatureId });

            builder.HasOne(d => d.Feature)
                .WithMany(f => f.BlockedBy)
                .HasForeignKey(d => d.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.BlockedBy)
                .WithMany()
                .HasForeignKey(d => d.BlockedByFeatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanningArtifact>(builder =>
        {
            builder.HasMany(a => a.Versions)
                .WithOne()
                .HasForeignKey(v => v.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => new { a.FeatureId, a.Kind, a.Order }).IsUnique();
        });

        modelBuilder.Entity<PlanningArtifactVersion>(builder =>
        {
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.HasIndex(v => new { v.ArtifactId, v.Version }).IsUnique();
        });

        modelBuilder.Entity<PlanningTaskDependency>(builder =>
        {
            builder.HasKey(d => new { d.BlockedById, d.BlockingId });

            builder.HasOne(d => d.Task)
                .WithMany(a => a.BlockedBy)
                .HasForeignKey(d => d.BlockedById)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.BlockedBy)
                .WithMany()
                .HasForeignKey(d => d.BlockingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlanningMaterializedFile has multiple cascade paths (Project direct, Project -> PlanningFeature -> row,
        // and a nullable FeatureId). SQLite accepts this; SQL Server would reject without Restrict.
        modelBuilder.Entity<PlanningMaterializedFile>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PlanningFeature>()
                .WithMany()
                .HasForeignKey(m => m.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(m => new { m.ProjectId, m.RelativePath }).IsUnique();
        });

        modelBuilder.Entity<FrontmatterKey>(builder =>
        {
            builder.Property(k => k.Name).HasMaxLength(200).UseCollation("NOCASE");
            builder.Property(k => k.Color).HasMaxLength(16);

            builder.HasIndex(k => k.Name).IsUnique();
        });

        modelBuilder.Entity<AgentNotification>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(n => n.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(n => n.Text).HasMaxLength(2000);

            builder.HasIndex(n => new { n.ProjectId, n.CreatedAt });
        });

        modelBuilder.Entity<AgentSession>(builder =>
        {
            builder.Property(s => s.Alias).HasMaxLength(AgentSession.MaxIdentityLength);
            builder.Property(s => s.Vendor).HasMaxLength(AgentSession.MaxIdentityLength);
        });
    }
}

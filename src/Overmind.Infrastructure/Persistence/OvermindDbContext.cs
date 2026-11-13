using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;
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
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueAppend> IssueAppends => Set<IssueAppend>();
    public DbSet<PlanningLane> PlanningLanes => Set<PlanningLane>();
    public DbSet<PlanningDirective> PlanningDirectives => Set<PlanningDirective>();
    public DbSet<EntryAck> EntryAcks => Set<EntryAck>();
    public DbSet<PlanningOperationRecord> PlanningOperationRecords => Set<PlanningOperationRecord>();
    public DbSet<WaveMessage> WaveMessages => Set<WaveMessage>();
    public DbSet<WaveWatermark> WaveWatermarks => Set<WaveWatermark>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<TelegramChatMapping> TelegramChatMappings => Set<TelegramChatMapping>();
    public DbSet<TelegramValidatorMapping> TelegramValidatorMappings => Set<TelegramValidatorMapping>();
    public DbSet<TelegramNotificationDelivery> TelegramNotificationDeliveries => Set<TelegramNotificationDelivery>();
    public DbSet<AgentRouteRow> AgentRoutes => Set<AgentRouteRow>();
    public DbSet<FeatureClaimSpan> FeatureClaimSpans => Set<FeatureClaimSpan>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

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

            builder.Property(p => p.NumberStyle)
                .HasDefaultValue(ProjectNumberStyle.Hash)
                .HasSentinel(ProjectNumberStyle.Hash);

            builder.Property(p => p.NumberPrefix)
                .HasMaxLength(Project.NumberPrefixMaxLength);

            builder.Property(p => p.IsFavorite)
                .HasDefaultValue(false)
                .HasSentinel(false);

            builder.Property(p => p.AutoActOnReplies)
                .HasDefaultValue(false)
                .HasSentinel(false);

            builder.Property(p => p.HumanDigestEnabled)
                .HasDefaultValue(false)
                .HasSentinel(false);

            builder.HasMany(p => p.Lanes)
                .WithOne()
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
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

            builder.HasMany(e => e.Acks)
                .WithOne()
                .HasForeignKey(a => a.ContextEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(e => e.State)
                .HasDefaultValue(ContextEntry.ActiveState);

            builder.Property(e => e.Enforces)
                .HasDefaultValue(false)
                .HasSentinel(false);
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
            builder.Property(f => f.PinnedBranch).HasMaxLength(200);
            builder.Property(f => f.PinnedWorktreePath).HasMaxLength(400);
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

            builder.HasQueryFilter(a => a.DeletedAt == null);

            builder.HasIndex(a => new { a.FeatureId, a.Kind, a.Order })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
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
            builder.Property(s => s.Role).HasMaxLength(AgentSession.MaxIdentityLength)
                .HasDefaultValue(AgentSession.DefaultRole);
        });

        modelBuilder.Entity<Issue>(builder =>
        {
            builder.Property(i => i.Kind).HasMaxLength(Issue.MaxKindLength)
                .HasDefaultValue(Issue.DefaultKind);

            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(i => i.AuthorProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(i => new { i.ProjectId, i.Status });
            builder.HasIndex(i => i.AuthorProjectId);
        });

        modelBuilder.Entity<IssueAppend>(builder =>
        {
            builder.HasOne<Issue>()
                .WithMany()
                .HasForeignKey(a => a.IssueId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => new { a.IssueId, a.CreatedAt });
        });

        modelBuilder.Entity<PlanningLane>(builder =>
        {
            builder.Property(l => l.Name).HasMaxLength(PlanningLane.MaxNameLength).UseCollation("NOCASE");
            builder.Property(l => l.Contour).HasMaxLength(PlanningLane.MaxContourLength);
            builder.Property(l => l.BoundAlias).HasMaxLength(PlanningLane.MaxBoundAliasLength);
            builder.Property(l => l.LiveSurfaces).HasMaxLength(PlanningLane.MaxLiveSurfacesJsonLength);

            builder.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique();
        });

        modelBuilder.Entity<PlanningFeature>(builder =>
        {
            builder.Property(f => f.Lane).HasMaxLength(PlanningLane.MaxNameLength);
            builder.Property(f => f.CodeSurfaces).HasMaxLength(4000);
        });

        modelBuilder.Entity<PlanningDirective>(builder =>
        {
            builder.HasOne<PlanningFeature>()
                .WithMany()
                .HasForeignKey(d => d.PlanningFeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(d => d.Text).HasMaxLength(PlanningDirective.MaxTextLength);
            builder.Property(d => d.AuthorAlias).HasMaxLength(PlanningDirective.MaxIdentityLength);
            builder.Property(d => d.AuthorVendor).HasMaxLength(PlanningDirective.MaxIdentityLength);
            builder.Property(d => d.AckByAlias).HasMaxLength(PlanningDirective.MaxIdentityLength);
            builder.Property(d => d.AckVendor).HasMaxLength(PlanningDirective.MaxIdentityLength);

            builder.HasIndex(d => new { d.PlanningFeatureId, d.AckAt });
        });

        modelBuilder.Entity<EntryAck>(builder =>
        {
            builder.Property(a => a.Alias).HasMaxLength(EntryAck.MaxIdentityLength);
            builder.Property(a => a.Vendor).HasMaxLength(EntryAck.MaxIdentityLength);

            builder.HasIndex(a => a.ContextEntryId);
        });

        modelBuilder.Entity<PlanningOperationRecord>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => new { r.ProjectId, r.OperationId }).IsUnique();
        });

        modelBuilder.Entity<WaveMessage>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(m => m.Room).HasMaxLength(PlanningLane.MaxNameLength).UseCollation("NOCASE");
            builder.Property(m => m.AuthorAlias).HasMaxLength(WaveMessage.MaxIdentityLength);
            builder.Property(m => m.AuthorRole).HasMaxLength(WaveMessage.MaxIdentityLength)
                .HasDefaultValue(AgentSession.DefaultRole);
            builder.Property(m => m.Text).HasMaxLength(WaveMessage.MaxTextLength);
            builder.Property(m => m.Mentions).HasMaxLength(WaveMessage.MaxMentionsLength);

            builder.HasIndex(m => new { m.ProjectId, m.Room, m.CreatedAt });
            builder.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<WaveWatermark>(builder =>
        {
            builder.HasKey(w => new { w.ProjectId, w.Alias });

            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(w => w.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(w => w.Alias).HasMaxLength(WaveWatermark.MaxIdentityLength).UseCollation("NOCASE");
        });

        modelBuilder.Entity<Conversation>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(c => c.Title).HasMaxLength(Conversation.MaxTitleLength);

            builder.HasIndex(c => new { c.ProjectId, c.UpdatedAt });
        });

        modelBuilder.Entity<ConversationMessage>(builder =>
        {
            builder.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(m => m.Role).HasMaxLength(ConversationMessage.MaxRoleLength);
            builder.Property(m => m.Content).HasMaxLength(ConversationMessage.MaxTextLength);

            builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        });

        modelBuilder.Entity<TelegramChatMapping>(builder =>
        {
            builder.HasKey(m => m.ChatId);

            builder.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TelegramValidatorMapping>(builder =>
        {
            builder.HasKey(m => m.Id);

            builder.HasIndex(m => new { m.ChatId, m.ProjectId }).IsUnique();

            builder.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TelegramNotificationDelivery>(builder =>
        {
            builder.HasKey(d => new { d.ChatId, d.MessageId });

            builder.HasOne<AgentNotification>()
                .WithMany()
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRouteRow>(builder =>
        {
            builder.Property(r => r.Pattern).HasMaxLength(AgentRouteText.MaxPatternLength);
            builder.Property(r => r.Domain).HasMaxLength(AgentRouteText.MaxDomainLength);
            builder.Property(r => r.TargetAgent).HasMaxLength(AgentRouteRow.MaxTargetAgentLength);
            builder.Property(r => r.MissReason).HasMaxLength(AgentRouteText.MaxMissReasonLength);

            builder.Property(r => r.LastHitAt).HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero));
            builder.Property(r => r.CreatedAt).HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero));

            builder.HasIndex(r => r.Pattern).IsUnique();
            builder.HasIndex(r => r.HitCount);
        });

        modelBuilder.Entity<FeatureClaimSpan>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PlanningFeature>()
                .WithMany()
                .HasForeignKey(s => s.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(s => s.Alias).HasMaxLength(AgentSession.MaxIdentityLength);
            builder.Property(s => s.Vendor).HasMaxLength(AgentSession.MaxIdentityLength);
            builder.Property(s => s.ByHuman).HasDefaultValue(false);

            builder.HasIndex(s => new { s.FeatureId, s.ReleasedAt });
        });

        modelBuilder.Entity<UsageRecord>(builder =>
        {
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(u => u.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(u => u.Alias).HasMaxLength(AgentSession.MaxIdentityLength);
            builder.Property(u => u.Model).HasMaxLength(UsageRecord.MaxModelLength);

            builder.HasIndex(u => u.Ts);
            builder.HasIndex(u => new { u.Alias, u.SessionId });
        });
    }
}

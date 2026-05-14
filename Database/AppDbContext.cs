using Microsoft.EntityFrameworkCore;
using Portlink.Api.Entities;
using Portlink.Api.Modules.Auth.Entities;
using Portlink.Api.Modules.Storage.Entities;

namespace Portlink.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ──────────────────────── DbSets ────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();
    public DbSet<SubcontractorProfile> SubcontractorProfiles => Set<SubcontractorProfile>();
    public DbSet<JobListing> JobListings => Set<JobListing>();
    public DbSet<JobFile> JobFiles => Set<JobFile>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<AssignedJob> AssignedJobs => Set<AssignedJob>();
    public DbSet<JobLog> JobLogs => Set<JobLog>();
    public DbSet<JobReport> JobReports => Set<JobReport>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<ConversationMessageDeletion> ConversationMessageDeletions => Set<ConversationMessageDeletion>();
    public DbSet<ConversationUserState> ConversationUserStates => Set<ConversationUserState>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Port> Ports => Set<Port>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<StorageFile> StorageFiles => Set<StorageFile>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ─────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── AgentProfile ──────────────────────────────────────
        modelBuilder.Entity<AgentProfile>(e =>
        {
            e.HasOne(a => a.User)
             .WithOne(u => u.AgentProfile)
             .HasForeignKey<AgentProfile>(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(a => a.UserId).IsUnique();
            e.Property(a => a.Rating).HasPrecision(3, 2);
        });

        // ── SubcontractorProfile ──────────────────────────────
        modelBuilder.Entity<SubcontractorProfile>(e =>
        {
            e.HasOne(s => s.User)
             .WithOne(u => u.SubcontractorProfile)
             .HasForeignKey<SubcontractorProfile>(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => s.UserId).IsUnique();
            e.Property(s => s.Rating).HasPrecision(3, 2);

            // PostgreSQL text[] için
            e.Property(s => s.ExpertiseTags)
             .HasColumnType("text[]");
        });

        // ── JobListing ────────────────────────────────────────
        modelBuilder.Entity<JobListing>(e =>
        {
            e.HasOne(j => j.Agent)
             .WithMany(a => a.JobListings)
             .HasForeignKey(j => j.AgentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(j => j.ListingImageStorageFile)
             .WithMany()
             .HasForeignKey(j => j.ListingImageStorageFileId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(j => j.SelectedServices)
             .HasColumnType("text[]");

            e.Property(j => j.BudgetMin).HasPrecision(15, 2);
            e.Property(j => j.BudgetMax).HasPrecision(15, 2);
        });

        // ── JobFile ───────────────────────────────────────────
        modelBuilder.Entity<JobFile>(e =>
        {
            e.HasOne(f => f.JobListing)
             .WithMany(j => j.JobFiles)
             .HasForeignKey(f => f.JobId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.Uploader)
             .WithMany()
             .HasForeignKey(f => f.UploadedBy)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(f => f.StorageFile)
             .WithMany()
             .HasForeignKey(f => f.StorageFileId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Offer ─────────────────────────────────────────────
        modelBuilder.Entity<Offer>(e =>
        {
            e.HasOne(o => o.JobListing)
             .WithMany(j => j.Offers)
             .HasForeignKey(o => o.JobId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(o => o.Subcontractor)
             .WithMany(s => s.Offers)
             .HasForeignKey(o => o.SubcontractorId)
             .OnDelete(DeleteBehavior.Cascade);

            // Her taşeron bir ilana bir teklif verebilir
            e.HasIndex(o => new { o.JobId, o.SubcontractorId }).IsUnique();

            e.Property(o => o.Price).HasPrecision(15, 2);
        });

        // ── AssignedJob ───────────────────────────────────────
        modelBuilder.Entity<AssignedJob>(e =>
        {
            e.HasOne(a => a.JobListing)
             .WithOne(j => j.AssignedJob)
             .HasForeignKey<AssignedJob>(a => a.JobId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Offer)
             .WithOne(o => o.AssignedJob)
             .HasForeignKey<AssignedJob>(a => a.OfferId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Agent)
             .WithMany(ag => ag.AssignedJobs)
             .HasForeignKey(a => a.AgentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Subcontractor)
             .WithMany(s => s.AssignedJobs)
             .HasForeignKey(a => a.SubcontractorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── JobLog ────────────────────────────────────────────
        modelBuilder.Entity<JobLog>(e =>
        {
            e.HasOne(l => l.AssignedJob)
             .WithMany(a => a.JobLogs)
             .HasForeignKey(l => l.AssignedJobId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Creator)
             .WithMany()
             .HasForeignKey(l => l.CreatedBy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── JobReport ─────────────────────────────────────────
        modelBuilder.Entity<JobReport>(e =>
        {
            e.HasOne(r => r.AssignedJob)
             .WithMany(a => a.JobReports)
             .HasForeignKey(r => r.AssignedJobId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Uploader)
             .WithMany()
             .HasForeignKey(r => r.UploadedBy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasOne(c => c.Agent)
             .WithMany()
             .HasForeignKey(c => c.AgentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Subcontractor)
             .WithMany()
             .HasForeignKey(c => c.SubcontractorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.JobListing)
             .WithMany()
             .HasForeignKey(c => c.JobListingId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(c => new { c.AgentId, c.SubcontractorId, c.JobListingId }).IsUnique();
        });

        modelBuilder.Entity<ConversationMessage>(e =>
        {
            e.HasOne(m => m.Conversation)
             .WithMany(c => c.Messages)
             .HasForeignKey(m => m.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Sender)
             .WithMany()
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(m => m.Body)
             .HasMaxLength(4000);

            e.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        });

        modelBuilder.Entity<ConversationMessageDeletion>(e =>
        {
            e.HasOne(d => d.Message)
             .WithMany(m => m.Deletions)
             .HasForeignKey(d => d.MessageId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(d => d.User)
             .WithMany()
             .HasForeignKey(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(d => new { d.MessageId, d.UserId }).IsUnique();
        });

        modelBuilder.Entity<ConversationUserState>(e =>
        {
            e.HasOne(s => s.Conversation)
             .WithMany(c => c.UserStates)
             .HasForeignKey(s => s.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.User)
             .WithMany()
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => new { s.ConversationId, s.UserId }).IsUnique();
        });

        // ── Message ───────────────────────────────────────────
        modelBuilder.Entity<Message>(e =>
        {
            e.HasOne(m => m.AssignedJob)
             .WithMany(a => a.Messages)
             .HasForeignKey(m => m.AssignedJobId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(m => m.Sender)
             .WithMany()
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Receiver)
             .WithMany()
             .HasForeignKey(m => m.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Notification ──────────────────────────────────────
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(n => n.Data)
             .HasColumnType("jsonb");
        });

        // ── Port ──────────────────────────────────────────────
        modelBuilder.Entity<StorageFile>(e =>
        {
            e.Property(s => s.FileCategory).HasConversion<string>();
            e.Property(s => s.RelatedEntityType).HasConversion<string>();

            e.HasOne(s => s.UploadedByUser)
             .WithMany()
             .HasForeignKey(s => s.UploadedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(s => s.UploadedByUserId);
            e.HasIndex(s => new { s.RelatedEntityType, s.RelatedEntityId });
            e.HasIndex(s => s.CreatedAt);
            e.HasIndex(s => s.IsDeleted);
        });

        modelBuilder.Entity<Port>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
        });

        // Seed initial ports
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Port>().HasData(
            new Port { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "TRIST-001", Name = "Ambarlı Limanı", Region = "İstanbul", CreatedAt = seedDate },
            new Port { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "TRIST-002", Name = "Haydarpaşa Limanı", Region = "İstanbul", CreatedAt = seedDate },
            new Port { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "TRIST-003", Name = "Tuzla Limanı", Region = "İstanbul", CreatedAt = seedDate },
            new Port { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Code = "TRYAL-001", Name = "Yalova Limanı", Region = "Yalova", CreatedAt = seedDate },
            new Port { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Code = "TRKOC-001", Name = "Derince Limanı", Region = "Kocaeli", CreatedAt = seedDate }
        );

        // ── ServiceCategory ───────────────────────────────────
        modelBuilder.Entity<ServiceCategory>(e =>
        {
            e.HasIndex(sc => sc.Code).IsUnique();
            e.Property(sc => sc.SubServices)
             .HasColumnType("text[]");
        });

        // ── WalletTransaction ─────────────────────────────────
        modelBuilder.Entity<WalletTransaction>(e =>
        {
            e.HasOne(w => w.Subcontractor)
             .WithMany(s => s.WalletTransactions)
             .HasForeignKey(w => w.SubcontractorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(w => w.AssignedJob)
             .WithMany(a => a.WalletTransactions)
             .HasForeignKey(w => w.AssignedJobId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(w => w.Amount).HasPrecision(15, 2);
        });

        // ── RefreshToken ──────────────────────────────────────
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasOne(r => r.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

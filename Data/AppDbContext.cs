using Microsoft.EntityFrameworkCore;
using Portlink.Api.Entities;

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
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Port> Ports => Set<Port>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
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
        modelBuilder.Entity<Port>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
        });

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

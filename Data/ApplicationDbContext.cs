using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Models;

namespace ShiftingGuru.Data;

/// <summary>
/// One context for the whole application. Now inherits IdentityDbContext so
/// the Identity tables live alongside Leads - a second DbContext would mean a
/// second connection and no shared transactions.
///
/// Also implements IDataProtectionKeyContext: the keys that sign login cookies
/// and form tokens are stored here, so every Cloud Run instance shares them
/// and they survive restarts and deploys.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorDocument> VendorDocuments => Set<VendorDocument>();
    public DbSet<RegistrationPayment> RegistrationPayments => Set<RegistrationPayment>();
    public DbSet<VendorService> VendorServices => Set<VendorService>();
    public DbSet<LeadAssignment> LeadAssignments => Set<LeadAssignment>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<CustomerAccessToken> CustomerAccessTokens => Set<CustomerAccessToken>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<MovingRoute> Routes => Set<MovingRoute>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    // NEW: one-time codes from the partner sign-up form.
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();

    // NEW: shared encryption keys (see the class comment).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must run first: this is what configures the Identity tables.
        base.OnModelCreating(builder);

        builder.HasSequence<long>("lead_number_seq").StartsAt(1).IncrementsBy(1);
        builder.HasSequence<long>("vendor_number_seq").StartsAt(1).IncrementsBy(1);
        builder.HasSequence<long>("quote_number_seq").StartsAt(1).IncrementsBy(1);

        builder.Entity<Lead>(lead =>
        {
            lead.ToTable("Leads");
            lead.HasKey(l => l.Id);

            lead.Property(l => l.LeadNumber).IsRequired().HasMaxLength(24);
            lead.HasIndex(l => l.LeadNumber).IsUnique();

            lead.Property(l => l.ServiceSlug).IsRequired().HasMaxLength(60);
            lead.Property(l => l.ServiceName).IsRequired().HasMaxLength(80);

            lead.Property(l => l.MovingFrom).HasMaxLength(80);
            lead.Property(l => l.MovingTo).HasMaxLength(80);
            lead.Property(l => l.StorageLocation).HasMaxLength(80);

            lead.Property(l => l.PropertyType).HasMaxLength(40);
            lead.Property(l => l.MoveSize).HasMaxLength(120);
            lead.Property(l => l.OfficeSize).HasMaxLength(60);
            lead.Property(l => l.DeskCount).HasMaxLength(30);
            lead.Property(l => l.VehicleType).HasMaxLength(40);
            lead.Property(l => l.VehicleModel).HasMaxLength(80);
            lead.Property(l => l.VehicleCondition).HasMaxLength(20);
            lead.Property(l => l.GoodsType).HasMaxLength(80);
            lead.Property(l => l.LoadDetails).HasMaxLength(120);
            lead.Property(l => l.VehicleRequirement).HasMaxLength(60);
            lead.Property(l => l.StorageType).HasMaxLength(40);
            lead.Property(l => l.StorageSize).HasMaxLength(60);
            lead.Property(l => l.StorageDuration).HasMaxLength(40);

            lead.Property(l => l.CustomerName).IsRequired().HasMaxLength(80);
            lead.Property(l => l.Phone).IsRequired().HasMaxLength(20);
            lead.Property(l => l.Email).HasMaxLength(120);
            lead.Property(l => l.PreferredContactMethod).HasMaxLength(20);
            lead.Property(l => l.AdditionalRequirements).HasMaxLength(600);

            lead.Property(l => l.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            lead.Property(l => l.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            lead.Property(l => l.UpdatedAt).HasColumnType("timestamp with time zone");
            lead.Property(l => l.MovingDate).HasColumnType("date");

            lead.HasIndex(l => l.CreatedAt);
            lead.HasIndex(l => l.Status);
            lead.HasIndex(l => new { l.Phone, l.CreatedAt });
            lead.HasIndex(l => l.SelectedVendorId);
            lead.HasIndex(l => l.SelectedQuoteId);
            lead.HasIndex(l => l.ConvertedAt);

            lead.Property(l => l.ConvertedAt).HasColumnType("timestamp with time zone");

            // Restrict on both: a converted lead must keep pointing at the
            // vendor and quote that won it.
            lead.HasOne(l => l.SelectedVendor)
                .WithMany()
                .HasForeignKey(l => l.SelectedVendorId)
                .OnDelete(DeleteBehavior.Restrict);

            lead.HasOne(l => l.SelectedQuote)
                .WithMany()
                .HasForeignKey(l => l.SelectedQuoteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Supports admin search on customer name.
            lead.HasIndex(l => l.CustomerName);
        });

        builder.Entity<Vendor>(vendor =>
        {
            vendor.ToTable("Vendors");
            vendor.HasKey(v => v.Id);

            vendor.Property(v => v.VendorNumber).IsRequired().HasMaxLength(28);
            vendor.HasIndex(v => v.VendorNumber).IsUnique();

            // One Identity account per vendor record, enforced by the database.
            vendor.Property(v => v.IdentityUserId).IsRequired().HasMaxLength(450);
            vendor.HasIndex(v => v.IdentityUserId).IsUnique();

            vendor.Property(v => v.BusinessName).IsRequired().HasMaxLength(120);
            vendor.Property(v => v.ContactPerson).IsRequired().HasMaxLength(80);
            vendor.Property(v => v.Phone).IsRequired().HasMaxLength(20);
            vendor.Property(v => v.Email).IsRequired().HasMaxLength(120);
            vendor.Property(v => v.City).IsRequired().HasMaxLength(80);
            vendor.Property(v => v.Address).IsRequired().HasMaxLength(250);
            vendor.Property(v => v.GstNumber).HasMaxLength(20);
            vendor.Property(v => v.OperatingLocations).HasMaxLength(250);
            vendor.Property(v => v.AdditionalInformation).HasMaxLength(600);

            vendor.Property(v => v.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            vendor.Property(v => v.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            vendor.Property(v => v.UpdatedAt).HasColumnType("timestamp with time zone");

            vendor.HasIndex(v => v.Email);
            vendor.HasIndex(v => v.Phone);
            vendor.HasIndex(v => v.Status);
            vendor.HasIndex(v => v.City);
            vendor.HasIndex(v => v.CreatedAt);

            vendor.HasMany(v => v.Services)
                  .WithOne(s => s.Vendor!)
                  .HasForeignKey(s => s.VendorId)
                  .OnDelete(DeleteBehavior.Cascade);

            // NEW: when the email and mobile codes were confirmed at sign-up.
            vendor.Property(v => v.EmailVerifiedAt).HasColumnType("timestamp with time zone");
            vendor.Property(v => v.PhoneVerifiedAt).HasColumnType("timestamp with time zone");

            // NEW: uploaded KYC documents. The files themselves live in storage;
            // these rows only say where.
            vendor.HasMany(v => v.Documents)
                  .WithOne(d => d.Vendor!)
                  .HasForeignKey(d => d.VendorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // NEW
        builder.Entity<VendorDocument>(document =>
        {
            document.ToTable("VendorDocuments");
            document.HasKey(d => d.Id);

            document.Property(d => d.Type).IsRequired().HasMaxLength(30).HasConversion<string>();
            document.Property(d => d.StoragePath).IsRequired().HasMaxLength(300);
            document.Property(d => d.ContentType).IsRequired().HasMaxLength(60);
            document.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(200);
            document.Property(d => d.UploadedAt).IsRequired().HasColumnType("timestamp with time zone");

            // One document of each type per vendor.
            document.HasIndex(d => new { d.VendorId, d.Type }).IsUnique();
        });

        builder.Entity<VendorService>(service =>
        {
            service.ToTable("VendorServices");
            service.HasKey(s => s.Id);

            service.Property(s => s.ServiceSlug).IsRequired().HasMaxLength(60);
            service.Property(s => s.ServiceName).IsRequired().HasMaxLength(80);

            // A vendor can't list the same service twice.
            service.HasIndex(s => new { s.VendorId, s.ServiceSlug }).IsUnique();
            service.HasIndex(s => s.ServiceSlug);
        });

        builder.Entity<LeadAssignment>(assignment =>
        {
            assignment.ToTable("LeadAssignments");
            assignment.HasKey(a => a.Id);

            assignment.Property(a => a.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            assignment.Property(a => a.AssignedAt).IsRequired().HasColumnType("timestamp with time zone");
            assignment.Property(a => a.ViewedAt).HasColumnType("timestamp with time zone");
            assignment.Property(a => a.UpdatedAt).HasColumnType("timestamp with time zone");

            // The same lead can't be assigned to the same vendor twice. A
            // withdrawn assignment is reactivated rather than duplicated.
            assignment.HasIndex(a => new { a.LeadId, a.VendorId }).IsUnique();

            // Supports the vendor's "my leads" query and the admin filters.
            assignment.HasIndex(a => new { a.VendorId, a.Status });
            assignment.HasIndex(a => a.LeadId);
            assignment.HasIndex(a => a.AssignedAt);

            assignment.HasOne(a => a.Lead)
                      .WithMany(l => l.Assignments)
                      .HasForeignKey(a => a.LeadId)
                      .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: removing a vendor must not silently wipe
            // the assignment history attached to their leads.
            assignment.HasOne(a => a.Vendor)
                      .WithMany(v => v.Assignments)
                      .HasForeignKey(a => a.VendorId)
                      .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Quote>(quote =>
        {
            quote.ToTable("Quotes");
            quote.HasKey(q => q.Id);

            quote.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(28);
            quote.HasIndex(q => q.QuoteNumber).IsUnique();

            // decimal(18,2): exact to the paisa, no floating-point drift.
            foreach (var money in new[]
            {
                nameof(Quote.BasePrice), nameof(Quote.PackingCharges),
                nameof(Quote.TransportationCharges), nameof(Quote.LoadingUnloadingCharges),
                nameof(Quote.AdditionalCharges), nameof(Quote.TotalAmount)
            })
            {
                quote.Property<decimal>(money).HasColumnType("decimal(18,2)").IsRequired();
            }

            quote.Property(q => q.EstimatedPickupDate).HasColumnType("date");
            quote.Property(q => q.EstimatedDeliveryDate).HasColumnType("date");
            quote.Property(q => q.VendorNotes).HasMaxLength(800);

            quote.Property(q => q.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            quote.Property(q => q.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            quote.Property(q => q.UpdatedAt).HasColumnType("timestamp with time zone");

            quote.HasIndex(q => q.LeadId);
            quote.HasIndex(q => q.VendorId);
            quote.HasIndex(q => q.Status);
            quote.HasIndex(q => q.CreatedAt);

            // One ACTIVE quote per vendor per lead. Filtered, so a rejected or
            // cancelled quote stops blocking and the vendor can requote without
            // any history being deleted.
            quote.HasIndex(q => new { q.LeadId, q.VendorId })
                 .IsUnique()
                 .HasFilter("\"Status\" IN ('Draft', 'Submitted', 'UnderReview', 'Accepted')");

            quote.HasOne(q => q.Lead)
                 .WithMany(l => l.Quotes)
                 .HasForeignKey(q => q.LeadId)
                 .OnDelete(DeleteBehavior.Cascade);

            quote.HasOne(q => q.Vendor)
                 .WithMany(v => v.Quotes)
                 .HasForeignKey(q => q.VendorId)
                 .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerAccessToken>(token =>
        {
            token.ToTable("CustomerAccessTokens");
            token.HasKey(t => t.Id);

            // Lookup is by hash, so it has to be unique and indexed.
            token.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();

            token.Property(t => t.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            token.Property(t => t.ExpiresAt).IsRequired().HasColumnType("timestamp with time zone");
            token.Property(t => t.LastUsedAt).HasColumnType("timestamp with time zone");
            token.Property(t => t.RevokedAt).HasColumnType("timestamp with time zone");

            token.HasIndex(t => t.LeadId);
            token.HasIndex(t => t.ExpiresAt);

            token.HasOne(t => t.Lead)
                 .WithMany(l => l.AccessTokens)
                 .HasForeignKey(t => t.LeadId)
                 .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NotificationLog>(notification =>
        {
            notification.ToTable("NotificationLogs");
            notification.HasKey(n => n.Id);

            notification.Property(n => n.Type).IsRequired().HasMaxLength(40).HasConversion<string>();
            notification.Property(n => n.Status).IsRequired().HasMaxLength(20).HasConversion<string>();

            notification.Property(n => n.Recipient).IsRequired().HasMaxLength(180);
            notification.Property(n => n.Subject).IsRequired().HasMaxLength(200);
            notification.Property(n => n.HtmlBody).IsRequired();
            notification.Property(n => n.EntityType).IsRequired().HasMaxLength(40);
            notification.Property(n => n.ErrorMessage).HasMaxLength(500);

            notification.Property(n => n.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            notification.Property(n => n.LastAttemptAt).HasColumnType("timestamp with time zone");
            notification.Property(n => n.SentAt).HasColumnType("timestamp with time zone");

            notification.HasIndex(n => n.Status);
            notification.HasIndex(n => n.Type);
            notification.HasIndex(n => n.CreatedAt);

            // The same event can't notify the same person twice, whatever the
            // browser does with refreshes and retries.
            notification.HasIndex(n => new { n.Type, n.EntityType, n.EntityId, n.Recipient }).IsUnique();
        });

        builder.Entity<Review>(review =>
        {
            review.ToTable("Reviews");
            review.HasKey(r => r.Id);

            review.Property(r => r.Rating).IsRequired();
            review.Property(r => r.Title).HasMaxLength(120);
            review.Property(r => r.Comment).IsRequired().HasMaxLength(1000);
            review.Property(r => r.CustomerNameSnapshot).IsRequired().HasMaxLength(80);
            review.Property(r => r.ModerationNote).HasMaxLength(500);

            review.Property(r => r.Status).IsRequired().HasMaxLength(20).HasConversion<string>();

            review.Property(r => r.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            review.Property(r => r.UpdatedAt).HasColumnType("timestamp with time zone");
            review.Property(r => r.PublishedAt).HasColumnType("timestamp with time zone");

            // One review per lead. A lead has one selected vendor, so this is
            // the whole duplicate rule - no application check can be bypassed.
            review.HasIndex(r => r.LeadId).IsUnique();

            review.HasIndex(r => r.VendorId);
            review.HasIndex(r => r.Status);
            review.HasIndex(r => r.Rating);
            review.HasIndex(r => r.CreatedAt);

            review.HasOne(r => r.Lead)
                  .WithOne(l => l.Review!)
                  .HasForeignKey<Review>(r => r.LeadId)
                  .OnDelete(DeleteBehavior.Cascade);

            review.HasOne(r => r.Vendor)
                  .WithMany(v => v.Reviews)
                  .HasForeignKey(r => r.VendorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Location>(location =>
        {
            location.ToTable("Locations");
            location.HasKey(l => l.Id);

            location.Property(l => l.Name).IsRequired().HasMaxLength(80);
            location.Property(l => l.Slug).IsRequired().HasMaxLength(80);
            location.HasIndex(l => l.Slug).IsUnique();

            location.Property(l => l.City).IsRequired().HasMaxLength(80);
            location.Property(l => l.State).HasMaxLength(80);
            location.Property(l => l.Country).IsRequired().HasMaxLength(60);

            location.Property(l => l.ShortDescription).IsRequired().HasMaxLength(300);
            location.Property(l => l.Content).IsRequired();
            location.Property(l => l.H1).IsRequired().HasMaxLength(160);
            location.Property(l => l.MetaTitle).IsRequired().HasMaxLength(160);
            location.Property(l => l.MetaDescription).IsRequired().HasMaxLength(320);
            location.Property(l => l.OgTitle).HasMaxLength(160);
            location.Property(l => l.OgDescription).HasMaxLength(320);
            location.Property(l => l.OgImage).HasMaxLength(400);

            // NEW: where the city's hero photo is stored, e.g. media/locations/agra-3f2a....webp
            location.Property(l => l.HeroImagePath).HasMaxLength(300);

            location.Property(l => l.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            location.Property(l => l.UpdatedAt).HasColumnType("timestamp with time zone");

            location.HasIndex(l => l.IsPublished);
        });

        builder.Entity<MovingRoute>(route =>
        {
            // Table stays "Routes" - only the C# type is renamed, to avoid
            // colliding with Microsoft.AspNetCore.Routing.Route.
            route.ToTable("Routes");
            route.HasKey(r => r.Id);

            route.Property(r => r.Slug).IsRequired().HasMaxLength(160);
            route.HasIndex(r => r.Slug).IsUnique();

            route.Property(r => r.H1).IsRequired().HasMaxLength(160);
            route.Property(r => r.ShortDescription).IsRequired().HasMaxLength(300);
            route.Property(r => r.Content).IsRequired();
            route.Property(r => r.MetaTitle).IsRequired().HasMaxLength(160);
            route.Property(r => r.MetaDescription).IsRequired().HasMaxLength(320);
            route.Property(r => r.OgTitle).HasMaxLength(160);
            route.Property(r => r.OgDescription).HasMaxLength(320);
            route.Property(r => r.OgImage).HasMaxLength(400);

            route.Property(r => r.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            route.Property(r => r.UpdatedAt).HasColumnType("timestamp with time zone");

            // One page per corridor, and no Delhi-to-Delhi.
            route.HasIndex(r => new { r.FromLocationId, r.ToLocationId }).IsUnique();
            route.ToTable(t => t.HasCheckConstraint(
                "ck_routes_distinct_endpoints", "\"FromLocationId\" <> \"ToLocationId\""));

            route.HasIndex(r => r.IsPublished);

            // Restrict on both ends: deleting a city must never silently take
            // every route page that mentions it.
            route.HasOne(r => r.FromLocation)
                 .WithMany(l => l.RoutesFrom)
                 .HasForeignKey(r => r.FromLocationId)
                 .OnDelete(DeleteBehavior.Restrict);

            route.HasOne(r => r.ToLocation)
                 .WithMany(l => l.RoutesTo)
                 .HasForeignKey(r => r.ToLocationId)
                 .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Faq>(faq =>
        {
            faq.ToTable("Faqs");
            faq.HasKey(f => f.Id);

            faq.Property(f => f.Question).IsRequired().HasMaxLength(300);
            faq.Property(f => f.Answer).IsRequired().HasMaxLength(2000);
            faq.Property(f => f.ServiceSlug).HasMaxLength(60);

            faq.Property(f => f.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            faq.Property(f => f.UpdatedAt).HasColumnType("timestamp with time zone");

            faq.HasIndex(f => new { f.LocationId, f.DisplayOrder });
            faq.HasIndex(f => new { f.RouteId, f.DisplayOrder });
            faq.HasIndex(f => new { f.ServiceSlug, f.DisplayOrder });

            // Exactly one owner, enforced by the database rather than trusted.
            faq.ToTable(t => t.HasCheckConstraint(
                "ck_faqs_single_owner",
                "(CASE WHEN \"LocationId\" IS NULL THEN 0 ELSE 1 END) + " +
                "(CASE WHEN \"RouteId\" IS NULL THEN 0 ELSE 1 END) + " +
                "(CASE WHEN \"ServiceSlug\" IS NULL THEN 0 ELSE 1 END) = 1"));

            faq.HasOne(f => f.Location)
               .WithMany(l => l.Faqs)
               .HasForeignKey(f => f.LocationId)
               .OnDelete(DeleteBehavior.Cascade);

            faq.HasOne(f => f.Route)
               .WithMany(r => r.Faqs)
               .HasForeignKey(f => f.RouteId)
               .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdminAuditLog>(audit =>
        {
            audit.ToTable("AdminAuditLogs");
            audit.HasKey(a => a.Id);

            audit.Property(a => a.AdminUserId).IsRequired().HasMaxLength(450);
            audit.Property(a => a.AdminEmail).HasMaxLength(180);
            audit.Property(a => a.Action).IsRequired().HasMaxLength(30).HasConversion<string>();
            audit.Property(a => a.EntityType).IsRequired().HasMaxLength(40);
            audit.Property(a => a.Description).IsRequired().HasMaxLength(400);
            audit.Property(a => a.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");

            audit.HasIndex(a => a.CreatedAt);
            audit.HasIndex(a => new { a.EntityType, a.EntityId });
            audit.HasIndex(a => a.AdminUserId);
        });

        // NEW: partner registration fee payments (Razorpay).
        builder.Entity<RegistrationPayment>(payment =>
        {
            payment.ToTable("RegistrationPayments");
            payment.HasKey(p => p.Id);

            payment.Property(p => p.Email).IsRequired().HasMaxLength(120);
            payment.Property(p => p.RazorpayOrderId).IsRequired().HasMaxLength(40);
            payment.Property(p => p.RazorpayPaymentId).HasMaxLength(40);
            payment.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
            payment.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            payment.Property(p => p.Status).IsRequired().HasMaxLength(20).HasConversion<string>();

            payment.Property(p => p.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            payment.Property(p => p.PaidAt).HasColumnType("timestamp with time zone");
            payment.Property(p => p.UsedAt).HasColumnType("timestamp with time zone");

            // One row per Razorpay order; the webhook and the browser both look it up.
            payment.HasIndex(p => p.RazorpayOrderId).IsUnique();
            payment.HasIndex(p => new { p.Email, p.CreatedAt });
            payment.HasIndex(p => p.Status);
            payment.HasIndex(p => p.VendorId);

            // Restrict: payment history is kept even if a vendor record is removed.
            payment.HasOne(p => p.Vendor)
                   .WithMany()
                   .HasForeignKey(p => p.VendorId)
                   .OnDelete(DeleteBehavior.Restrict);
        });

        // NEW
        builder.Entity<EmailVerification>(verification =>
        {
            verification.ToTable("EmailVerifications");
            verification.HasKey(e => e.Id);

            verification.Property(e => e.Email).IsRequired().HasMaxLength(120);
            verification.Property(e => e.CodeHash).IsRequired().HasMaxLength(64);
            verification.Property(e => e.TokenHash).HasMaxLength(64);

            verification.Property(e => e.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
            verification.Property(e => e.ExpiresAt).IsRequired().HasColumnType("timestamp with time zone");
            verification.Property(e => e.VerifiedAt).HasColumnType("timestamp with time zone");
            verification.Property(e => e.UsedAt).HasColumnType("timestamp with time zone");

            // "Latest code for this email" and the rate-limit counts.
            verification.HasIndex(e => new { e.Email, e.CreatedAt });
            verification.HasIndex(e => e.CreatedAt);

            // Proof tokens are looked up by hash. PostgreSQL allows many NULLs here.
            verification.HasIndex(e => e.TokenHash).IsUnique();
        });
    }
}
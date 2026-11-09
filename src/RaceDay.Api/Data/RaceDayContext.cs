using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core (Code-First) context. Table and column names, keys, constraints and delete
/// behaviour are configured to match the Part 1 ERD and SQL script exactly.
/// </summary>
public class RaceDayContext : DbContext
{
    public RaceDayContext(DbContextOptions<RaceDayContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RaceRoute> Routes => Set<RaceRoute>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<RaceResult> Results => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Users ----------
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant')"));
            e.HasKey(x => x.UserID);
            e.Property(x => x.FullName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.ProfilePictureBlobName).HasMaxLength(255).IsUnicode(false);
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ---------- Events ----------
        modelBuilder.Entity<RaceEvent>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_Type", "[EventType] IN ('Run','Walk','Cycle')"));
            e.HasKey(x => x.EventID);
            e.Property(x => x.EventName).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.EventType).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            e.Property(x => x.Description).HasMaxLength(1000).IsUnicode(false);
            e.Property(x => x.BannerBlobName).HasMaxLength(255).IsUnicode(false);
            e.HasOne(x => x.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Events_Organiser");
        });

        // ---------- Categories ----------
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryID);
            e.Property(x => x.CategoryName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            e.Property(x => x.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);
            e.Property(x => x.MaxParticipants).HasDefaultValue(100);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Categories_Event");
        });

        // ---------- Routes ----------
        modelBuilder.Entity<RaceRoute>(e =>
        {
            e.ToTable("Routes");
            e.HasKey(x => x.RouteID);
            e.Property(x => x.RouteName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            e.Property(x => x.ElevationGainM).HasPrecision(6, 2);
            e.Property(x => x.MapUrl).HasMaxLength(255).IsUnicode(false);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Routes)
                .HasForeignKey(x => x.EventID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Routes_Event");
        });

        // ---------- Enrolments ----------
        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Confirmed','Cancelled')"));
            e.HasKey(x => x.EnrolmentID);
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(EnrolmentStatuses.Confirmed);
            e.HasOne(x => x.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Enrolments_Participant");
            e.HasOne(x => x.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Enrolments_Category");
            e.HasIndex(x => new { x.ParticipantID, x.CategoryID })
                .IsUnique()
                .HasDatabaseName("UQ_Enrolments_Participant_Category");
        });

        // ---------- Results (one-to-one with Enrolments) ----------
        modelBuilder.Entity<RaceResult>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Finished','DNF','DQ')"));
            e.HasKey(x => x.ResultID);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(ResultStatuses.Finished);
            e.HasOne(x => x.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<RaceResult>(x => x.EnrolmentID)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Results_Enrolment");
        });
    }
}
//ST10486532
//Author: Gift Tshwaane
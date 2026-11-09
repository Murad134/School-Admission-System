using Microsoft.EntityFrameworkCore;
using SchoolAdmissionSystem.Api.Models;

namespace SchoolAdmissionSystem.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<SeatHold> Holds => Set<SeatHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("classes");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                .HasMaxLength(50);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.TotalSeats)
                .IsRequired();

            entity.Property(c => c.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(c => c.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.HasMany(c => c.Holds)
                .WithOne(h => h.Class)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.Name)
                .IsUnique()
                .HasDatabaseName("idx_classes_name_unique");
        });

        modelBuilder.Entity<SeatHold>(entity =>
        {
            entity.ToTable("holds");
            entity.HasKey(h => h.Id);

            entity.Property(h => h.Id)
                .HasMaxLength(50);

            entity.Property(h => h.ClassId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(h => h.StudentId)
                .IsRequired()
                .HasMaxLength(17);

            entity.Property(h => h.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(h => h.ExpiresAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(h => h.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(h => h.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.HasIndex(h => new { h.ClassId, h.Status, h.ExpiresAt })
                .HasDatabaseName("idx_holds_class_status_expires");

            entity.HasIndex(h => new { h.StudentId, h.CreatedAt })
                .HasDatabaseName("idx_holds_student_id");
        });
    }
}


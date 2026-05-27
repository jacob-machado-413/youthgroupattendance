using Microsoft.EntityFrameworkCore;
using YouthGroupAttendance.Api.Models;

namespace YouthGroupAttendance.Api.Data;

public class YouthGroupContext : DbContext
{
    public YouthGroupContext(DbContextOptions<YouthGroupContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.FullName)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.GraduationYear)
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.Gender)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.School)
                  .HasMaxLength(100);

            // Index on FullName for quick lookup of returning students
            entity.HasIndex(e => e.FullName);
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("Attendances");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Date)
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.EventType)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(50);

            entity.Property(e => e.Notes)
                  .HasMaxLength(500);

            entity.HasOne(e => e.Student)
                  .WithMany(s => s.Attendances)
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Index for querying attendance by date and by student
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.StudentId);
        });
    }
}

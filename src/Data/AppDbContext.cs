using ASP_FINAL_PROJECT.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ASP_FINAL_PROJECT.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Lesson> Lessons => Set<Lesson>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Lesson>(entity =>
        {
            entity.HasKey(lesson => lesson.Id).HasName("lesson_pk");
            entity.Property(lesson => lesson.Name).HasMaxLength(160).IsRequired();
            entity.Property(lesson => lesson.Description).HasMaxLength(4000).IsRequired();
            entity.Property(lesson => lesson.Address).HasMaxLength(300).IsRequired();
            entity.Property(lesson => lesson.Capacity).IsRequired();
            entity.Property(lesson => lesson.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(lesson => new { lesson.Status, lesson.StartDate });
        });
    }
}

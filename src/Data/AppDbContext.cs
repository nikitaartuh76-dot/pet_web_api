using ASP_FINAL_PROJECT.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ASP_FINAL_PROJECT.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
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
            entity.HasOne<ApplicationUser>(l => l.Creator)
                .WithMany()
                .HasForeignKey(lesson => lesson.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>(l => l.Host)
                .WithMany()
                .HasForeignKey(lesson => lesson.HostId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.TokenHash);
            entity.Property(token => token.TokenHash).HasMaxLength(64);

            entity.HasOne<ApplicationUser>(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId);
        });
    }
}

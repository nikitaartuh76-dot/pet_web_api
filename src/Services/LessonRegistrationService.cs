using System.Data;
using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.Entities;
using Microsoft.EntityFrameworkCore;

namespace ASP_FINAL_PROJECT.Services;

public sealed class LessonRegistrationService(AppDbContext dbContext)
{
    public async Task RegisterAsync(Guid lessonId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var lesson = await dbContext.Lessons.FromSqlInterpolated($"SELECT * FROM \"Lessons\" WHERE \"Id\" = {lessonId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lesson is null) throw new KeyNotFoundException("Lesson not found.");
        if (lesson.Status != LessonStatus.Published || lesson.StartDate <= DateTime.UtcNow)
            throw new InvalidOperationException("Registration is unavailable for this lesson.");
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            throw new KeyNotFoundException("User not found.");

        var registration = await dbContext.LessonRegistrations.FindAsync(new object[] { lessonId, userId }, cancellationToken);
        if (registration?.Status == RegistrationStatus.Registered)
            throw new InvalidOperationException("User is already registered.");
        var count = await dbContext.LessonRegistrations.CountAsync(item =>
            item.LessonId == lessonId && item.Status == RegistrationStatus.Registered, cancellationToken);
        if (count >= lesson.Capacity) throw new InvalidOperationException("Lesson is full.");

        if (registration is null)
        {
            registration = new LessonRegistry { LessonId = lessonId, UserId = userId };
            dbContext.LessonRegistrations.Add(registration);
        }
        registration.Status = RegistrationStatus.Registered;
        registration.RegisteredAt = DateTime.UtcNow;
        registration.CancelledAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CancelRegistrationAsync(Guid lessonId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var lesson = await dbContext.Lessons.FromSqlInterpolated($"SELECT * FROM \"Lessons\" WHERE \"Id\" = {lessonId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (lesson is null) throw new KeyNotFoundException("Lesson not found.");
        var registration = await dbContext.LessonRegistrations.FindAsync(new object[] { lessonId, userId }, cancellationToken);
        if (registration is null) throw new KeyNotFoundException("Registration not found.");
        if (registration.Status != RegistrationStatus.Registered)
            throw new InvalidOperationException("Registration is already cancelled.");
        registration.Status = RegistrationStatus.Cancelled;
        registration.CancelledAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<List<LessonRegistry>> GetUserRegistrationsAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return dbContext.LessonRegistrations.AsNoTracking().Include(item => item.Lesson)
            .Where(item => item.UserId == userId).OrderBy(item => item.Lesson.StartDate)
            .ToListAsync(cancellationToken);
    }
    public Task<List<ApplicationUser>> GetAllUsersFromLesson(Guid lessonId, CancellationToken cancellationToken)
    {
        return dbContext.LessonRegistrations.AsNoTracking()
            .Where(registration => registration.LessonId == lessonId && registration.Status == RegistrationStatus.Registered)
            .Select(registration => registration.User)
            .ToListAsync(cancellationToken);
    }
}

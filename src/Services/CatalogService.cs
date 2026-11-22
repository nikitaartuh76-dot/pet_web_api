using System.Diagnostics;
using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.Metrics;
using Microsoft.EntityFrameworkCore;

namespace ASP_FINAL_PROJECT.Services;

public sealed class CatalogService(AppDbContext dbContext, CatalogDbMetrics metrics, ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<IReadOnlyList<Lesson>> GetLessonsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        IQueryable<Lesson> query = dbContext.Lessons.AsNoTracking();

     
        if (from.HasValue)
        {
            query = query.Where(lesson => lesson.StartDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(lesson => lesson.StartDate < to.Value);
        }
        var started = Stopwatch.GetTimestamp();
        var lessons = await query
            .Where(lesson => lesson.Status == LessonStatus.Published)
            .OrderBy(lesson => lesson.StartDate)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started);
        logger.LogInformation("Запрос в PostgreSQL занял {EllapsedTime} мс", elapsed.TotalMilliseconds);
        metrics.CountListRead();
        metrics.RecordListReadDuration(elapsed);
        return lessons;
    }
    public Task<Lesson?> GetLessonForManagementAsync(Guid id, CancellationToken token)
    {
        return dbContext.Lessons.FirstOrDefaultAsync(l => l.Id == id, token);
        
    }
    public Task<Lesson?> GetLessonAsync(Guid id, CancellationToken cancellationToken)
    {
        
        return dbContext.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                lesson => lesson.Id == id && lesson.Status == LessonStatus.Published,
                cancellationToken);
    }

    public async Task<Lesson> CreateLessonAsync(
        PostLessonDTO request, string creatorId,
        CancellationToken cancellationToken)
    {
        ValidateSchedule(request.StartDate, request.EndDate, request.Capacity);

        var lesson = new Lesson
        {
            CreatorId = creatorId,
            HostId = request.HostId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Address = request.Address.Trim(),
            Capacity = request.Capacity,
            Status = LessonStatus.Draft,
            CreationDate = DateTime.UtcNow
        };

        dbContext.Lessons.Add(lesson);
        await dbContext.SaveChangesAsync(cancellationToken);

        return lesson;
    }

    public async Task<Lesson?> UpdateLessonAsync(
        Guid id,
        UpdateLessonDTO request,
        CancellationToken cancellationToken)
    {
        ValidateSchedule(request.StartDate, request.EndDate, request.Capacity);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lesson = await dbContext.Lessons
            .FromSqlInterpolated($"SELECT * FROM \"Lessons\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (lesson is null || lesson.Status == LessonStatus.Cancelled)
        {
            return null;
        }

        var registeredCount = await dbContext.LessonRegistrations.CountAsync(item =>
            item.LessonId == id && item.Status == RegistrationStatus.Registered, cancellationToken);
        if (request.Capacity < registeredCount)
            throw new ArgumentException("Capacity cannot be less than the number of registered users.");

        lesson.Name = request.Name.Trim();
        lesson.Description = request.Description.Trim();
        lesson.StartDate = request.StartDate;
        lesson.EndDate = request.EndDate;
        lesson.Address = request.Address.Trim();
        lesson.Capacity = request.Capacity;
        lesson.LastModifiedDate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return lesson;
    }

    public async Task<bool> DeleteLessonAsync(Guid id, CancellationToken cancellationToken)
    {
        var lesson = await dbContext.Lessons
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (lesson is null)
        {
            return false;
        }

        dbContext.Lessons.Remove(lesson);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Lesson> ChangeStatusAsync(
        Lesson lesson,
        LessonStatus status,
        CancellationToken cancellationToken)
    {
        var canTransition = lesson.Status == LessonStatus.Draft
            && status is LessonStatus.Published or LessonStatus.Cancelled
            || lesson.Status == LessonStatus.Published
            && status == LessonStatus.Cancelled;

        if (!canTransition)
        {
            throw new InvalidOperationException(
                $"Cannot change lesson status from {lesson.Status} to {status}.");
        }

        if (status == LessonStatus.Published && lesson.StartDate <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("A lesson can only be published before it starts.");
        }

        lesson.Status = status;
        lesson.LastModifiedDate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return lesson;
    }

    private static void ValidateSchedule(DateTime startDate, DateTime endDate, int capacity)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("EndDate must be later than StartDate.");
        }

        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.");
        }
    }
}


using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;
using Microsoft.EntityFrameworkCore;

namespace ASP_FINAL_PROJECT.Services;

public sealed class CatalogService(AppDbContext dbContext) : ICatalogService
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

        return await query
            .Where(lesson => lesson.Status == LessonStatus.Published)
            .OrderBy(lesson => lesson.StartDate)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Lesson?> GetLessonAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(
                lesson => lesson.Id == id && lesson.Status == LessonStatus.Published,
                cancellationToken);
    }

    public async Task<Lesson> CreateLessonAsync(
        PostLessonDTO request,
        CancellationToken cancellationToken)
    {
        ValidateSchedule(request.StartDate, request.EndDate, request.Capacity);

        var lesson = new Lesson
        {
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
        int id,
        UpdateLessonDTO request,
        CancellationToken cancellationToken)
    {
        ValidateSchedule(request.StartDate, request.EndDate, request.Capacity);

        var lesson = await dbContext.Lessons
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (lesson is null || lesson.Status == LessonStatus.Cancelled)
        {
            return null;
        }

        lesson.Name = request.Name.Trim();
        lesson.Description = request.Description.Trim();
        lesson.StartDate = request.StartDate;
        lesson.EndDate = request.EndDate;
        lesson.Address = request.Address.Trim();
        lesson.Capacity = request.Capacity;
        lesson.LastModifiedDate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return lesson;
    }

    public async Task<bool> DeleteLessonAsync(int id, CancellationToken cancellationToken)
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

    public async Task<Lesson?> ChangeStatusAsync(
        int id,
        LessonStatus status,
        CancellationToken cancellationToken)
    {
        var lesson = await dbContext.Lessons
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (lesson is null)
        {
            return null;
        }

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


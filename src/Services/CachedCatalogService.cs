using ASP_FINAL_PROJECT.DTOS;
using Microsoft.Extensions.Caching.Hybrid;

namespace ASP_FINAL_PROJECT.Services;

public sealed class CachedCatalogService(CatalogService inner, HybridCache cache) : ICatalogService
{
    private const string LessonListTag = "catalog:lessons:list";

    public async Task<Lesson?> GetLessonAsync(Guid id, CancellationToken cancellationToken)
    {

        return await cache.GetOrCreateAsync<Guid, Lesson?>(
            $"catalog:lessons:{id}", id,
            async (lessonId, factoryToken) => await inner.GetLessonAsync(lessonId, factoryToken),
            cancellationToken: cancellationToken);
    }

    public Task<Lesson?> GetLessonForManagementAsync(Guid id, CancellationToken cancellationToken) =>
        inner.GetLessonForManagementAsync(id, cancellationToken);

    public async Task<IReadOnlyList<Lesson>?> GetLessonsAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var key = $"catalog:lessons:list:{from?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "null"}:{to?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "null"}";
        var lessons = await cache.GetOrCreateAsync<IReadOnlyList<Lesson>?>(key, async (cancellationToken) =>
        {
            var lessons = await inner.GetLessonsAsync(from, to, cancellationToken);
            return lessons;
        }, cancellationToken: cancellationToken, tags: [LessonListTag]);
        return lessons;
    }


    public async Task<Lesson> CreateLessonAsync(PostLessonDTO request, string creatorId, CancellationToken cancellationToken)
    {
        var lesson = await inner.CreateLessonAsync(request, creatorId, cancellationToken);
        await cache.RemoveAsync($"catalog:lessons:{lesson.Id}", CancellationToken.None);
        return lesson;
    }

    public async Task<Lesson?> UpdateLessonAsync(Guid id, UpdateLessonDTO request, CancellationToken cancellationToken)
    {
        var lesson = await inner.UpdateLessonAsync(id, request, cancellationToken);
        if (lesson is not null)
        {
            await cache.RemoveAsync($"catalog:lessons:{id}", CancellationToken.None);
            await cache.RemoveByTagAsync(LessonListTag);
        }
        return lesson;
    }

    public async Task<bool> DeleteLessonAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await inner.DeleteLessonAsync(id, cancellationToken);
        if (deleted)
        {
            await cache.RemoveAsync($"catalog:lessons:{id}", CancellationToken.None);
            await cache.RemoveByTagAsync(LessonListTag);
            
        }
        return deleted;
    }

    public async Task<Lesson> ChangeStatusAsync(Lesson lesson, LessonStatus status, CancellationToken cancellationToken)
    {
        var updated = await inner.ChangeStatusAsync(lesson, status, cancellationToken);
        await cache.RemoveAsync($"catalog:lessons:{updated.Id}", CancellationToken.None);
        await cache.RemoveByTagAsync(LessonListTag);
        return updated;
    }
}

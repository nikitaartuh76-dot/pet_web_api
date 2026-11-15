using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;

namespace ASP_FINAL_PROJECT.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<Lesson>> GetLessonsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);

    Task<Lesson?> GetLessonAsync(Guid id, CancellationToken cancellationToken);

    Task<Lesson> CreateLessonAsync(PostLessonDTO request, string creatorId, CancellationToken cancellationToken);

    Task<Lesson?> UpdateLessonAsync(Guid id, UpdateLessonDTO request, CancellationToken cancellationToken);

    Task<bool> DeleteLessonAsync(Guid id, CancellationToken cancellationToken);
    Task<Lesson?> GetLessonForManagementAsync(Guid id, CancellationToken cancellationToken);

    Task<Lesson> ChangeStatusAsync(Lesson lesson, LessonStatus status, CancellationToken cancellationToken);
}

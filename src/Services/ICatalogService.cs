using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;

namespace ASP_FINAL_PROJECT.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<Lesson>> GetLessonsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);

    Task<Lesson?> GetLessonAsync(int id, CancellationToken cancellationToken);

    Task<Lesson> CreateLessonAsync(PostLessonDTO request, CancellationToken cancellationToken);

    Task<Lesson?> UpdateLessonAsync(int id, UpdateLessonDTO request, CancellationToken cancellationToken);

    Task<bool> DeleteLessonAsync(int id, CancellationToken cancellationToken);

    Task<Lesson?> ChangeStatusAsync(int id, LessonStatus status, CancellationToken cancellationToken);
}

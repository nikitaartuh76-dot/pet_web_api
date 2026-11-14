using ASP_FINAL_PROJECT.Entities;

namespace ASP_FINAL_PROJECT.DTOS;

public sealed record PostLessonDTO(
    int HostId,
    string Name,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    string Address,
    int Capacity);

public sealed record UpdateLessonDTO(
    string Name,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    string Address,
    int Capacity);

public sealed record ChangeLessonStatusDTO(LessonStatus Status);

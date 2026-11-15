using Microsoft.AspNetCore.Authentication;

namespace ASP_FINAL_PROJECT.Entities;

public class Lesson
{
    public Guid Id { get; set; }
    public string CreatorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public LessonStatus Status { get; set; } = LessonStatus.Draft;
    public string HostId { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public ApplicationUser Creator { get; set; }
    public ApplicationUser Host { get; set; }
}

public enum LessonStatus
{
    Draft,
    Published,
    Cancelled
}

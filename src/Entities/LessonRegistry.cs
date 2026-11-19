namespace ASP_FINAL_PROJECT.Entities;

public class LessonRegistry
{
    public Guid LessonId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public RegistrationStatus Status { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

public enum RegistrationStatus
{
    Registered,
    Cancelled
}

using ASP_FINAL_PROJECT.Entities;

namespace ASP_FINAL_PROJECT
{

    public sealed class RefreshToken
    {
        public required string TokenHash { get; init; }
        public required string UserId { get; init; }
        public Guid SessionId { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? UsedAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public ApplicationUser? User { get; set; }
    }
}

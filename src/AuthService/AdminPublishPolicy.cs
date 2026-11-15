using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ASP_FINAL_PROJECT
{
    public class AdminPublishRequirement : IAuthorizationRequirement
    {
    }
    public class AdminPublishHandler : AuthorizationHandler<AdminPublishRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminPublishRequirement requirement)
        {
            if (context.User.HasClaim(ClaimTypes.Role, "Admin"))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}

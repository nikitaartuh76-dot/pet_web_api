using ASP_FINAL_PROJECT.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ASP_FINAL_PROJECT
{
    public class LessonOwnerRequirement : IAuthorizationRequirement
    {


    }

    public class LessonOwnerHandler : AuthorizationHandler<LessonOwnerRequirement, Lesson>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, LessonOwnerRequirement requirement, Lesson resource)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(userId != null && userId == resource.CreatorId)
            {
                context.Succeed(requirement);
            }
           
            return Task.CompletedTask;
        }
    }
}

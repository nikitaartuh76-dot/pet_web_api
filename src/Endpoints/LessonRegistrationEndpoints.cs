using System.Security.Claims;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Authorization;

namespace ASP_FINAL_PROJECT.Endpoints;

public static class LessonRegistrationEndpoints
{
    public static RouteGroupBuilder MapLessonRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/catalog").RequireAuthorization();

        group.MapPost("/lessons/{id:guid}/registrations", async (
            Guid id, ClaimsPrincipal user, LessonRegistrationService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            try
            {
                await service.RegisterAsync(id, userId, cancellationToken);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

        group.MapDelete("/lessons/{id:guid}/registrations/me", async (
            Guid id, ClaimsPrincipal user, LessonRegistrationService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            try
            {
                await service.CancelRegistrationAsync(id, userId, cancellationToken);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

        group.MapGet("/registrations/me", async (
            ClaimsPrincipal user, LessonRegistrationService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            var registrations = await service.GetUserRegistrationsAsync(userId, cancellationToken);
            return Results.Ok(registrations.Select(item => new
            {
                item.LessonId,
                status = item.Status.ToString(),
                item.RegisteredAt,
                item.CancelledAt,
                lesson = new
                {
                    item.Lesson.Name,
                    item.Lesson.Description,
                    item.Lesson.StartDate,
                    item.Lesson.EndDate,
                    item.Lesson.Address,
                    status = item.Lesson.Status.ToString()
                }
            }));
        });

        group.MapGet("/lessons/{id:guid}/registrations", async (IAuthorizationService auth, Guid id, LessonRegistrationService service,
            ClaimsPrincipal user, ICatalogService catalog , CancellationToken token) =>
        {
            var lesson = await catalog.GetLessonForManagementAsync(id, token);
            if (lesson is null) return Results.NotFound();
            var ownerResult = await auth.AuthorizeAsync(user, lesson , "LessonOwner");
            var adminResult = await auth.AuthorizeAsync(user, "AdminPublish");
            if (!ownerResult.Succeeded && !adminResult.Succeeded) return Results.Forbid();

            var registrations = await service.GetAllUsersFromLesson(id, token);
            var users = registrations.Select(registration => new { UserId = registration.Id, UserName = registration.UserName });
            return Results.Ok(users);
        }).RequireAuthorization();
        group.MapDelete("/lessons/{id:guid}/registrations/{userId}", async (Guid id, string userID ,IAuthorizationService auth, LessonRegistrationService service, ClaimsPrincipal user, ICatalogService catalog,
            CancellationToken token) =>
        {
            var lesson = await catalog.GetLessonForManagementAsync(id, token);
            if (lesson is null) return Results.NotFound();
            var ownerResult = await auth.AuthorizeAsync(user, lesson, "LessonOwner");
            var adminResult = await auth.AuthorizeAsync(user, "AdminPublish");
            if (!ownerResult.Succeeded && !adminResult.Succeeded) return Results.Forbid();
            try { 
              await service.CancelRegistrationAsync(id, userID, token);
                return Results.NoContent();
            }
            catch(KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch(InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }).RequireAuthorization();
        return group;
    }
}

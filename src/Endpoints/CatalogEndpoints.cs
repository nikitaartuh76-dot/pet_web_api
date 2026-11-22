using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ASP_FINAL_PROJECT.Endpoints;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/catalog");

        group.MapGet("/lessons", async (
            DateTime? from,
            DateTime? to,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from.Value >= to.Value)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["dateRange"] = ["The 'from' date must be earlier than the 'to' date."]
                });
            }

            var lessons = await catalog.GetLessonsAsync(from, to, cancellationToken);
            return Results.Ok(lessons);
        }).AllowAnonymous();

        group.MapGet("/lessons/{id:guid}", async (
            Guid id,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var lesson = await catalog.GetLessonAsync(id, cancellationToken);
            return lesson is null ? Results.NotFound() : Results.Ok(lesson);
        })
        .WithName("GetLessonById");

        group.MapPost("/lessons", async (
            PostLessonDTO request,
            HttpContext httpContext,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var creatorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(creatorId)) return Results.Unauthorized();

            try
            {
                var lesson = await catalog.CreateLessonAsync(request, creatorId, cancellationToken);
                return Results.CreatedAtRoute(
                    "GetLessonById",
                    new { id = lesson.Id },
                    lesson);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["lesson"] = [exception.Message]
                });
            }
        }).RequireAuthorization("CanPublishLessons");

        group.MapPut("/lessons/{id:guid}", async (
            Guid id,
            UpdateLessonDTO request,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var lesson = await catalog.UpdateLessonAsync(id, request, cancellationToken);
                return lesson is null ? Results.NotFound() : Results.Ok(lesson);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["lesson"] = [exception.Message]
                });
            }
        }).RequireAuthorization("CanPublishLessons");

        group.MapDelete("/lessons/{id:guid}", async (
            Guid id,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var deleted = await catalog.DeleteLessonAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("CanPublishLessons");

        group.MapPatch("/lessons/{id:guid}/status", async (
            Guid id,
            ChangeLessonStatusDTO request,
            ICatalogService catalog,
            CancellationToken cancellationToken, IAuthorizationService auth, HttpContext ctx) =>
        {
            if (!Enum.IsDefined(request.Status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["The requested status is not valid."]
                });
            }
            try
            {
                var lesson = await catalog.GetLessonForManagementAsync(id, cancellationToken);
                if (lesson is null) return Results.NotFound();
                var authResult = await auth.AuthorizeAsync(ctx.User, lesson, "LessonOwner");
                var authResult2 = await auth.AuthorizeAsync(ctx.User, "AdminPublish");
                if (authResult.Succeeded || authResult2.Succeeded)
                {
                    var lessonCh = await catalog.ChangeStatusAsync(lesson, request.Status, cancellationToken);
                    return Results.Ok(lessonCh);
                }
                else
                {
                    return Results.Forbid();
                }
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        }).RequireAuthorization("CanPublishLessons");

        group.MapPatch("/lessons/{id:guid}/publish", async (Guid id, ICatalogService service, CancellationToken token, HttpContext ctx, IAuthorizationService auth) =>
        {
            try
            {
                var lesson = await service.GetLessonForManagementAsync(id, token);
                if (lesson is null)
                {
                    return Results.NotFound();
                }
                else
                {
                    var authResult = await auth.AuthorizeAsync(ctx.User, lesson, "LessonOwner");
                    if (authResult.Succeeded)
                    {
                        await service.ChangeStatusAsync(lesson, LessonStatus.Published, token);
                        return Results.Ok(lesson);
                    }

                    else { return Results.Forbid(); }
                }
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }).RequireAuthorization("CanPublishLessons");

        return group;
    }
}

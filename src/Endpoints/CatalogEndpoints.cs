using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;

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
        });

        group.MapGet("/lessons/{id:int}", async (
            int id,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var lesson = await catalog.GetLessonAsync(id, cancellationToken);
            return lesson is null ? Results.NotFound() : Results.Ok(lesson);
        })
        .WithName("GetLessonById");

        group.MapPost("/lessons", async (
            PostLessonDTO request,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var lesson = await catalog.CreateLessonAsync(request, cancellationToken);
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

        group.MapPut("/lessons/{id:int}", async (
            int id,
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

        group.MapDelete("/lessons/{id:int}", async (
            int id,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var deleted = await catalog.DeleteLessonAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("CanPublishLessons");

        group.MapPatch("/lessons/{id:int}/status", async (
            int id,
            ChangeLessonStatusDTO request,
            ICatalogService catalog,
            CancellationToken cancellationToken) =>
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
                var lesson = await catalog.ChangeStatusAsync(id, request.Status, cancellationToken);
                return lesson is null ? Results.NotFound() : Results.Ok(lesson);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        }).RequireAuthorization("CanPublishLessons");

        group.MapPatch("/lessons/{id:int}/publish", async (int id, ICatalogService service, CancellationToken token) =>
        {
            try
            {
                var lesson = await service.ChangeStatusAsync(id, LessonStatus.Published, token);
                if (lesson is not null)
                {
                   return Results.Ok(lesson);
                }
                else
                {
                   return Results.Problem(detail: "No such lesson in catalog", statusCode: 404);
                }
            }
            catch(InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }).RequireAuthorization("CanPublishLessons");

        return group;
    }
}

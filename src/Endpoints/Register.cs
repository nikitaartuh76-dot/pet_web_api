using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.DTOS;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ASP_FINAL_PROJECT
{
    public static class AuthEndpoints
    {
        static public RouteGroupBuilder MapAuth(this IEndpointRouteBuilder builder)
        {
            var group = builder.MapGroup("/auth");
            group.MapPost("/register", async ([FromBody] PostAuthZDTO user, UserManager<ApplicationUser> manager, EmailService sender) =>
            {

                var userIdent = new ApplicationUser()
                {
                    Email = user.Email,
                    UserName = user.UserName,
                };
                var result = await manager.CreateAsync(userIdent, user.Password);
                if (result.Succeeded)
                {
                    var token = await manager.GenerateEmailConfirmationTokenAsync(userIdent);
                    await sender.SendAsync($"{user.Email}", "Подтверждение учётной записи", $"Токен для подтверждения : {token}");
                    return Results.Ok(new { Message = "Пользователь создан. Проверьте почту для подтверждения email." });
                }
                else
                {
                    var problems = result.Errors
                    .GroupBy(error => error.Code)
                         .ToDictionary(
                         group => group.Key,
                         group => group.Select(error => error.Description).ToArray()
                         );

                    return Results.ValidationProblem(problems);
                }
            });
            group.MapPost("/confirm-email", async (ConfirmEmailDTO dto, UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.FindByEmailAsync(dto.Email);

                if (user != null)
                {
                    var confirmResult = await userManager.ConfirmEmailAsync(user, dto.Token);
                    if (confirmResult.Succeeded)
                    {
                        return Results.Ok("Email успешно подтверждён");
                    }
                    else
                    {
                        var problems = confirmResult.Errors
                         .GroupBy(error => error.Code)
                         .ToDictionary(
                         group => group.Key,
                         group => group.Select(error => error.Description).ToArray()
                         );
                        return Results.ValidationProblem(problems);
                    }
                }
                else
                {
                    return Results.Unauthorized();
                }
            });
            group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
                Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));

            group.MapPost("/login", async (PostAuthZDTO request, UserManager<ApplicationUser> manager,
                SignInManager<ApplicationUser> signInManager, IConfiguration config, HttpContext context, RefreshTokenService service, CancellationToken cancellationToken) =>
            {
                var user = await manager.FindByEmailAsync(request.Email);
                if (user is null) return Results.Unauthorized();
                var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, true);
                if (!result.Succeeded) return Results.Unauthorized();

                var principal = await signInManager.CreateUserPrincipalAsync(user);
                var jwt = service.CreateAccessToken(principal, config);
                var rawToken = await service.CreateRefreshTokenAsync(user.Id, cancellationToken);
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Cookies.Append("access_token", jwt, new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
                    Path = "/", MaxAge = TimeSpan.FromMinutes(15)
                });
                context.Response.Cookies.Append("refresh_token", rawToken, new CookieOptions
                {
                    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
                    Path = "/", MaxAge = TimeSpan.FromDays(7)
                });
                return Results.Ok();
            });

            group.MapPost("/logout", (HttpContext context) =>
            {
                context.Response.Cookies.Delete("access_token", new CookieOptions { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
                return Results.Ok();
            });

            group.MapGet("/me", (HttpContext ctx) =>
            {
                var Claims = ctx.User.Claims.Select(claim => new
                {
                    claim.Type,
                    claim.Value
                });
                return Results.Ok(Claims);
            }).RequireAuthorization();
            group.MapGet("/organizer-check", () =>
            {
                return Results.Ok("Доступ организатора разрешён");
            })
                .RequireAuthorization("OrganizerOnly");

            group.MapPost("/change-password", async (HttpContext ctx, ChangePasswordDTO dto, UserManager<ApplicationUser> manager) =>
            {
                var user = await manager.GetUserAsync(ctx.User);
                if (user is not null)
                {
                    var res = await manager.ChangePasswordAsync(user, dto.OldPass, dto.NewPass);
                    if (res.Succeeded)
                    {
                        ctx.Response.Cookies.Delete("access_token", new CookieOptions { Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
                        return Results.Ok();
                    }
                    else
                    {
                        var problems = res.Errors
                         .GroupBy(error => error.Code)
                         .ToDictionary(
                         group => group.Key,
                         group => group.Select(error => error.Description).ToArray()
                         );
                        return Results.ValidationProblem(problems);
                    }
                }
                return Results.Unauthorized();
            }).RequireAuthorization();

            group.MapPost("/forgot-password", async (ForgotPasswordDTO dto, UserManager<ApplicationUser> userManager, EmailService sender) =>
            {
                var user = await userManager.FindByEmailAsync(dto.Email);
                if (user is not null)
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    await sender.SendAsync(user.Email!, $"Ссылка для сброса пароля", $"Нажмите на ссылку чтобы сбросить старый пароль, {dto.Email} , {token}");
                    return Results.Ok(new { Message = "Если Email существует, проверьте его, там лежит ссылка для сброса пароля" });
                }
                else return Results.Ok(new { Message = "Если Email существует, проверьте его, там лежит ссылка для сброса пароля" });
            });
            group.MapPost("/reset-password", async (ResetPasswordDTO dto, UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.FindByEmailAsync(dto.Email);
                if (user is not null)
                {
                    var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
                    if (result.Succeeded)
                    {
                        return Results.Ok(new
                        {
                            Message = "Пароль успешно сброшен",
                            Email = dto.Email
                        });
                    }
                    else
                    {
                        var problems = result.Errors
                         .GroupBy(error => error.Code)
                         .ToDictionary(
                         group => group.Key,
                         group => group.Select(error => error.Description).ToArray()
                         );
                        return Results.ValidationProblem(problems);
                    }
                }
                else
                {
                    return Results.Conflict("Учётной записи не существует");
                }
            });
            group.MapPost("/refresh", async (HttpContext ctx, RefreshTokenDTO token, CancellationToken cancellationToken, RefreshTokenService service, IConfiguration config) =>
            {
                await service.MarkTokenUsedAsync(token.Token);
                ctx.Response.Cookies.Delete("refresh_token");
                ctx.Response.Cookies.Delete("access_token");
                var accessToken = service.CreateAccessToken(ctx.User, config);
                var refreshToken = await service.CreateRefreshTokenAsync(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!,cancellationToken);
                ctx.Response.Cookies.Append("access_token", accessToken);
                ctx.Response.Cookies.Append("refresh_token", refreshToken);
                return Results.Ok();

            }).RequireAuthorization();
            return group;
        }

    }
}

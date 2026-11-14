using ASP_FINAL_PROJECT.DTOS;
using ASP_FINAL_PROJECT.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
            group.MapPost("/login", async (PostAuthZDTO user, UserManager<ApplicationUser> manager,
                HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
            {
                var userApp = await manager.FindByEmailAsync(user.Email);
                if (userApp != null)
                {
                    signInManager.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    var res = await signInManager.PasswordSignInAsync(userApp, user.Password, false, true);
                    if (res.Succeeded)
                    {
                        return Results.Ok($"{user.UserName} успешно аутентифицирован!");
                    }
                    if (res.IsLockedOut)
                    {
                        return Results.BadRequest("Ты потратил все попытки входа");
                    }
                    if (res.IsNotAllowed)
                    {
                        return Results.Problem(statusCode: 401, detail: "Email не подтверждён");
                    }
                    else 
                    {
                        return Results.Problem("Неверный пароль", statusCode: 401);
                    }
                }
                return Results.Unauthorized();
            });

            group.MapPost("/logout", async (HttpContext ctx) =>
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Ok("Вы вышли из акка");
            });

            group.MapGet("/me", (HttpContext ctx) =>
            {
                var Claims = ctx.User.Claims.Select(claim => new
                {
                    claim.Type,
                    claim.Value
                });
                return Results.Ok(Claims);
            });
            group.MapGet("/organizer-check", () =>
            {
                return Results.Ok("Доступ организатора разрешён");
            })
                .RequireAuthorization("OrganizerOnly");

            group.MapPost("/change-password", async (HttpContext ctx, ChangePasswordDTO dto, SignInManager<ApplicationUser> sign, UserManager<ApplicationUser> manager) =>
            {
                var user = await manager.GetUserAsync(ctx.User);
                if (user is not null)
                {
                    var res = await manager.ChangePasswordAsync(user, dto.OldPass, dto.NewPass);
                    if (res.Succeeded)
                    {
                        sign.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        await sign.SignInAsync(user, false);
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
                    return Results.Ok(new { Message = "Если Email существует, проверьте его, там лежит ссылка для сброса пароля"});
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
            return group;
        }

    }
}

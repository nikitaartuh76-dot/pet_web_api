using ASP_FINAL_PROJECT;
using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.Endpoints;
using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Configure ConnectionStrings:DefaultConnection with a PostgreSQL connection string.");
builder.Services
    .AddIdentityCore<ApplicationUser>(identityOpts =>
    {
        identityOpts.User.RequireUniqueEmail = true;
        identityOpts.Lockout.AllowedForNewUsers = true;
        identityOpts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);
        identityOpts.Lockout.MaxFailedAccessAttempts = 4;
        identityOpts.SignIn.RequireConfirmedEmail = false;
        identityOpts.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<EmailService>();
builder.Host.UseSerilog((ctx, config) =>
{
    config.ReadFrom.Configuration(ctx.Configuration.GetSection("Serilog"));
});
builder.Services.AddAuthentication(AuthOpts =>
{
    AuthOpts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;

}).AddCookie(opts =>
{
    opts.Events.OnValidatePrincipal = async context =>
    {
        var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
        var principal = context.Principal;
        var user = await signInManager.ValidateSecurityStampAsync(principal);
        if (user is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("Cookies");
        }


    };
});


builder.Services.AddAuthorization(authOpts =>
{
    authOpts.AddPolicy("AdminOnly", policyBuilder =>
    {
        policyBuilder.RequireRole("Administrator");
    });
    authOpts.AddPolicy("CanPublishLessons", policy =>
    {
        policy.RequireClaim("permission", "lessons-publish");
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateAsyncScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var ivan = await userManager.FindByEmailAsync("ivan_test@example.com");
    await userManager.AddToRoleAsync(ivan, "Organizer");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapCatalogEndpoints();
app.MapAuth();

app.Run();

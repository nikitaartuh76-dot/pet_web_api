using ASP_FINAL_PROJECT;
using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.Endpoints;
using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
})
.AddJwtBearer(JwtOpts =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"]
        ?? throw new InvalidOperationException(
            "JWT signing key is not configured.");

    JwtOpts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Convert.FromBase64String(signingKey)),

        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        ValidateLifetime = true,
        RequireExpirationTime = true,

        ClockSkew = TimeSpan.Zero
    };
});


builder.Services.AddAuthorization(authOpts =>
{
    authOpts.AddPolicy("CanPublishLessons", policy => policy.RequireClaim("permission", "lessons.publish"));
    authOpts.AddPolicy("LessonOwner", policy =>
    {
        policy.AddRequirements(new LessonOwnerRequirement());
        policy.RequireAuthenticatedUser();
    });
    authOpts.AddPolicy("AdminPublish", policyBuilder =>
    {
        policyBuilder.AddRequirements(new AdminPublishRequirement());
        policyBuilder.RequireAuthenticatedUser();
    });
});
builder.Services.AddScoped<IAuthorizationHandler, LessonOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminPublishHandler>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddOpenApi();

var app = builder.Build();


app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapCatalogEndpoints();
app.MapAuth();

app.Run();

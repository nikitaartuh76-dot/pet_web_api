using ASP_FINAL_PROJECT;
using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.Endpoints;
using ASP_FINAL_PROJECT.Entities;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
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
builder.Services.AddScoped<RefreshTokenService>();
builder.Host.UseSerilog((ctx, config) =>
{
    config.ReadFrom.Configuration(ctx.Configuration.GetSection("Serilog"));
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(JwtOpts =>
{
    var signingKey = builder.Configuration["Jwt:SigningKey"]
        ?? throw new InvalidOperationException(
            "JWT signing key is not configured.");

    JwtOpts.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["access_token"];
            return Task.CompletedTask;
        }
    };
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
    authOpts.AddPolicy("OrganizerOnly", policy => policy.RequireRole("Organizer"));
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
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var protectedRequest = path.StartsWithSegments("/catalog") ||
        path == "/auth/login" || path == "/auth/logout" ||
        path == "/auth/change-password" || path == "/auth/refresh";
    if (protectedRequest && !HttpMethods.IsGet(context.Request.Method) &&
        !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
    }
    await next();
});
app.UseAuthorization();

app.MapOpenApi();
app.MapCatalogEndpoints();
app.MapAuth();

app.Run();

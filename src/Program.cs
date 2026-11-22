using ASP_FINAL_PROJECT;
using ASP_FINAL_PROJECT.Data;
using ASP_FINAL_PROJECT.Endpoints;
using ASP_FINAL_PROJECT.Metrics;
using ASP_FINAL_PROJECT.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
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
var seqUrl = builder.Configuration["SeqConnectionString"];
builder.Host.UseSerilog((ctx, config) =>
{
    config.ReadFrom.Configuration(ctx.Configuration.GetSection("Serilog"));
    config.WriteTo.Console();
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        config.WriteTo.Seq(seqUrl);
    }
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

builder.Services.AddHybridCache(opts =>
{
    opts.DefaultEntryOptions = new HybridCacheEntryOptions()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10),

    };

    opts.ReportTagMetrics = true;

});
var cacheConnectionString = builder.Configuration["CacheConnectionString"];
if (!string.IsNullOrWhiteSpace(cacheConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = cacheConnectionString);
}

builder.Services.AddOpenTelemetry().WithMetrics(builder =>
{
    builder.AddMeter(CatalogDbMetrics.MeterName, "Microsoft.AspNetCore.Hosting").AddPrometheusExporter();

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
builder.Services.AddSingleton<CatalogDbMetrics>();
builder.Services.AddScoped<IAuthorizationHandler, LessonOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminPublishHandler>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<ICatalogService, CachedCatalogService>();
builder.Services.AddScoped<LessonRegistrationService>();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)
    .AddRedis(cacheConnectionString!);
   

var app = builder.Build();

app.UseSerilogRequestLogging();
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

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions()
{
    ResponseWriter =  async (context, response) =>
    {
        var status = response.Status.ToString();
        var checks = response.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Status.ToString());
        await context.Response.WriteAsJsonAsync(new { status = status, checks = checks });
    }
});

app.MapPrometheusScrapingEndpoint();
app.MapOpenApi();
app.MapCatalogEndpoints();
app.MapLessonRegistrationEndpoints();
app.MapAuth();

app.Run();


using Kudos.Api.Auth;
using Kudos.Api.Data;
using Kudos.Api.Models;
using KudosModel = Kudos.Api.Models.Kudos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<KudosOptions>(builder.Configuration.GetSection("Kudos"));
builder.Services.AddSingleton<MongoContext>();

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
            options.DefaultChallengeScheme = TestAuthHandler.Scheme;
        })
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
}
else
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
    .AddJwtBearer(options =>
    {
        var cognito = builder.Configuration.GetSection("Cognito");
        var region = cognito.GetValue<string>("Region");
        var userPoolId = cognito.GetValue<string>("UserPoolId");
        var clientId = cognito.GetValue<string>("ClientId");

        options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
        options.Audience = clientId;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true
        };
    });
}

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var mongoContext = app.Services.GetRequiredService<MongoContext>();
await SeedData.EnsureUsersAsync(mongoContext.Users);
if (app.Environment.IsDevelopment())
{
    await SeedData.EnsureKudosAsync(mongoContext.Kudos, mongoContext.Users);
}

var apiGroup = app.MapGroup("/api");
apiGroup.RequireAuthorization();

apiGroup.MapGet("/users", async (MongoContext context) =>
{
    var users = await context.Users
        .Find(FilterDefinition<User>.Empty)
        .SortBy(user => user.Name)
        .ToListAsync();

    return Results.Ok(users.Select(user => new
    {
        id = user.Id,
        name = user.Name,
        team = user.Team,
        externalId = user.ExternalId
    }));
});

apiGroup.MapGet("/kudos", async (
    MongoContext context,
    IOptions<KudosOptions> options,
    ClaimsPrincipal userPrincipal,
    int? page,
    int? pageSize,
    string? team,
    string? search,
    string? toUserId,
    string? fromUserId) =>
{
    var currentPage = Math.Max(1, page ?? 1);
    var size = Math.Clamp(pageSize ?? 12, 1, 100);

    var filter = Builders<KudosModel>.Filter.Empty;
    var isAdmin = IsAdmin(userPrincipal);

    if (!string.IsNullOrWhiteSpace(team))
    {
        filter &= Builders<KudosModel>.Filter.Eq(k => k.ToUserTeam, team);
    }

    if (!string.IsNullOrWhiteSpace(toUserId))
    {
        filter &= Builders<KudosModel>.Filter.Eq(k => k.ToUserId, toUserId);
    }

    if (!string.IsNullOrWhiteSpace(fromUserId))
    {
        filter &= Builders<KudosModel>.Filter.Eq(k => k.FromUserId, fromUserId);
    }

    if (!isAdmin)
    {
        filter &= Builders<KudosModel>.Filter.Eq(k => k.IsVisible, true);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var escaped = Regex.Escape(search.Trim());
        var regex = new BsonRegularExpression(escaped, "i");
        var searchFilter = Builders<KudosModel>.Filter.Or(
            Builders<KudosModel>.Filter.Regex(k => k.Message, regex),
            Builders<KudosModel>.Filter.Regex(k => k.ToUserName, regex),
            Builders<KudosModel>.Filter.Regex(k => k.FromUserName, regex)
        );
        filter &= searchFilter;
    }

    var total = await context.Kudos.CountDocumentsAsync(filter);
    var skip = (currentPage - 1) * size;

    var kudos = await context.Kudos
        .Find(filter)
        .SortByDescending(entry => entry.CreatedAt)
        .Skip(skip)
        .Limit(size)
        .ToListAsync();

    return Results.Ok(new
    {
        page = currentPage,
        pageSize = size,
        total,
        dryRun = options.Value.DryRun,
        items = kudos.Select(entry => new
        {
            id = entry.Id,
            toUserId = entry.ToUserId,
            toUserName = entry.ToUserName,
            toUserTeam = entry.ToUserTeam,
            fromUserId = entry.FromUserId,
            fromUserName = entry.FromUserName,
            fromUserTeam = entry.FromUserTeam,
            message = entry.Message,
            createdAt = entry.CreatedAt,
            isVisible = entry.IsVisible,
            moderatedBy = entry.ModeratedBy,
            moderatedAt = entry.ModeratedAt,
            moderationReason = entry.ModerationReason
        })
    });
});

apiGroup.MapPost("/kudos", async (
    KudosCreateRequest request,
    ClaimsPrincipal userPrincipal,
    MongoContext context,
    IOptions<KudosOptions> options) =>
{
    if (string.IsNullOrWhiteSpace(request.ToUserId) ||
        string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Recipient and message are required." });
    }

    if (request.Message.Length > 240)
    {
        return Results.BadRequest(new { error = "Message must be 240 characters or less." });
    }

    var externalId = GetExternalId(userPrincipal);

    if (string.IsNullOrWhiteSpace(externalId))
    {
        return Results.Unauthorized();
    }

    var displayName = userPrincipal.FindFirstValue("name") ??
        userPrincipal.Identity?.Name ??
        "Unknown User";
    var email = userPrincipal.FindFirstValue("preferred_username") ?? string.Empty;

    var fromUser = await context.Users
        .Find(u => u.ExternalId == externalId)
        .FirstOrDefaultAsync();

    var isDryRun = options.Value.DryRun;

    if (fromUser is null)
    {
        var newUser = new User
        {
            Name = displayName,
            Team = "Unassigned",
            ExternalId = externalId
        };

        if (!isDryRun)
        {
            await context.Users.InsertOneAsync(newUser);
        }
        else
        {
            newUser.Id = externalId;
        }

        fromUser = newUser;
    }

    var toUser = await context.Users
        .Find(u => u.Id == request.ToUserId)
        .FirstOrDefaultAsync();

    if (toUser is null)
    {
        return Results.NotFound(new { error = "User not found." });
    }

    var kudos = new KudosModel
    {
        ToUserId = toUser.Id,
        ToUserName = toUser.Name,
        ToUserTeam = toUser.Team,
        FromUserId = fromUser.Id,
        FromUserName = string.IsNullOrWhiteSpace(fromUser.Name) ? email : fromUser.Name,
        FromUserTeam = fromUser.Team,
        Message = request.Message.Trim(),
        CreatedAt = DateTime.UtcNow,
        IsVisible = true
    };

    if (!isDryRun)
    {
        await context.Kudos.InsertOneAsync(kudos);
    }
    else
    {
        kudos.Id = "dry-run";
    }

    return Results.Ok(new
    {
        id = kudos.Id,
        toUserId = kudos.ToUserId,
        toUserName = kudos.ToUserName,
        toUserTeam = kudos.ToUserTeam,
        fromUserId = kudos.FromUserId,
        fromUserName = kudos.FromUserName,
        fromUserTeam = kudos.FromUserTeam,
        message = kudos.Message,
        createdAt = kudos.CreatedAt,
        isVisible = kudos.IsVisible,
        moderatedBy = kudos.ModeratedBy,
        moderatedAt = kudos.ModeratedAt,
        moderationReason = kudos.ModerationReason
    });
});

apiGroup.MapPatch("/kudos/{id}/visibility", async (
    string id,
    KudosModerationRequest request,
    ClaimsPrincipal userPrincipal,
    MongoContext context,
    IOptions<KudosOptions> options) =>
{
    if (!IsAdmin(userPrincipal))
    {
        return Results.Forbid();
    }

    var externalId = GetExternalId(userPrincipal);
    if (string.IsNullOrWhiteSpace(externalId))
    {
        return Results.Unauthorized();
    }

    var kudos = await context.Kudos.Find(k => k.Id == id).FirstOrDefaultAsync();
    if (kudos is null)
    {
        return Results.NotFound(new { error = "Kudos not found." });
    }

    var now = DateTime.UtcNow;
    kudos.IsVisible = request.IsVisible;
    kudos.ModeratedBy = externalId;
    kudos.ModeratedAt = now;
    kudos.ModerationReason = request.Reason;

    if (!options.Value.DryRun)
    {
        var update = Builders<KudosModel>.Update
            .Set(k => k.IsVisible, request.IsVisible)
            .Set(k => k.ModeratedBy, externalId)
            .Set(k => k.ModeratedAt, now)
            .Set(k => k.ModerationReason, request.Reason);

        await context.Kudos.UpdateOneAsync(k => k.Id == id, update);
    }

    return Results.Ok(new
    {
        id = kudos.Id,
        isVisible = kudos.IsVisible,
        moderatedBy = kudos.ModeratedBy,
        moderatedAt = kudos.ModeratedAt,
        moderationReason = kudos.ModerationReason,
        dryRun = options.Value.DryRun
    });
});

apiGroup.MapDelete("/kudos/{id}", async (
    string id,
    ClaimsPrincipal userPrincipal,
    MongoContext context,
    IOptions<KudosOptions> options) =>
{
    if (!IsAdmin(userPrincipal))
    {
        return Results.Forbid();
    }

    var existing = await context.Kudos.Find(k => k.Id == id).FirstOrDefaultAsync();
    if (existing is null)
    {
        return Results.NotFound(new { error = "Kudos not found." });
    }

    if (!options.Value.DryRun)
    {
        await context.Kudos.DeleteOneAsync(k => k.Id == id);
    }

    return Results.Ok(new { id, deleted = true, dryRun = options.Value.DryRun });
});

app.Run("http://localhost:5000");

static bool IsAdmin(ClaimsPrincipal user)
{
    var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
        .Concat(user.FindAll("roles").Select(c => c.Value))
        .Concat(user.FindAll("role").Select(c => c.Value))
        .Concat(user.FindAll("cognito:groups").Select(c => c.Value));

    return roles.Any(role =>
        string.Equals(role, "KudosAdmin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
}

static string? GetExternalId(ClaimsPrincipal user) =>
    user.FindFirstValue("oid") ??
    user.FindFirstValue(ClaimTypes.NameIdentifier) ??
    user.FindFirstValue("sub");

public partial class Program { }

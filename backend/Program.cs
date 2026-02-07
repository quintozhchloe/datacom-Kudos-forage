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
            var azureAd = builder.Configuration.GetSection("AzureAd");
            var instance = azureAd.GetValue<string>("Instance");
            var tenantId = azureAd.GetValue<string>("TenantId");
            var audience = azureAd.GetValue<string>("Audience");

            options.Authority = $"{instance}{tenantId}/v2.0";
            options.Audience = audience;
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
            createdAt = entry.CreatedAt
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

    var externalId = userPrincipal.FindFirstValue("oid") ??
        userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ??
        userPrincipal.FindFirstValue("sub");

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
        CreatedAt = DateTime.UtcNow
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
        createdAt = kudos.CreatedAt
    });
});

app.Run("http://localhost:5000");

public partial class Program { }

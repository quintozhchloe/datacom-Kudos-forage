using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Kudos.Api.Tests;

public class KudosEndpointsTests : IClassFixture<KudosApiFactory>, IClassFixture<KudosApiDryRunFactory>
{
    private readonly KudosApiFactory _factory;
    private readonly KudosApiDryRunFactory _dryRunFactory;

    public KudosEndpointsTests(KudosApiFactory factory, KudosApiDryRunFactory dryRunFactory)
    {
        _factory = factory;
        _dryRunFactory = dryRunFactory;
    }

    [Fact]
    public async Task PostKudos_ReturnsUnauthorized_WhenNoAuth()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/kudos", new
        {
            toUserId = "missing",
            message = "Great job"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostKudos_DryRun_ReturnsDryRunId()
    {
        using var client = _dryRunFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "test-user-1");
        client.DefaultRequestHeaders.Add("X-Test-User-Name", "Test User");

        var users = await client.GetFromJsonAsync<List<UserDto>>("/api/users");
        var toUserId = users?.FirstOrDefault()?.Id;

        Assert.False(string.IsNullOrWhiteSpace(toUserId));

        var response = await client.PostAsJsonAsync("/api/kudos", new
        {
            toUserId,
            message = "Thanks for the help!"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<KudosResponse>();
        Assert.NotNull(payload);
        Assert.Equal("dry-run", payload!.Id);
    }

    [Fact]
    public async Task Moderation_HidesKudos_ForNonAdmin()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "user-1");
        client.DefaultRequestHeaders.Add("X-Test-User-Name", "User One");

        var users = await client.GetFromJsonAsync<List<UserDto>>("/api/users");
        var toUserId = users?.FirstOrDefault()?.Id;
        Assert.False(string.IsNullOrWhiteSpace(toUserId));

        var createResponse = await client.PostAsJsonAsync("/api/kudos", new
        {
            toUserId,
            message = "Appreciate the help!"
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<KudosResponse>();
        Assert.NotNull(created);

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-User-Id", "admin-1");
        adminClient.DefaultRequestHeaders.Add("X-Test-User-Name", "Admin User");
        adminClient.DefaultRequestHeaders.Add("X-Test-User-Roles", "KudosAdmin");

        var hideResponse = await adminClient.PatchAsJsonAsync($"/api/kudos/{created!.Id}/visibility", new
        {
            isVisible = false,
            reason = "Inappropriate"
        });
        hideResponse.EnsureSuccessStatusCode();

        var feed = await client.GetFromJsonAsync<KudosFeed>("/api/kudos?page=1&pageSize=10");
        Assert.DoesNotContain(feed!.Items, item => item.Id == created.Id);

        var adminFeed = await adminClient.GetFromJsonAsync<KudosFeed>("/api/kudos?page=1&pageSize=10");
        Assert.Contains(adminFeed!.Items, item => item.Id == created.Id && item.IsVisible == false);
    }

    private sealed record UserDto(string Id, string Name, string Team, string ExternalId);
    private sealed record KudosResponse(
        string Id,
        string ToUserId,
        string ToUserName,
        string ToUserTeam,
        string FromUserId,
        string FromUserName,
        string FromUserTeam,
        string Message,
        DateTime CreatedAt,
        bool IsVisible);

    private sealed record KudosFeed(long Total, List<KudosResponse> Items);
}

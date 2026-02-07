using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Kudos.Api.Tests;

public class KudosEndpointsTests : IClassFixture<KudosApiFactory>
{
    private readonly KudosApiFactory _factory;

    public KudosEndpointsTests(KudosApiFactory factory)
    {
        _factory = factory;
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
        using var client = _factory.CreateClient();
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
        DateTime CreatedAt);
}

using Kudos.Api.Models;
using MongoDB.Driver;

namespace Kudos.Api.Data;

public static class SeedData
{
    public static async Task EnsureUsersAsync(IMongoCollection<User> users)
    {
        var exists = await users.Find(FilterDefinition<User>.Empty).AnyAsync();
        if (exists)
        {
            return;
        }

        var seedUsers = new List<User>
        {
            new() { Name = "Avery Johnson", Team = "Engineering", ExternalId = "" },
            new() { Name = "Jordan Lee", Team = "Product", ExternalId = "" },
            new() { Name = "Riley Patel", Team = "Design", ExternalId = "" },
            new() { Name = "Morgan Chen", Team = "Customer Success", ExternalId = "" },
            new() { Name = "Casey Rivera", Team = "Data", ExternalId = "" }
        };

        await users.InsertManyAsync(seedUsers);
    }
}

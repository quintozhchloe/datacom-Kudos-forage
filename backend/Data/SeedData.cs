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

    public static async Task EnsureKudosAsync(
        IMongoCollection<Kudos> kudos,
        IMongoCollection<User> users)
    {
        var exists = await kudos.Find(FilterDefinition<Kudos>.Empty).AnyAsync();
        if (exists)
        {
            return;
        }

        var userList = await users.Find(FilterDefinition<User>.Empty).ToListAsync();
        if (userList.Count < 2)
        {
            return;
        }

        var first = userList[0];
        var second = userList[1];
        var third = userList.Count > 2 ? userList[2] : userList[0];

        var seedKudos = new List<Kudos>
        {
            new()
            {
                ToUserId = first.Id,
                ToUserName = first.Name,
                ToUserTeam = first.Team,
                FromUserId = second.Id,
                FromUserName = second.Name,
                FromUserTeam = second.Team,
                Message = "Thanks for jumping in to help unblock the release.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-45),
                IsVisible = true
            },
            new()
            {
                ToUserId = second.Id,
                ToUserName = second.Name,
                ToUserTeam = second.Team,
                FromUserId = third.Id,
                FromUserName = third.Name,
                FromUserTeam = third.Team,
                Message = "Great insights during the customer call today!",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                IsVisible = true
            },
            new()
            {
                ToUserId = third.Id,
                ToUserName = third.Name,
                ToUserTeam = third.Team,
                FromUserId = first.Id,
                FromUserName = first.Name,
                FromUserTeam = first.Team,
                Message = "Appreciate the quick turnaround on the dashboard update.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsVisible = true
            }
        };

        await kudos.InsertManyAsync(seedKudos);
    }
}

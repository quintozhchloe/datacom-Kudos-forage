using Kudos.Api.Models;
using KudosModel = Kudos.Api.Models.Kudos;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Kudos.Api.Data;

public class MongoContext
{
    public MongoContext(IOptions<MongoSettings> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.Database);

        Users = database.GetCollection<User>("users");
        Kudos = database.GetCollection<KudosModel>("kudos");
    }

    public IMongoCollection<User> Users { get; }
    public IMongoCollection<KudosModel> Kudos { get; }
}

public class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
}

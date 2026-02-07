using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Mongo2Go;

namespace Kudos.Api.Tests;

public class KudosApiFactory : WebApplicationFactory<Program>
{
    private readonly MongoDbRunner _mongo;

    public KudosApiFactory()
    {
        _mongo = MongoDbRunner.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = _mongo.ConnectionString,
                ["Mongo:Database"] = "kudos-tests",
                ["Kudos:DryRun"] = "false"
            };

            config.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _mongo.Dispose();
        }
    }
}

public class KudosApiDryRunFactory : KudosApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Mongo:Database"] = "kudos-tests-dryrun",
                ["Kudos:DryRun"] = "true"
            };

            config.AddInMemoryCollection(settings);
        });
    }
}

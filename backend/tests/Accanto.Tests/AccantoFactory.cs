using Accanto.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Tests;

public class AccantoFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "accanto-test-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", "accanto-test");
        builder.UseSetting("Jwt:Audience", "accanto-test");
        builder.UseSetting("Jwt:Key", "test-key-very-long-test-key-very-long-1234");
        builder.UseSetting("Jwt:ExpiryMinutes", "60");
        builder.UseSetting("Storage:RootPath", Path.Combine(Path.GetTempPath(), _dbName));
        builder.UseSetting("ConnectionStrings:Postgres", "Host=ignored");
        // Chiave AES-256 deterministica di test (32 zeri in base64). NON usare in produzione.
        builder.UseSetting("Encryption:MasterKey", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

        // Rate limit alti per i test
        builder.UseSetting("RateLimit:Login:PermitLimit", "10000");
        builder.UseSetting("RateLimit:Login:Window", "00:01:00");
        builder.UseSetting("RateLimit:Register:PermitLimit", "10000");
        builder.UseSetting("RateLimit:Register:Window", "00:01:00");
        builder.UseSetting("RateLimit:Sensitive:PermitLimit", "10000");
        builder.UseSetting("RateLimit:Sensitive:Window", "00:01:00");
        builder.UseSetting("RateLimit:InviteCreate:PermitLimit", "10000");
        builder.UseSetting("RateLimit:InviteCreate:Window", "00:01:00");

        builder.ConfigureServices(services =>
        {
            // Remove all EF/DbContext services that the production registration created.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AccantoDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AccantoDbContext>(opt =>
                opt.UseInMemoryDatabase(_dbName));
        });
    }
}

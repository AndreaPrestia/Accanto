using Accanto.Application.Common.Persistence;
using Accanto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Tests;

internal static class TestDb
{
    public static AccantoDbContext Create()
    {
        var opts = new DbContextOptionsBuilder<AccantoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        return new AccantoDbContext(opts, new NullFieldProtector());
    }
}

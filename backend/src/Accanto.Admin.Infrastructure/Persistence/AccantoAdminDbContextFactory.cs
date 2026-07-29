using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accanto.Admin.Infrastructure.Persistence;

/// <summary>
/// Factory design-time per `dotnet ef` (migrations). Permette di generare/applicare
/// le migration senza avviare l'Admin API. La connection string viene letta da
/// <c>ConnectionStrings__AdminDatabase</c>; il fallback e' un Postgres locale innocuo
/// usato solo per la generazione dello schema (le migration non dipendono dal contenuto).
/// </summary>
public class AccantoAdminDbContextFactory : IDesignTimeDbContextFactory<AccantoAdminDbContext>
{
    public AccantoAdminDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__AdminDatabase")
                   ?? "Host=localhost;Port=5432;Database=accanto_admin;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AccantoAdminDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AccantoAdminDbContext(options);
    }
}

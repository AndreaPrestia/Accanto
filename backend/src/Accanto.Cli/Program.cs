using System.Security.Cryptography;
using Accanto.Infrastructure;
using Accanto.Infrastructure.Persistence;
using Accanto.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accanto.Cli;

/// <summary>
/// CLI di amministrazione Accanto. Comandi disponibili:
///
///   accanto generate-key
///     Stampa una nuova chiave AES-256 in base64.
///
///   accanto rotate-keys
///     Riscrive tutti i campi cifrati e tutti i file dei documenti usando la chiave
///     "attiva" (Encryption:ActiveKeyId). Le vecchie chiavi devono restare configurate
///     in Encryption:Keys (oppure Encryption:MasterKey per il formato legacy v1)
///     finche' la rotazione non e' completa.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "generate-key" => GenerateKey(),
                "rotate-keys" => await RotateKeysAsync(rest),
                "erase-user" => await EraseUserAsync(rest),
                "--help" or "-h" or "help" => PrintUsage(),
                _ => UnknownCommand(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Errore: {ex.Message}");
            return 2;
        }
    }

    private static int GenerateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        Console.WriteLine(Convert.ToBase64String(key));
        return 0;
    }

    private static async Task<int> RotateKeysAsync(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddAccantoInfrastructure(config);
        services.AddScoped<KeyRotationService>();

        await using var provider = services.BuildServiceProvider();

        var protector = (AesGcmFieldProtector)provider.GetRequiredService<Accanto.Application.Common.Security.IFieldProtector>();
        if (protector.ActiveKeyId is null)
        {
            Console.Error.WriteLine(
                "Nessuna ActiveKeyId configurata: imposta Encryption:ActiveKeyId e Encryption:Keys "
                + "(la chiave legacy va lasciata in Encryption:MasterKey finche' tutti i dati v1 non sono ruotati).");
            return 3;
        }

        Console.WriteLine($"Avvio rotazione verso la chiave attiva '{protector.ActiveKeyId}'.");
        using var scope = provider.CreateScope();
        // Migrazioni con connection string privilegiata se presente (vedi
        // Accanto.Api/Program.cs per il razionale). La rotazione effettiva
        // dei dati riusa il DbContext registrato (runtime, accanto_app).
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var migratorConn = cfg.GetConnectionString("PostgresMigrator")
                           ?? cfg.GetConnectionString("Postgres");
        var migratorOptions = new DbContextOptionsBuilder<AccantoDbContext>()
            .UseNpgsql(migratorConn)
            .Options;
        await using (var migratorDb = new AccantoDbContext(migratorOptions, protector))
        {
            await migratorDb.Database.MigrateAsync();
        }
        var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();

        var rotator = scope.ServiceProvider.GetRequiredService<KeyRotationService>();
        var report = await rotator.RotateAsync();

        Console.WriteLine("Rotazione completata:");
        Console.WriteLine($"  cerchi di cura      : {report.CareCircles}");
        Console.WriteLine($"  voci di diario      : {report.TimelineEntries}");
        Console.WriteLine($"  domande per medico  : {report.DoctorQuestions}");
        Console.WriteLine($"  aggiornamenti       : {report.SharedUpdates}");
        Console.WriteLine($"  documenti (DB)      : {report.MedicalDocumentRows}");
        Console.WriteLine($"  documenti (file)    : {report.MedicalDocumentFiles}");
        if (report.MissingFiles.Count > 0)
        {
            Console.WriteLine($"  file mancanti       : {report.MissingFiles.Count}");
            foreach (var f in report.MissingFiles)
                Console.WriteLine($"    - {f}");
        }
        return 0;
    }

    /// <summary>
    /// Cancellazione GDPR amministrativa: tombstone dell'utente con
    /// cascade dei documenti (compresa la replica S3 via outbox).
    /// Da usare quando l'utente non puo' usare l'endpoint API
    /// (es. account compromesso, supporto legale).
    /// </summary>
    private static async Task<int> EraseUserAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Manca <userId>. Uso: accanto erase-user <userId> --reason \"...\" [--yes]");
            return 1;
        }

        if (!Guid.TryParse(args[0], out var userId))
        {
            Console.Error.WriteLine($"userId non valido: {args[0]}");
            return 1;
        }

        var reason = ParseFlag(args, "--reason");
        var skipPrompt = args.Any(a => a is "--yes" or "-y");

        if (string.IsNullOrWhiteSpace(reason))
        {
            Console.Error.WriteLine("Specificare --reason \"motivazione\" (richiesto per audit log).");
            return 1;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => Microsoft.Extensions.Logging.ConsoleLoggerExtensions.AddSimpleConsole(b));
        services.AddAccantoInfrastructure(config);
        // Application layer: registra IUserErasureService + IRefreshTokenService.
        Accanto.Application.ApplicationServiceCollectionExtensions.AddAccantoApplication(services);

        await using var provider = services.BuildServiceProvider();

        // Migrazioni con connection string privilegiata (come rotate-keys).
        var migratorConn = config.GetConnectionString("PostgresMigrator")
                           ?? config.GetConnectionString("Postgres");
        var protector = provider.GetRequiredService<Accanto.Application.Common.Security.IFieldProtector>();
        var migratorOptions = new DbContextOptionsBuilder<AccantoDbContext>()
            .UseNpgsql(migratorConn)
            .Options;
        await using (var migratorDb = new AccantoDbContext(migratorOptions, protector))
        {
            await migratorDb.Database.MigrateAsync();
        }

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            Console.Error.WriteLine($"Utente {userId} non trovato.");
            return 4;
        }

        if (user.IsErased)
        {
            Console.WriteLine($"Utente {userId} gia' tombstonato il {user.ErasedAt:O}: nessuna azione.");
            return 0;
        }

        Console.WriteLine($"Stai per cancellare definitivamente l'utente:");
        Console.WriteLine($"  Id           : {user.Id}");
        Console.WriteLine($"  Email        : {user.Email}");
        Console.WriteLine($"  DisplayName  : {user.DisplayName}");
        Console.WriteLine($"  Reason       : {reason}");
        if (!skipPrompt)
        {
            Console.Write("Digita ERASE per confermare: ");
            var typed = Console.ReadLine();
            if (typed != "ERASE")
            {
                Console.WriteLine("Conferma non ricevuta: operazione annullata.");
                return 5;
            }
        }

        var erasure = scope.ServiceProvider.GetRequiredService<Accanto.Application.Account.IUserErasureService>();
        await erasure.EraseAsync(userId, $"Admin CLI: {reason}");

        Console.WriteLine($"Utente {userId} cancellato (tombstone). I blob locali sono stati rimossi e la replica S3 sara' propagata dal worker.");
        return 0;
    }

    private static string? ParseFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.Ordinal))
                return args[i + 1];
        }
        return null;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Comando sconosciuto: {command}");
        PrintUsage();
        return 1;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Uso: accanto <comando>");
        Console.WriteLine();
        Console.WriteLine("Comandi:");
        Console.WriteLine("  generate-key   Stampa una nuova chiave AES-256 in base64.");
        Console.WriteLine("  rotate-keys    Riscrive i dati cifrati usando Encryption:ActiveKeyId.");
        Console.WriteLine("  erase-user     Cancellazione GDPR di un utente (tombstone + cascade documenti).");
        Console.WriteLine("                 Uso: accanto erase-user <userId> --reason \"...\" [--yes]");
        Console.WriteLine("  help           Mostra questo messaggio.");
        return 0;
    }
}

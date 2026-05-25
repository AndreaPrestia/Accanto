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
        var db = scope.ServiceProvider.GetRequiredService<AccantoDbContext>();
        await db.Database.MigrateAsync();

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
        Console.WriteLine("  help           Mostra questo messaggio.");
        return 0;
    }
}

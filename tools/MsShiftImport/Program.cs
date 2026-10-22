using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// Brings MSShift history into a Shore Desk database (phase 4). Reads the parsed files
// under C:\DeskData\import\msshift\parsed (made by tools/msshift-import/*.py) and prints
// COUNTS ONLY - the records hold injuries, birth dates and addresses.
//
//   dotnet run --project tools/MsShiftImport -- --db DeskImportTest --incidents --events
//   dotnet run --project tools/MsShiftImport -- --db DeskImportTest --remove
//
// The live database "Desk" also needs --live (Fadi says when). Every imported row has
// LegacyRef "MSShift ..." so --remove can take the whole import out again.
public static class Cli
{
    public static async Task<int> Main(string[] args)
    {
        var opts = args.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dbName = ArgAfter(args, "--db");
        if (dbName is null || !(opts.Contains("--incidents") || opts.Contains("--events") || opts.Contains("--remove")))
        {
            Console.WriteLine("Usage: MsShiftImport --db <database> (--incidents | --events | --remove) [--live]");
            return 2;
        }
        var live = string.Equals(dbName, "Desk", StringComparison.OrdinalIgnoreCase);
        if (live && !opts.Contains("--live"))
        {
            Console.WriteLine("Refused: \"Desk\" is the live database. Test on a copy first; add --live only when Fadi has said yes.");
            return 2;
        }

        var parsed = ArgAfter(args, "--parsed") ?? @"C:\DeskData\import\msshift\parsed";
        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseSqlServer($@"Server=localhost\SQLEXPRESS;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True")
            .Options;
        await using var db = new DeskDbContext(options);

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            if (live)
            {
                Console.WriteLine($"Refused: the live database is missing {pending.Count} migration(s). Run Publish.ps1 first.");
                return 1;
            }
            Console.WriteLine($"Applying {pending.Count} migration(s) to {dbName}...");
            await db.Database.MigrateAsync();
        }

        if (opts.Contains("--remove"))
        {
            var n = await Importer.RemoveAsync(db);
            Console.WriteLine($"Removed {n.incidents} imported incident reports and {n.events} imported events from {dbName}.");
            return 0;
        }

        var importer = new Importer(db);
        await importer.PrepareAsync();
        if (opts.Contains("--incidents"))
        {
            var result = await new IncidentImport(importer).RunAsync(Path.Combine(parsed, "incidents.json"));
            result.Print();
        }
        if (opts.Contains("--events"))
        {
            var result = await new EventImport(importer).RunAsync(Path.Combine(parsed, "events"));
            result.Print();
        }
        return 0;
    }

    static string? ArgAfter(string[] args, string name)
    {
        var i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}

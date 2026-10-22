using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;

namespace MsShiftImport;

// Photos and PDFs read out of MSShift (C:\DeskData\import\msshift\files, named
// <form>-<report number>-photo1.jpg / -pdf1.pdf) become attachments of the imported
// reports, stored like uploads: <storage>\incidents\<report id>\<random name>.
public class AttachmentImport(Importer imp, string storageRoot)
{
    public class Result
    {
        public int Files, Added, AlreadyThere, NoReport, BadType;

        public void Print() =>
            Console.WriteLine($"Attachments: {Files} files, added {Added}, already attached {AlreadyThere}, " +
                              $"report not imported {NoReport}, unknown type {BadType}");
    }

    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".png"] = "image/png", [".gif"] = "image/gif", [".pdf"] = "application/pdf", [".mp4"] = "video/mp4",
    };

    public async Task<Result> RunAsync(string folder)
    {
        var res = new Result();
        // "MSShift IR 2025-IR-123" -> key "IR-2025-IR-123" as the read-out names the files.
        var reports = await imp.Db.Incidents.Where(r => r.LegacyRef != null && r.LegacyRef.StartsWith(Importer.RefPrefix))
            .Select(r => new { r.Id, r.LegacyRef, r.CreatedUtc }).ToListAsync();
        var byKey = new Dictionary<string, (int Id, DateTime Created)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in reports)
        {
            var parts = r.LegacyRef![Importer.RefPrefix.Length..].Split(' ', 2);
            if (parts.Length == 2) byKey[$"{parts[0]}-{FileKey(parts[1])}"] = (r.Id, r.CreatedUtc);
        }
        var existing = (await imp.Db.IncidentAttachments.Select(a => new { a.IncidentId, a.FileName }).ToListAsync())
            .Select(a => (a.IncidentId, a.FileName)).ToHashSet();

        foreach (var path in Directory.GetFiles(folder).Order())
        {
            var ext = Path.GetExtension(path);
            if (ext == ".jsonl") continue;
            res.Files++;
            if (!Types.TryGetValue(ext, out var contentType)) { res.BadType++; continue; }
            var name = Path.GetFileNameWithoutExtension(path);          // IR-2025-IR-123-photo1
            var cut = name.LastIndexOf('-');
            if (cut < 0 || !byKey.TryGetValue(name[..cut], out var report)) { res.NoReport++; continue; }
            var label = name[(cut + 1)..];                               // photo1 / pdf2
            var fileName = $"MSShift {label}{ext.ToLowerInvariant()}";
            if (!existing.Add((report.Id, fileName))) { res.AlreadyThere++; continue; }

            var stored = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var dir = Path.Combine(storageRoot, "incidents", report.Id.ToString());
            Directory.CreateDirectory(dir);
            File.Copy(path, Path.Combine(dir, stored));
            imp.Db.IncidentAttachments.Add(new IncidentAttachment
            {
                IncidentId = report.Id, FileName = fileName, StoredName = stored, ContentType = contentType,
                SizeBytes = new FileInfo(path).Length, Caption = "From MSShift", UploadedById = imp.ImportUserId,
                UploadedUtc = report.Created,
            });
            res.Added++;
            if (res.Added % 200 == 0) await imp.Db.SaveChangesAsync();
        }
        await imp.Db.SaveChangesAsync();
        return res;
    }

    // Same cleaning as the read-out's file names (letters, digits and dashes only).
    static string FileKey(string number) => new(number.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
}

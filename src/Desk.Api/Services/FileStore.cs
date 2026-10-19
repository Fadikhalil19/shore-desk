using Microsoft.Extensions.Options;

namespace Desk.Api.Services;

// Uploaded files (report photos, videos, documents) under App:StorageRoot
// (C:\DeskData\files). Each file gets a random name on disk; the original name
// is only kept in the database. Only the listed file types are accepted.
public class FileStore(IOptions<AppOptions> options)
{
    static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".gif"] = "image/gif",
        [".webp"] = "image/webp", [".heic"] = "image/heic", [".heif"] = "image/heif",
        [".mp4"] = "video/mp4", [".mov"] = "video/quicktime", [".m4a"] = "audio/mp4",
        [".pdf"] = "application/pdf", [".txt"] = "text/plain",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    public static string AllowedList => string.Join(", ", Types.Keys);

    public static string? ContentTypeFor(string fileName) => Types.GetValueOrDefault(Path.GetExtension(fileName));

    // Shown in the browser (photos, PDFs); everything else is downloaded.
    public static bool ShowInline(string contentType) =>
        contentType is "image/jpeg" or "image/png" or "image/gif" or "image/webp" or "application/pdf" or "video/mp4";

    public async Task<string> SaveAsync(string folder, IFormFile file, CancellationToken ct)
    {
        var stored = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        var dir = Folder(folder);
        Directory.CreateDirectory(dir);
        await using var target = new FileStream(Path.Combine(dir, stored), FileMode.CreateNew, FileAccess.Write);
        await file.CopyToAsync(target, ct);
        return stored;
    }

    public string PathFor(string folder, string storedName)
    {
        if (storedName.Contains('/') || storedName.Contains('\\') || storedName.Contains(".."))
            throw new ArgumentException("Bad stored file name.", nameof(storedName));
        return Path.Combine(Folder(folder), storedName);
    }

    string Folder(string folder) => Path.Combine(options.Value.StorageRoot, folder);
}

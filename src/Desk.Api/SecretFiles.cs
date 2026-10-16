namespace Desk.Api;

// Secrets live in small files outside the repository (see scripts\Set-*.ps1).
public static class SecretFiles
{
    public static string Read(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Secret file missing: {path}. Run the matching script in scripts\\.");
        return File.ReadAllText(path).Trim();
    }
}

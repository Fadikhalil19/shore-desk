namespace Desk.Api;

public class AppOptions
{
    public const string Section = "App";

    // Photos and attachments live here (from phase 2). Never shared.
    public string StorageRoot { get; set; } = @"C:\DeskData\files";

    // Folder holding secret files (client secret). Outside the repo.
    public string SecretsDir { get; set; } = @"C:\DeskData\secrets";

    // Cookie-encryption keys when running as a service.
    public string KeysDir { get; set; } = @"C:\DeskData\keys";

    // Largest upload accepted, in megabytes.
    public int MaxUploadMb { get; set; } = 50;

    // Only accounts with these sign-in domains may use the app.
    public string[] AllowedEmailDomains { get; set; } = [];

    public GroupIds Groups { get; set; } = new();

    // Entra security groups (object IDs). Entra only decides who may sign in;
    // what each person sees is set on the app's access screen.
    public class GroupIds
    {
        public string Users { get; set; } = "";
        public string Admins { get; set; } = "";
    }
}

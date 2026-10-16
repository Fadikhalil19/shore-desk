// Shore Desk scheduler (Windows Service): emails and reminders, separate from the web app.
//   dotnet run --project src\Desk.Scheduler -- --once    one pass, then exit (testing)
using Desk.Core.Data;
using Desk.Scheduler;
using Microsoft.EntityFrameworkCore;

var once = args.Contains("--once");
// Settings are read from next to the program (a service starts in System32).
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args.Where(a => a != "--once").ToArray(),
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddWindowsService(o => o.ServiceName = "ShoreDeskScheduler");

builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection(SchedulerOptions.Section));
builder.Services.AddDbContext<DeskDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Desk")));
builder.Services.AddScoped<MailJob>();
builder.Services.AddSingleton(new RunMode(once));

var mode = builder.Configuration[$"{SchedulerOptions.Section}:Mode"] ?? "Outbox";
if (mode.Equals("Outbox", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IMailSender, FileOutboxSender>();
else if (mode.Equals("Graph", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<IMailSender, GraphMailSender>();
else
    throw new InvalidOperationException($"Unknown mail mode '{mode}'. Use Outbox or Graph.");

builder.Services.AddHostedService<Worker>();
builder.Build().Run();

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Desk.Core.Data;

// Used only by "dotnet ef" (migrations, database update), so it never has to start
// the web app and read its secrets.
public class DesignTimeFactory : IDesignTimeDbContextFactory<DeskDbContext>
{
    public DeskDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DeskDbContext>()
            .UseSqlServer(@"Server=localhost\SQLEXPRESS;Database=Desk;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True")
            .Options);
}

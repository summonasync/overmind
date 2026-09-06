using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Overmind.Infrastructure.Persistence;

public sealed class OvermindDbContextFactory : IDesignTimeDbContextFactory<OvermindDbContext>
{
    public OvermindDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OvermindDbContext>()
            .UseSqlite("Data Source=overmind-design.db")
            .Options;

        return new OvermindDbContext(options);
    }
}

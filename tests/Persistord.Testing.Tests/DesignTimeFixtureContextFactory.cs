using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Persistord.Testing.Tests;

public sealed class DesignTimeFixtureContextFactory : IDesignTimeDbContextFactory<FixtureContext>
{
    public FixtureContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FixtureContext>().UseSqlite("DataSource=design.db").Options);
}

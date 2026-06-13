using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Persistord.Sample;

/// <summary>
/// Lets the EF Core tools (<c>dotnet ef migrations</c>) build <see cref="MyBotContext"/>
/// at design time. The library never selects a provider, so the sample picks SQLite here.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MyBotContext>
{
    public MyBotContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MyBotContext>()
            .UseSqlite("DataSource=sample.db")
            .Options;
        return new MyBotContext(options);
    }
}

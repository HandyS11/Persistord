using Xunit;

namespace Persistord.Testing.Tests;

public class UniqueModelCacheKeyFactoryTests
{
    [Fact]
    public void Create_returns_a_different_key_on_every_call()
    {
        using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        using var context = database.CreateContext<FixtureContext>(o => new FixtureContext(o));
        var factory = new UniqueModelCacheKeyFactory();

        Assert.NotEqual(factory.Create(context, designTime: false), factory.Create(context, designTime: false));
    }

    [Fact]
    public void Create_guards_its_context() =>
        Assert.Throws<ArgumentNullException>("context", () => new UniqueModelCacheKeyFactory().Create(null!, false));
}

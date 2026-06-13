using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.Sample;

var options = new DbContextOptionsBuilder<MyBotContext>()
    .UseSqlite("DataSource=sample.db")
    .Options;

await using var context = new MyBotContext(options);
await context.Database.EnsureCreatedAsync();

context.Guilds.Add(new GuildEntity
{
    Id = 1UL, Name = "Sample Guild", OwnerId = 2UL
});
await context.SaveChangesAsync();

Console.WriteLine($"Guilds persisted: {await context.Guilds.CountAsync()}");

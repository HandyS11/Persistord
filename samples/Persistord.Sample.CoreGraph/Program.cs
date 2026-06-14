using Microsoft.EntityFrameworkCore;
using Persistord.Core.Entities;
using Persistord.Sample.CoreGraph;

// The library never picks a provider; this sample uses SQLite to stay
// self-contained. Any EF Core 10 relational provider works the same way.
var options = new DbContextOptionsBuilder<CoreGraphContext>()
    .UseSqlite("DataSource=coregraph.db")
    .Options;

await using var db = new CoreGraphContext(options);
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

// Discord ids are 64-bit ulong snowflakes. This one is larger than long.MaxValue,
// which proves the bit-faithful ulong <-> long round-trip Persistord applies.
const ulong guildId = 18446744073709551615UL; // ulong.MaxValue
const ulong ownerId = 4242424242424242UL;

db.Guilds.Add(new GuildEntity
{
    Id = guildId, Name = "Showcase Guild", OwnerId = ownerId
});

// A category that parents a text channel, which in turn parents a thread.
const ulong categoryId = 1000UL;
const ulong textChannelId = 1001UL;
const ulong threadId = 1002UL;
db.Channels.AddRange(
    new ChannelEntity
    {
        Id = categoryId, GuildId = guildId, Type = ChannelType.Category, Name = "general"
    },
    new ChannelEntity
    {
        Id = textChannelId,
        GuildId = guildId,
        ParentId = categoryId,
        Type = ChannelType.Text,
        Name = "chat",
    },
    new ChannelEntity
    {
        Id = threadId,
        GuildId = guildId,
        ParentId = textChannelId,
        Type = ChannelType.Thread,
        Name = "a-thread",
    });

// A user, that user's per-guild membership (composite (GuildId, UserId) key), and a role.
db.Users.Add(new UserEntity
{
    Id = ownerId, Username = "owner", GlobalName = "The Owner"
});
db.Members.Add(new MemberEntity
{
    GuildId = guildId, UserId = ownerId, Nickname = "Boss", JoinedAt = DateTimeOffset.UtcNow,
});
db.Roles.Add(new RoleEntity
{
    Id = 2000UL,
    GuildId = guildId,
    Name = "Admin",
    Permissions = 8UL,
    Color = 0xFF0000, // Color is a plain int RGB value — not a snowflake, so no ulong conversion applies.
});

await db.SaveChangesAsync();

// Read back and prove the snowflake survived the round-trip through signed storage.
var guild = await db.Guilds.SingleAsync();
Console.WriteLine(
    $"Guild id round-trip: stored & read back {guild.Id} (matches ulong.MaxValue: {guild.Id == ulong.MaxValue})");
Console.WriteLine($"Channels: {await db.Channels.CountAsync()} (category -> text -> thread)");

var thread = await db.Channels.SingleAsync(c => c.Type == ChannelType.Thread);
Console.WriteLine($"Thread '{thread.Name}' parent id: {thread.ParentId}");

var member = await db.Members.SingleAsync();
Console.WriteLine($"Member (guild {member.GuildId}, user {member.UserId}) nickname: {member.Nickname}");
Console.WriteLine($"Roles: {await db.Roles.CountAsync()}");

using Discord;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;
using Persistord.Sample.DiscordNet;

// The library never picks a provider; this sample uses SQLite to stay
// self-contained. Any EF Core 10 relational provider works the same way.
var options = new DbContextOptionsBuilder<AdapterContext>()
    .UseSqlite("DataSource=adapter.db")
    .Options;

await using var db = new AdapterContext(options);
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

// In a real bot these objects arrive from DiscordSocketClient events (e.g.
// client.MessageReceived). They are interfaces that can't be constructed offline,
// so this sample fakes them with NSubstitute to show the mappers in isolation.
var guild = Substitute.For<IGuild>();
guild.Id.Returns(100UL);
guild.Name.Returns("Showcase Guild");
guild.OwnerId.Returns(200UL);

var user = Substitute.For<IUser>();
user.Id.Returns(200UL);
user.Username.Returns("owner");
user.GlobalName.Returns("The Owner");

var role = Substitute.For<IRole>();
role.Id.Returns(300UL);
role.Guild.Returns(guild);
role.Name.Returns("Admin");
role.Permissions.Returns(new GuildPermissions(8UL));
role.Colors.Returns(RoleColors.Solid(new Color(0xFF0000)));

// Build a faked message carrying an attachment, a reaction and an embed.
var channel = Substitute.For<IMessageChannel>();
channel.Id.Returns(500UL);

var attachment = Substitute.For<IAttachment>();
attachment.Id.Returns(800UL);
attachment.Filename.Returns("changelog.txt");
attachment.Url.Returns("https://cdn/changelog.txt");

var emote = Substitute.For<IEmote>();
emote.Name.Returns("🎉");
object boxedMetadata = new ReactionMetadata();
typeof(ReactionMetadata).GetProperty(nameof(ReactionMetadata.ReactionCount))!
    .SetValue(boxedMetadata, 7);
var reactions = new Dictionary<IEmote, ReactionMetadata> { [emote] = (ReactionMetadata)boxedMetadata };

var embed = new EmbedBuilder()
    .WithTitle("Persistord 1.0")
    .WithDescription("Provider-agnostic Discord persistence.")
    .WithColor(new Color(0x5865F2))
    .WithFooter(f => f.Text = "released today")
    .WithAuthor(a => a.Name = "Persistord")
    .AddField("Modules", "Core, Messages, History", inline: true)
    .Build();

var message = Substitute.For<IMessage>();
message.Id.Returns(9001UL);
message.Channel.Returns(channel);
message.Author.Returns(user);
message.Content.Returns("Check out this release!");
message.EditedTimestamp.Returns((DateTimeOffset?)null);
message.Attachments.Returns([attachment]);
message.Reactions.Returns(reactions);
message.Embeds.Returns([embed]);

// Map the faked Discord.Net types straight to Persistord entities and persist them.
db.Guilds.Add(guild.ToGuildEntity());
db.Users.Add(user.ToUserEntity());
db.Roles.Add(role.ToRoleEntity());
db.Messages.Add(message.ToMessageEntity());
db.MessageHistory.Add(message.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();

// Read back to confirm the mappers produced complete, persistable graphs.
var storedGuild = await db.Guilds.SingleAsync();
var storedRole = await db.Roles.SingleAsync();
var storedMessage = await db.Messages
    .Include(m => m.Embeds).Include(m => m.Attachments).Include(m => m.Reactions)
    .SingleAsync();

Console.WriteLine($"Mapped guild: {storedGuild.Name} (owner {storedGuild.OwnerId})");
Console.WriteLine($"Mapped role: {storedRole.Name} permissions={storedRole.Permissions} color=0x{storedRole.Color:X6}");
Console.WriteLine($"Mapped message {storedMessage.Id}: \"{storedMessage.Content}\"");
Console.WriteLine($"  embeds={storedMessage.Embeds.Count} attachments={storedMessage.Attachments.Count} reactions={storedMessage.Reactions.Count}");
Console.WriteLine($"  reaction: {storedMessage.Reactions.Single().Emoji} x{storedMessage.Reactions.Single().Count}");
Console.WriteLine($"History rows: {await db.MessageHistory.CountAsync()}");

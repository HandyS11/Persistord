using Microsoft.EntityFrameworkCore;
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Persistord.Sample.Messages;

var options = new DbContextOptionsBuilder<MessagesContext>()
    .UseSqlite("DataSource=messages.db")
    .Options;

await using var db = new MessagesContext(options);
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

// A message carrying every kind of rich content the Messages module models:
// an owned embed (with footer, author and fields), relational attachments and reactions.
var message = new MessageEntity
{
    Id = 9001UL,
    ChannelId = 1001UL,
    AuthorId = 4242UL,
    Content = "Check out this release!",
};

var embed = new Embed
{
    Title = "Persistord 1.0",
    Description = "Provider-agnostic Discord persistence.",
    Color = 0x5865F2,
    Footer = new EmbedFooter { Text = "released today", IconUrl = "https://example/icon.png" },
    Author = new EmbedAuthor { Name = "Persistord", Url = "https://github.com/HandyS11/Persistord" },
};
embed.Fields.Add(new EmbedField { Name = "Providers", Value = "any EF Core 10", Inline = true });
embed.Fields.Add(new EmbedField { Name = "Modules", Value = "Core, Messages, History", Inline = true });
message.Embeds.Add(embed);

message.Attachments.Add(new AttachmentEntity
{
    Id = 9100UL, FileName = "changelog.txt", Url = "https://cdn/changelog.txt",
});
message.Reactions.Add(new ReactionEntity { Emoji = "🎉", Count = 12 });
message.Reactions.Add(new ReactionEntity { Emoji = "rocket:806139563617779712", Count = 5 });

db.Messages.Add(message);
await db.SaveChangesAsync();

// Read back with the child collections to prove they persisted and re-materialize.
var stored = await db.Messages
    .Include(m => m.Embeds).ThenInclude(e => e.Fields)
    .Include(m => m.Attachments)
    .Include(m => m.Reactions)
    .SingleAsync();

Console.WriteLine($"Message {stored.Id}: \"{stored.Content}\"");
var storedEmbed = stored.Embeds.Single();
Console.WriteLine($"Embed: {storedEmbed.Title} | footer='{storedEmbed.Footer?.Text}' author='{storedEmbed.Author?.Name}' fields={storedEmbed.Fields.Count}");
Console.WriteLine($"Attachments: {string.Join(", ", stored.Attachments.Select(a => a.FileName))}");
Console.WriteLine($"Reactions: {string.Join(", ", stored.Reactions.Select(r => $"{r.Emoji} x{r.Count}"))}");

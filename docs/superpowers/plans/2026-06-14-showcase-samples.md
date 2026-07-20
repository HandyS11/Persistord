# Showcase Samples Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four focused, runnable console sample projects that each showcase one Persistord capability area (core graph, messages, soft-delete/history, Discord.Net adapter), plus solution and README wiring.

**Architecture:** Each sample is a standalone console app deriving its own `DiscordDbContext` (wiring only the modules it needs), using SQLite with `EnsureDeleted` + `EnsureCreated` so it is idempotent and migration-free. Each writes data for its capability then reads it back and prints clear evidence. The Discord.Net sample builds NSubstitute fakes of the Discord.Net interfaces, runs the `.To*Entity()` mappers, and persists the results.

**Tech Stack:** .NET 10, EF Core 10.0.9, SQLite, NSubstitute 5.3.0, Discord.Net 3.20.x. Central package management (`Directory.Packages.props`) — reference packages **without** versions. Formatting enforced by ReSharper `cleanupcode`.

---

## Conventions for every sample

- csproj mirrors `samples/Persistord.Sample/Persistord.Sample.csproj`: `OutputType=Exe`, `TargetFramework=net10.0`, `IsPackable=false`, `GenerateDocumentationFile=false`.
- `PackageReference` entries carry **no** `Version` attribute (central package management).
- Each sample uses its own SQLite file db (e.g. `coregraph.db`) and starts with `EnsureDeletedAsync()` then `EnsureCreatedAsync()`.
- After writing the code for a task, format before committing:
  `dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder"`
  (run after Task 5 adds projects to the solution; for Tasks 1–4 it is fine to run `dotnet format`-equivalent only at the end, but the canonical formatter is `cleanupcode` — see Task 5 step for the full-solution pass).
- Verify a sample with: `dotnet run --project samples/<Name>/<Name>.csproj`.

---

### Task 1: Core graph sample

Showcases Guilds, Channels (category → text → thread hierarchy via `ParentId`), Users, Members (composite key), Roles, and the snowflake `ulong ↔ long` round-trip.

**Files:**
- Create: `samples/Persistord.Sample.CoreGraph/Persistord.Sample.CoreGraph.csproj`
- Create: `samples/Persistord.Sample.CoreGraph/CoreGraphContext.cs`
- Create: `samples/Persistord.Sample.CoreGraph/Program.cs`

- [ ] **Step 1: Create the csproj**

`samples/Persistord.Sample.CoreGraph/Persistord.Sample.CoreGraph.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the context**

`samples/Persistord.Sample.CoreGraph/CoreGraphContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;

namespace Persistord.Sample.CoreGraph;

/// <summary>
/// Minimal context for the core-graph sample. It wires no optional module — the
/// base <see cref="DiscordDbContext"/> already exposes Guilds, Channels, Users,
/// Members and Roles, and applies the global snowflake conversion.
/// </summary>
public sealed class CoreGraphContext(DbContextOptions<CoreGraphContext> options)
    : DiscordDbContext(options);
```

- [ ] **Step 3: Create Program.cs**

`samples/Persistord.Sample.CoreGraph/Program.cs`:

```csharp
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

db.Guilds.Add(new GuildEntity { Id = guildId, Name = "Showcase Guild", OwnerId = ownerId });

// A category that parents a text channel, which in turn parents a thread.
const ulong categoryId = 1000UL;
const ulong textChannelId = 1001UL;
const ulong threadId = 1002UL;
db.Channels.AddRange(
    new ChannelEntity { Id = categoryId, GuildId = guildId, Type = ChannelType.Category, Name = "general" },
    new ChannelEntity
    {
        Id = textChannelId, GuildId = guildId, ParentId = categoryId, Type = ChannelType.Text, Name = "chat",
    },
    new ChannelEntity
    {
        Id = threadId, GuildId = guildId, ParentId = textChannelId, Type = ChannelType.Thread, Name = "a-thread",
    });

// A user, that user's per-guild membership (composite (GuildId, UserId) key), and a role.
db.Users.Add(new UserEntity { Id = ownerId, Username = "owner", GlobalName = "The Owner" });
db.Members.Add(new MemberEntity
{
    GuildId = guildId, UserId = ownerId, Nickname = "Boss", JoinedAt = DateTimeOffset.UtcNow,
});
db.Roles.Add(new RoleEntity
{
    Id = 2000UL, GuildId = guildId, Name = "Admin", Permissions = 8UL, Color = 0xFF0000,
});

await db.SaveChangesAsync();

// Read back and prove the snowflake survived the round-trip through signed storage.
var guild = await db.Guilds.SingleAsync();
Console.WriteLine($"Guild id round-trip: stored & read back {guild.Id} (matches ulong.MaxValue: {guild.Id == ulong.MaxValue})");
Console.WriteLine($"Channels: {await db.Channels.CountAsync()} (category -> text -> thread)");

var thread = await db.Channels.SingleAsync(c => c.Type == ChannelType.Thread);
Console.WriteLine($"Thread '{thread.Name}' parent id: {thread.ParentId}");

var member = await db.Members.SingleAsync();
Console.WriteLine($"Member (guild {member.GuildId}, user {member.UserId}) nickname: {member.Nickname}");
Console.WriteLine($"Roles: {await db.Roles.CountAsync()}");
```

- [ ] **Step 4: Build and run**

Run: `dotnet run --project samples/Persistord.Sample.CoreGraph/Persistord.Sample.CoreGraph.csproj`
Expected output (order matters; exact ids shown):

```
Guild id round-trip: stored & read back 18446744073709551615 (matches ulong.MaxValue: True)
Channels: 3 (category -> text -> thread)
Thread 'a-thread' parent id: 1001
Member (guild 18446744073709551615, user 4242424242424242) nickname: Boss
Roles: 1
```

- [ ] **Step 5: Commit**

```bash
git add samples/Persistord.Sample.CoreGraph
git commit -m "docs(samples): add core-graph showcase sample"
```

---

### Task 2: Messages sample

Showcases `MessageEntity` with owned `Embed`s (footer, author, fields), relational `AttachmentEntity` and `ReactionEntity`.

**Files:**
- Create: `samples/Persistord.Sample.Messages/Persistord.Sample.Messages.csproj`
- Create: `samples/Persistord.Sample.Messages/MessagesContext.cs`
- Create: `samples/Persistord.Sample.Messages/Program.cs`

- [ ] **Step 1: Create the csproj**

`samples/Persistord.Sample.Messages/Persistord.Sample.Messages.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the context**

`samples/Persistord.Sample.Messages/MessagesContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample.Messages;

/// <summary>Context for the messages sample: core skeleton plus the Messages module.</summary>
public sealed class MessagesContext(DbContextOptions<MessagesContext> options) : DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
    }
}
```

- [ ] **Step 3: Create Program.cs**

`samples/Persistord.Sample.Messages/Program.cs`:

```csharp
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
```

- [ ] **Step 4: Build and run**

Run: `dotnet run --project samples/Persistord.Sample.Messages/Persistord.Sample.Messages.csproj`
Expected output:

```
Message 9001: "Check out this release!"
Embed: Persistord 1.0 | footer='released today' author='Persistord' fields=2
Attachments: changelog.txt
Reactions: 🎉 x12, rocket:806139563617779712 x5
```

- [ ] **Step 5: Commit**

```bash
git add samples/Persistord.Sample.Messages
git commit -m "docs(samples): add messages & rich-content showcase sample"
```

---

### Task 3: Soft-delete & history sample

Showcases soft-delete (`IsDeleted`/`DeletedAt`), the default query filter (hidden vs `IgnoreQueryFilters()`), and the append-only `MessageHistoryEntity` audit trail across Created → Edited → Deleted.

**Files:**
- Create: `samples/Persistord.Sample.History/Persistord.Sample.History.csproj`
- Create: `samples/Persistord.Sample.History/HistoryContext.cs`
- Create: `samples/Persistord.Sample.History/Program.cs`

- [ ] **Step 1: Create the csproj**

`samples/Persistord.Sample.History/Persistord.Sample.History.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../../src/Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the context**

`samples/Persistord.Sample.History/HistoryContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample.History;

/// <summary>Context for the history sample: core skeleton, Messages, and History.</summary>
public sealed class HistoryContext(DbContextOptions<HistoryContext> options) : DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule(); // default: soft-deleted messages are filtered out
        modelBuilder.ApplyHistoryModule();
    }
}
```

- [ ] **Step 3: Create Program.cs**

`samples/Persistord.Sample.History/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using Persistord.Sample.History;

var options = new DbContextOptionsBuilder<HistoryContext>()
    .UseSqlite("DataSource=history.db")
    .Options;

await using var db = new HistoryContext(options);
await db.Database.EnsureDeletedAsync();
await db.Database.EnsureCreatedAsync();

const ulong messageId = 7001UL;

// 1. Create the message and record a "Created" history snapshot.
db.Messages.Add(new MessageEntity { Id = messageId, ChannelId = 1001UL, AuthorId = 4242UL, Content = "first draft" });
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId, Content = "first draft", RecordedAt = DateTimeOffset.UtcNow, ChangeType = HistoryChangeType.Created,
});
await db.SaveChangesAsync();

// 2. Edit the message and record an "Edited" snapshot.
var message = await db.Messages.SingleAsync(m => m.Id == messageId);
message.Content = "edited text";
message.EditedAt = DateTimeOffset.UtcNow;
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId, Content = "edited text", RecordedAt = DateTimeOffset.UtcNow, ChangeType = HistoryChangeType.Edited,
});
await db.SaveChangesAsync();

// 3. Soft-delete the message (the row survives so history's FK stays valid) and
//    record a "Deleted" snapshot.
message.IsDeleted = true;
message.DeletedAt = DateTimeOffset.UtcNow;
db.MessageHistory.Add(new MessageHistoryEntity
{
    MessageId = messageId, Content = message.Content, RecordedAt = DateTimeOffset.UtcNow, ChangeType = HistoryChangeType.Deleted,
});
await db.SaveChangesAsync();

// The default query filter hides soft-deleted messages...
var visible = await db.Messages.CountAsync();
// ...but IgnoreQueryFilters() includes them, so the row (and its FK target) is still there.
var includingDeleted = await db.Messages.IgnoreQueryFilters().CountAsync();
Console.WriteLine($"Messages visible by default: {visible}; including soft-deleted: {includingDeleted}");

// The append-only history retains every change in order.
var history = await db.MessageHistory
    .Where(h => h.MessageId == messageId)
    .OrderBy(h => h.RecordedAt).ThenBy(h => h.Id)
    .ToListAsync();
Console.WriteLine($"History rows: {history.Count}");
foreach (var row in history)
{
    Console.WriteLine($"  {row.ChangeType}: \"{row.Content}\"");
}
```

- [ ] **Step 4: Build and run**

Run: `dotnet run --project samples/Persistord.Sample.History/Persistord.Sample.History.csproj`
Expected output:

```
Messages visible by default: 0; including soft-deleted: 1
History rows: 3
  Created: "first draft"
  Edited: "edited text"
  Deleted: "edited text"
```

- [ ] **Step 5: Commit**

```bash
git add samples/Persistord.Sample.History
git commit -m "docs(samples): add soft-delete & history showcase sample"
```

---

### Task 4: Discord.Net adapter sample

Showcases the `.To*Entity()` mappers. Discord.Net gateway/REST types are interfaces that cannot be constructed offline, so the sample fakes them with NSubstitute (the same approach the adapter tests use), runs the mappers, and persists the results.

**Files:**
- Create: `samples/Persistord.Sample.DiscordNet/Persistord.Sample.DiscordNet.csproj`
- Create: `samples/Persistord.Sample.DiscordNet/AdapterContext.cs`
- Create: `samples/Persistord.Sample.DiscordNet/Program.cs`

- [ ] **Step 1: Create the csproj**

`samples/Persistord.Sample.DiscordNet/Persistord.Sample.DiscordNet.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Discord.Net" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../../src/Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../../src/Persistord.History/Persistord.History.csproj" />
    <ProjectReference Include="../../src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the context**

`samples/Persistord.Sample.DiscordNet/AdapterContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Persistord.Core;
using Persistord.History;
using Persistord.History.Entities;
using Persistord.Messages;
using Persistord.Messages.Entities;

namespace Persistord.Sample.DiscordNet;

/// <summary>Context for the Discord.Net adapter sample: core skeleton, Messages, and History.</summary>
public sealed class AdapterContext(DbContextOptions<AdapterContext> options) : DiscordDbContext(options)
{
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<MessageHistoryEntity> MessageHistory => Set<MessageHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMessagesModule();
        modelBuilder.ApplyHistoryModule();
    }
}
```

- [ ] **Step 3: Create Program.cs**

Note: `ReactionMetadata.ReactionCount` has an internal setter, so it is set via reflection on a boxed value — exactly as the adapter tests do it.

`samples/Persistord.Sample.DiscordNet/Program.cs`:

```csharp
using Discord;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;
using Persistord.Sample.DiscordNet;

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
role.Colors.Returns(new RoleColors(new Color(0xFF0000)));

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
```

- [ ] **Step 4: Build and run**

Run: `dotnet run --project samples/Persistord.Sample.DiscordNet/Persistord.Sample.DiscordNet.csproj`
Expected output:

```
Mapped guild: Showcase Guild (owner 200)
Mapped role: Admin permissions=8 color=0xFF0000
Mapped message 9001: "Check out this release!"
  embeds=1 attachments=1 reactions=1
  reaction: 🎉 x7
History rows: 1
```

If `GuildPermissions`/`RoleColors`/`Color` construction differs in the resolved Discord.Net 3.20.x build, mirror exactly what `tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs` does for the role fake.

- [ ] **Step 5: Commit**

```bash
git add samples/Persistord.Sample.DiscordNet
git commit -m "docs(samples): add Discord.Net adapter showcase sample"
```

---

### Task 5: Solution & README integration

Wire the four new projects into the solution, run the canonical formatter over everything, and update the README to point at each sample.

**Files:**
- Modify: `Persistord.slnx`
- Modify: `README.md:127-133`

- [ ] **Step 1: Add the projects to the solution**

In `Persistord.slnx`, inside the `<Folder Name="/samples/">` element, add the four projects after the existing `Persistord.Sample` line so the folder reads:

```xml
  <Folder Name="/samples/">
    <Project Path="samples/Persistord.Sample/Persistord.Sample.csproj" />
    <Project Path="samples/Persistord.Sample.CoreGraph/Persistord.Sample.CoreGraph.csproj" />
    <Project Path="samples/Persistord.Sample.Messages/Persistord.Sample.Messages.csproj" />
    <Project Path="samples/Persistord.Sample.History/Persistord.Sample.History.csproj" />
    <Project Path="samples/Persistord.Sample.DiscordNet/Persistord.Sample.DiscordNet.csproj" />
  </Folder>
```

- [ ] **Step 2: Update the README samples bullet**

In `README.md`, replace the single samples bullet (currently the last bullet of the "Documentation" section, referencing only `samples/Persistord.Sample`) with:

```markdown
- Samples — runnable, focused walkthroughs (all SQLite):
  - [`Persistord.Sample`](samples/Persistord.Sample) — minimal quick-start (all three modules, generated migration).
  - [`Persistord.Sample.CoreGraph`](samples/Persistord.Sample.CoreGraph) — guilds, channels, users, members, roles, and the snowflake round-trip.
  - [`Persistord.Sample.Messages`](samples/Persistord.Sample.Messages) — messages with embeds, attachments, and reactions.
  - [`Persistord.Sample.History`](samples/Persistord.Sample.History) — soft-delete, query filters, and append-only history.
  - [`Persistord.Sample.DiscordNet`](samples/Persistord.Sample.DiscordNet) — `.To*Entity()` mappers driven by faked Discord.Net types.
```

- [ ] **Step 3: Build the whole solution**

Run: `dotnet build Persistord.slnx`
Expected: build succeeds, all sample projects compile.

- [ ] **Step 4: Format the solution**

Run: `dotnet jb cleanupcode Persistord.slnx --profile="ReformatAndReorder"`
Expected: completes with no errors; re-run `dotnet build Persistord.slnx` to confirm it still builds.

- [ ] **Step 5: Run every sample to confirm output**

Run each and confirm the expected output from Tasks 1–4:

```bash
dotnet run --project samples/Persistord.Sample.CoreGraph/Persistord.Sample.CoreGraph.csproj
dotnet run --project samples/Persistord.Sample.Messages/Persistord.Sample.Messages.csproj
dotnet run --project samples/Persistord.Sample.History/Persistord.Sample.History.csproj
dotnet run --project samples/Persistord.Sample.DiscordNet/Persistord.Sample.DiscordNet.csproj
```

- [ ] **Step 6: Commit**

```bash
git add Persistord.slnx README.md samples
git commit -m "docs(samples): register showcase samples in solution and README"
```

---

## Self-Review

**Spec coverage:**
- Multiple focused projects → Tasks 1–4. ✓
- SQLite provider every sample → all csproj/Program use `UseSqlite`. ✓
- NSubstitute fakes for the adapter → Task 4. ✓
- Core graph + snowflake round-trip → Task 1. ✓
- Messages/embeds/attachments/reactions → Task 2. ✓
- Soft-delete + query filter + history → Task 3. ✓
- `.To*Entity()` mappers → Task 4. ✓
- slnx + README updates, samples only → Task 5; no `src/` changes. ✓
- Idempotent `EnsureDeleted`+`EnsureCreated` → every Program. ✓

**Placeholder scan:** No TBD/TODO; all code blocks complete; the only conditional note (Task 4 step 4) points to the concrete test file to copy from, not a placeholder.

**Type consistency:** Context type names (`CoreGraphContext`, `MessagesContext`, `HistoryContext`, `AdapterContext`) match between each context file, its Program `DbContextOptionsBuilder<T>`, and constructor. Entity property names verified against `src/` (`GuildEntity.OwnerId`, `ChannelEntity.ParentId`, `MemberEntity` composite, `RoleEntity.Permissions/Color`, `MessageEntity` children, `Embed`/`EmbedField`/`EmbedFooter`/`EmbedAuthor`, `MessageHistoryEntity.ChangeType`). Mapper names match adapter source (`ToGuildEntity`, `ToUserEntity`, `ToRoleEntity`, `ToMessageEntity`, `ToHistoryEntity`).

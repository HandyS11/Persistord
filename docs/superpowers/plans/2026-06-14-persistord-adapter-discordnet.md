# Persistord.Adapters.DiscordNet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Persistord.Adapters.DiscordNet`, an opt-in package of extension methods that map Discord.Net interface types to Persistord entities.

**Architecture:** A single static `DiscordNetMappingExtensions` class exposes `.To*Entity()` extension methods on Discord.Net's interfaces (`IGuild`, `IGuildChannel`, `IUser`, `IGuildUser`, `IRole`, `IMessage`). Mappers are pure functions: they copy data fields only, never touch persistence-managed fields (`IsDeleted`, surrogate keys), and leave EF to fill foreign keys from navigation collections. Tests mock the interfaces with NSubstitute.

**Tech Stack:** .NET 10, Discord.Net 3.x (interfaces), xunit, NSubstitute, EF Core entities from `Persistord.Core`/`.Messages`/`.History`.

**Scope note:** This is the **reference adapter** (sub-project ② of the brainstorm, Discord.Net first). NetCord and DSharpPlus get sibling plans that replicate this shape against their concrete model types.

**Spec:** `docs/superpowers/specs/2026-06-14-persistord-adapter-packages-design.md`

---

## File Structure

**Created:**

- `src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj` — packable project, refs Core+Messages+History+Discord.Net
- `src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs` — all `.To*Entity()` extension methods + the private channel-type helper
- `src/Persistord.Adapters.DiscordNet/README.md` — package readme (API + versioning policy)
- `tests/Persistord.Adapters.DiscordNet.Tests/Persistord.Adapters.DiscordNet.Tests.csproj` — xunit + NSubstitute test project
- `tests/Persistord.Adapters.DiscordNet.Tests/ChannelTypeMappingTests.cs`
- `tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs`
- `tests/Persistord.Adapters.DiscordNet.Tests/MessageMappingTests.cs`
- `tests/Persistord.Adapters.DiscordNet.Tests/HistoryMappingTests.cs`

**Modified:**

- `Directory.Packages.props` — add `Discord.Net` and `NSubstitute` versions
- `Persistord.slnx` — add the two new projects

**Design refinement vs spec:** the spec's API table listed `ToChannelEntity(this IChannel channel)`, but `ChannelEntity.GuildId` requires guild context, so the binding is `IGuildChannel` (which carries `GuildId`). This is the planned refinement noted in the spec's §2.

---

## Task 1: Project scaffolding & package wiring

**Files:**

- Create: `src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj`
- Create: `tests/Persistord.Adapters.DiscordNet.Tests/Persistord.Adapters.DiscordNet.Tests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `Persistord.slnx`

- [ ] **Step 1: Add package versions to `Directory.Packages.props`**

Add `Discord.Net` to the "Packages" ItemGroup (it ships at runtime) and `NSubstitute` to the "Test + sample" ItemGroup:

In the first `<ItemGroup>` (Packages), after the EF Core lines, add:

```xml
    <PackageVersion Include="Discord.Net" Version="3.15.0" />
```

In the second `<ItemGroup>` (Test + sample), add:

```xml
    <PackageVersion Include="NSubstitute" Version="5.1.0" />
```

- [ ] **Step 2: Create the adapter project file**

Create `src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>Persistord.Adapters.DiscordNet</PackageId>
    <Description>Discord.Net adapter for Persistord: extension methods mapping Discord.Net interface types to Persistord entities.</Description>
    <Authors>HandyS11</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>discord;discord.net;efcore;persistence;adapter</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Discord.Net" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Persistord.Core/Persistord.Core.csproj" />
    <ProjectReference Include="../Persistord.Messages/Persistord.Messages.csproj" />
    <ProjectReference Include="../Persistord.History/Persistord.History.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="$(MSBuildProjectDirectory)/README.md" Pack="true" PackagePath="\" Condition="Exists('README.md')" />
  </ItemGroup>
</Project>
```

Note: the `Discord.Net` reference is a **normal** PackageReference (no `PrivateAssets`), so the dependency flows transitively to consumers.

- [ ] **Step 3: Create a placeholder README so the build does not warn on the missing pack file**

Create `src/Persistord.Adapters.DiscordNet/README.md` with a single line (full content comes in Task 7):

```markdown
# Persistord.Adapters.DiscordNet
```

- [ ] **Step 4: Create the test project file**

Create `tests/Persistord.Adapters.DiscordNet.Tests/Persistord.Adapters.DiscordNet.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Register both projects in `Persistord.slnx`**

Add the adapter under the `/src/` folder and the test under the `/tests/` folder:

```xml
    <Project Path="src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj" />
```

```xml
    <Project Path="tests/Persistord.Adapters.DiscordNet.Tests/Persistord.Adapters.DiscordNet.Tests.csproj" />
```

- [ ] **Step 6: Verify the solution restores and builds**

Run: `dotnet build Persistord.slnx`
Expected: build succeeds (the test project has no tests yet; the adapter compiles with an empty README and no code files).

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props Persistord.slnx src/Persistord.Adapters.DiscordNet tests/Persistord.Adapters.DiscordNet.Tests
git commit -m "feat(adapters): scaffold Persistord.Adapters.DiscordNet package and test project"
```

---

## Task 2: Channel-type translation helper

Discord.Net does not expose a uniform `ChannelType` property on `IGuildChannel`; the kind is determined by which sub-interface the channel implements. This helper centralizes that translation to Persistord's `ChannelType` enum (`Text=0`, `Voice=2`, `Category=4`, `Thread=11`).

**Files:**

- Create: `src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.DiscordNet.Tests/ChannelTypeMappingTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Adapters.DiscordNet.Tests/ChannelTypeMappingTests.cs`:

```csharp
using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Persistord.Core.Entities;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

public class ChannelTypeMappingTests
{
    [Fact]
    public void TextChannel_maps_to_Text()
    {
        var channel = Substitute.For<ITextChannel>();
        Assert.Equal(ChannelType.Text, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void VoiceChannel_maps_to_Voice()
    {
        var channel = Substitute.For<IVoiceChannel>();
        Assert.Equal(ChannelType.Voice, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void CategoryChannel_maps_to_Category()
    {
        var channel = Substitute.For<ICategoryChannel>();
        Assert.Equal(ChannelType.Category, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void ThreadChannel_maps_to_Thread()
    {
        var channel = Substitute.For<IThreadChannel>();
        Assert.Equal(ChannelType.Thread, channel.ToChannelEntity().Type);
    }

    [Fact]
    public void UnknownGuildChannel_falls_back_to_Text()
    {
        var channel = Substitute.For<IGuildChannel>();
        Assert.Equal(ChannelType.Text, channel.ToChannelEntity().Type);
    }
}
```

Note: `ITextChannel`, `IVoiceChannel`, `ICategoryChannel`, `IThreadChannel` all derive from `IGuildChannel`, so each substitute is a valid argument to `ToChannelEntity(this IGuildChannel)`.

- [ ] **Step 2: Create the extensions file with the channel mapper + type helper**

Create `src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs`:

```csharp
using Discord;
using Persistord.Core.Entities;

namespace Persistord.Adapters.DiscordNet;

/// <summary>
/// Extension methods mapping Discord.Net interface types to Persistord entities.
/// Mappers copy data fields only; persistence-managed fields and EF-generated keys
/// are left at their defaults.
/// </summary>
public static class DiscordNetMappingExtensions
{
    /// <summary>Maps a Discord.Net guild channel to a <see cref="ChannelEntity"/>.</summary>
    public static ChannelEntity ToChannelEntity(this IGuildChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ChannelEntity
        {
            Id = channel.Id,
            GuildId = channel.GuildId,
            ParentId = (channel as INestedChannel)?.CategoryId,
            Type = MapChannelType(channel),
            Name = channel.Name ?? string.Empty,
        };
    }

    private static ChannelType MapChannelType(IGuildChannel channel) => channel switch
    {
        ICategoryChannel => ChannelType.Category,
        IThreadChannel => ChannelType.Thread,
        IVoiceChannel => ChannelType.Voice,
        ITextChannel => ChannelType.Text,
        _ => ChannelType.Text,
    };
}
```

Note: order matters in the `switch` — `IThreadChannel`/`ITextChannel` relationships mean threads must be checked before text, and stage channels (`IStageChannel : IVoiceChannel`) fall into the voice arm.

- [ ] **Step 3: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter ChannelTypeMappingTests`
Expected: PASS (5 tests).

- [ ] **Step 4: Commit**

```bash
git add src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs tests/Persistord.Adapters.DiscordNet.Tests/ChannelTypeMappingTests.cs
git commit -m "feat(adapters): map Discord.Net channel kinds to Persistord ChannelType"
```

---

## Task 3: Core flat mappers — Guild, User, Role, Member

**Files:**

- Modify: `src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs`:

```csharp
using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

public class CoreEntityMappingTests
{
    [Fact]
    public void ToGuildEntity_maps_id_name_owner()
    {
        var guild = Substitute.For<IGuild>();
        guild.Id.Returns(100UL);
        guild.Name.Returns("Test Guild");
        guild.OwnerId.Returns(200UL);

        var entity = guild.ToGuildEntity();

        Assert.Equal(100UL, entity.Id);
        Assert.Equal("Test Guild", entity.Name);
        Assert.Equal(200UL, entity.OwnerId);
    }

    [Fact]
    public void ToUserEntity_maps_id_username_globalname()
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(300UL);
        user.Username.Returns("alice");
        user.GlobalName.Returns("Alice");

        var entity = user.ToUserEntity();

        Assert.Equal(300UL, entity.Id);
        Assert.Equal("alice", entity.Username);
        Assert.Equal("Alice", entity.GlobalName);
    }

    [Fact]
    public void ToUserEntity_tolerates_null_globalname()
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(301UL);
        user.Username.Returns("bob");
        user.GlobalName.Returns((string?)null);

        var entity = user.ToUserEntity();

        Assert.Null(entity.GlobalName);
    }

    [Fact]
    public void ToRoleEntity_maps_id_guild_name_permissions_color()
    {
        var guild = Substitute.For<IGuild>();
        guild.Id.Returns(100UL);
        var role = Substitute.For<IRole>();
        role.Id.Returns(400UL);
        role.Guild.Returns(guild);
        role.Name.Returns("Admins");
        role.Permissions.Returns(new GuildPermissions(8UL));
        role.Color.Returns(new Color(0xFF0000));

        var entity = role.ToRoleEntity();

        Assert.Equal(400UL, entity.Id);
        Assert.Equal(100UL, entity.GuildId);
        Assert.Equal("Admins", entity.Name);
        Assert.Equal(8UL, entity.Permissions);
        Assert.Equal(0xFF0000, entity.Color);
    }

    [Fact]
    public void ToMemberEntity_maps_guild_user_nickname_joinedat()
    {
        var joined = DateTimeOffset.UtcNow;
        var member = Substitute.For<IGuildUser>();
        member.GuildId.Returns(100UL);
        member.Id.Returns(300UL);
        member.Nickname.Returns("Ali");
        member.JoinedAt.Returns(joined);

        var entity = member.ToMemberEntity();

        Assert.Equal(100UL, entity.GuildId);
        Assert.Equal(300UL, entity.UserId);
        Assert.Equal("Ali", entity.Nickname);
        Assert.Equal(joined, entity.JoinedAt);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter CoreEntityMappingTests`
Expected: FAIL to compile — `ToGuildEntity`, `ToUserEntity`, `ToRoleEntity`, `ToMemberEntity` do not exist.

- [ ] **Step 3: Add the four mappers**

Add these methods inside `DiscordNetMappingExtensions` (after `ToChannelEntity`):

```csharp
    /// <summary>Maps a Discord.Net guild to a <see cref="GuildEntity"/>.</summary>
    public static GuildEntity ToGuildEntity(this IGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        return new GuildEntity
        {
            Id = guild.Id,
            Name = guild.Name ?? string.Empty,
            OwnerId = guild.OwnerId,
        };
    }

    /// <summary>Maps a Discord.Net user to a <see cref="UserEntity"/>.</summary>
    public static UserEntity ToUserEntity(this IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserEntity
        {
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            GlobalName = user.GlobalName,
        };
    }

    /// <summary>Maps a Discord.Net role to a <see cref="RoleEntity"/>.</summary>
    public static RoleEntity ToRoleEntity(this IRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new RoleEntity
        {
            Id = role.Id,
            GuildId = role.Guild.Id,
            Name = role.Name ?? string.Empty,
            Permissions = role.Permissions.RawValue,
            Color = unchecked((int)role.Color.RawValue),
        };
    }

    /// <summary>Maps a Discord.Net guild member to a <see cref="MemberEntity"/>.</summary>
    public static MemberEntity ToMemberEntity(this IGuildUser member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberEntity
        {
            GuildId = member.GuildId,
            UserId = member.Id,
            Nickname = member.Nickname,
            JoinedAt = member.JoinedAt,
        };
    }
```

Note: `GuildPermissions.RawValue` is `ulong` (matches `RoleEntity.Permissions`); `Color.RawValue` is `uint` (cast to `RoleEntity.Color`'s `int`). `IGuildUser : IUser`, so `member.Id` is the user's snowflake.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter CoreEntityMappingTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs
git commit -m "feat(adapters): map Discord.Net guild, user, role, member to Persistord entities"
```

---

## Task 4: Channel mapper field coverage

Task 2 created `ToChannelEntity` to satisfy the type-mapping tests. This task adds tests for its remaining fields (id, guild, parent, name) to lock the full contract.

**Files:**

- Test: `tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs` (add methods)

- [ ] **Step 1: Add failing tests for channel field coverage**

Add these methods to the existing `CoreEntityMappingTests` class:

```csharp
    [Fact]
    public void ToChannelEntity_maps_id_guild_name()
    {
        var channel = Substitute.For<ITextChannel>();
        channel.Id.Returns(500UL);
        channel.GuildId.Returns(100UL);
        channel.Name.Returns("general");

        var entity = channel.ToChannelEntity();

        Assert.Equal(500UL, entity.Id);
        Assert.Equal(100UL, entity.GuildId);
        Assert.Equal("general", entity.Name);
    }

    [Fact]
    public void ToChannelEntity_maps_category_parent_for_nested_channel()
    {
        var channel = Substitute.For<ITextChannel>(); // ITextChannel : INestedChannel
        channel.Id.Returns(501UL);
        channel.GuildId.Returns(100UL);
        channel.CategoryId.Returns(600UL);

        var entity = channel.ToChannelEntity();

        Assert.Equal(600UL, entity.ParentId);
    }

    [Fact]
    public void ToChannelEntity_leaves_parent_null_when_not_nested()
    {
        var channel = Substitute.For<IGuildChannel>();
        channel.Id.Returns(502UL);

        var entity = channel.ToChannelEntity();

        Assert.Null(entity.ParentId);
    }
```

Note: `ITextChannel` derives from `INestedChannel`, which exposes `CategoryId` (`ulong?`); a bare `IGuildChannel` substitute is not an `INestedChannel`, so the `as` cast yields null.

- [ ] **Step 2: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter CoreEntityMappingTests`
Expected: PASS (all 8 tests — the channel mapper from Task 2 already implements these fields).

- [ ] **Step 3: Commit**

```bash
git add tests/Persistord.Adapters.DiscordNet.Tests/CoreEntityMappingTests.cs
git commit -m "test(adapters): cover Discord.Net channel field mapping"
```

---

## Task 5: Message mapper (embeds, attachments, reactions)

**Files:**

- Modify: `src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.DiscordNet.Tests/MessageMappingTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Adapters.DiscordNet.Tests/MessageMappingTests.cs`:

```csharp
using System.Collections.Immutable;
using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

public class MessageMappingTests
{
    private static IMessage MinimalMessage()
    {
        var author = Substitute.For<IUser>();
        author.Id.Returns(300UL);
        var channel = Substitute.For<IMessageChannel>();
        channel.Id.Returns(500UL);

        var message = Substitute.For<IMessage>();
        message.Id.Returns(700UL);
        message.Channel.Returns(channel);
        message.Author.Returns(author);
        message.Content.Returns("hello");
        message.EditedTimestamp.Returns((DateTimeOffset?)null);
        message.Embeds.Returns(Array.Empty<IEmbed>());
        message.Attachments.Returns(Array.Empty<IAttachment>());
        message.Reactions.Returns(new Dictionary<IEmote, ReactionMetadata>());
        return message;
    }

    [Fact]
    public void ToMessageEntity_maps_scalar_fields()
    {
        var entity = MinimalMessage().ToMessageEntity();

        Assert.Equal(700UL, entity.Id);
        Assert.Equal(500UL, entity.ChannelId);
        Assert.Equal(300UL, entity.AuthorId);
        Assert.Equal("hello", entity.Content);
        Assert.Null(entity.EditedAt);
    }

    [Fact]
    public void ToMessageEntity_does_not_set_persistence_fields()
    {
        var entity = MinimalMessage().ToMessageEntity();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAt);
    }

    [Fact]
    public void ToMessageEntity_maps_attachments()
    {
        var attachment = Substitute.For<IAttachment>();
        attachment.Id.Returns(800UL);
        attachment.Filename.Returns("file.png");
        attachment.Url.Returns("https://cdn/file.png");

        var message = MinimalMessage();
        message.Attachments.Returns([attachment]);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Attachments);
        Assert.Equal(800UL, mapped.Id);
        Assert.Equal("file.png", mapped.FileName);
        Assert.Equal("https://cdn/file.png", mapped.Url);
    }

    [Fact]
    public void ToMessageEntity_maps_reactions()
    {
        var emote = Substitute.For<IEmote>();
        emote.Name.Returns("👍");
        var reactions = new Dictionary<IEmote, ReactionMetadata>
        {
            [emote] = new ReactionMetadata { ReactionCount = 3 },
        };

        var message = MinimalMessage();
        message.Reactions.Returns(reactions);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Reactions);
        Assert.Equal("👍", mapped.Emoji);
        Assert.Equal(3, mapped.Count);
    }

    [Fact]
    public void ToMessageEntity_maps_embed_with_footer_author_fields()
    {
        var embed = new EmbedBuilder()
            .WithTitle("T")
            .WithDescription("D")
            .WithColor(new Color(0x00FF00))
            .WithFooter(f => { f.Text = "foot"; f.IconUrl = "https://i/foot.png"; })
            .WithAuthor(a => { a.Name = "auth"; a.Url = "https://a"; })
            .AddField("fname", "fvalue", inline: true)
            .Build();

        var message = MinimalMessage();
        message.Embeds.Returns([embed]);

        var entity = message.ToMessageEntity();

        var mapped = Assert.Single(entity.Embeds);
        Assert.Equal("T", mapped.Title);
        Assert.Equal("D", mapped.Description);
        Assert.Equal(0x00FF00, mapped.Color);
        Assert.Equal("foot", mapped.Footer!.Text);
        Assert.Equal("https://i/foot.png", mapped.Footer.IconUrl);
        Assert.Equal("auth", mapped.Author!.Name);
        Assert.Equal("https://a", mapped.Author.Url);
        var field = Assert.Single(mapped.Fields);
        Assert.Equal("fname", field.Name);
        Assert.Equal("fvalue", field.Value);
        Assert.True(field.Inline);
    }
}
```

Note: `EmbedBuilder().Build()` returns a real `Embed` (implements `IEmbed`), so embeds are tested with genuine objects rather than mocks. `ReactionMetadata` is a struct with a settable `ReactionCount`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter MessageMappingTests`
Expected: FAIL to compile — `ToMessageEntity` does not exist.

- [ ] **Step 3: Implement the message mapper**

Add `using Persistord.Messages.Entities;` and `using Persistord.Messages.Owned;` to the top of `DiscordNetMappingExtensions.cs` (alongside the existing `using Persistord.Core.Entities;`). Then add these methods inside the class:

```csharp
    /// <summary>
    /// Maps a Discord.Net message to a <see cref="MessageEntity"/>, including embeds,
    /// attachments, and reactions. Soft-delete state and EF-generated keys are left
    /// at their defaults; child foreign keys are filled by EF from the navigation
    /// collections on save.
    /// </summary>
    public static MessageEntity ToMessageEntity(this IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var entity = new MessageEntity
        {
            Id = message.Id,
            ChannelId = message.Channel.Id,
            AuthorId = message.Author.Id,
            Content = message.Content,
            EditedAt = message.EditedTimestamp,
        };

        foreach (var attachment in message.Attachments)
        {
            entity.Attachments.Add(new AttachmentEntity
            {
                Id = attachment.Id,
                FileName = attachment.Filename ?? string.Empty,
                Url = attachment.Url ?? string.Empty,
            });
        }

        foreach (var (emote, metadata) in message.Reactions)
        {
            entity.Reactions.Add(new ReactionEntity
            {
                Emoji = emote.Name ?? string.Empty,
                Count = metadata.ReactionCount,
            });
        }

        foreach (var embed in message.Embeds)
        {
            entity.Embeds.Add(MapEmbed(embed));
        }

        return entity;
    }

    private static Embed MapEmbed(IEmbed embed)
    {
        var mapped = new Embed
        {
            Title = embed.Title,
            Description = embed.Description,
            Color = embed.Color.HasValue ? unchecked((int)embed.Color.Value.RawValue) : null,
        };

        if (embed.Footer.HasValue)
        {
            mapped.Footer = new EmbedFooter
            {
                Text = embed.Footer.Value.Text,
                IconUrl = embed.Footer.Value.IconUrl,
            };
        }

        if (embed.Author.HasValue)
        {
            mapped.Author = new EmbedAuthor
            {
                Name = embed.Author.Value.Name,
                Url = embed.Author.Value.Url,
            };
        }

        foreach (var field in embed.Fields)
        {
            mapped.Fields.Add(new EmbedField
            {
                Name = field.Name ?? string.Empty,
                Value = field.Value ?? string.Empty,
                Inline = field.Inline,
            });
        }

        return mapped;
    }
```

Note on naming: Persistord and Discord.Net both define `EmbedFooter`/`EmbedAuthor`/`EmbedField`. Because the file's `using` list includes `Persistord.Messages.Owned`, the unqualified type names resolve to Persistord's. The Discord.Net source structs are reached only through `embed.Footer.Value` etc. and are never named, so there is no ambiguity. `IEmbed.Color`, `.Footer`, `.Author` are all nullable structs; `.Fields` is `ImmutableArray<EmbedField>`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter MessageMappingTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs tests/Persistord.Adapters.DiscordNet.Tests/MessageMappingTests.cs
git commit -m "feat(adapters): map Discord.Net message with embeds, attachments, reactions"
```

---

## Task 6: History helper

**Files:**

- Modify: `src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs`
- Test: `tests/Persistord.Adapters.DiscordNet.Tests/HistoryMappingTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Persistord.Adapters.DiscordNet.Tests/HistoryMappingTests.cs`:

```csharp
using Discord;
using NSubstitute;
using Persistord.Adapters.DiscordNet;
using Persistord.History.Entities;
using Xunit;

namespace Persistord.Adapters.DiscordNet.Tests;

public class HistoryMappingTests
{
    [Fact]
    public void ToHistoryEntity_maps_message_and_change_type()
    {
        var before = DateTimeOffset.UtcNow;
        var message = Substitute.For<IMessage>();
        message.Id.Returns(700UL);
        message.Content.Returns("edited content");

        var entity = message.ToHistoryEntity(HistoryChangeType.Edited);

        Assert.Equal(700UL, entity.MessageId);
        Assert.Equal("edited content", entity.Content);
        Assert.Equal(HistoryChangeType.Edited, entity.ChangeType);
        Assert.True(entity.RecordedAt >= before);
    }

    [Fact]
    public void ToHistoryEntity_leaves_surrogate_id_default()
    {
        var message = Substitute.For<IMessage>();
        message.Id.Returns(701UL);

        var entity = message.ToHistoryEntity(HistoryChangeType.Created);

        Assert.Equal(0L, entity.Id);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter HistoryMappingTests`
Expected: FAIL to compile — `ToHistoryEntity` does not exist.

- [ ] **Step 3: Implement the history helper**

Add `using Persistord.History.Entities;` to the top of `DiscordNetMappingExtensions.cs`, then add this method inside the class:

```csharp
    /// <summary>
    /// Builds a <see cref="MessageHistoryEntity"/> snapshot of a message for the given
    /// change type. <see cref="MessageHistoryEntity.RecordedAt"/> is stamped with the
    /// current UTC time; the surrogate key is left for EF to assign.
    /// </summary>
    public static MessageHistoryEntity ToHistoryEntity(this IMessage message, HistoryChangeType changeType)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new MessageHistoryEntity
        {
            MessageId = message.Id,
            Content = message.Content,
            RecordedAt = DateTimeOffset.UtcNow,
            ChangeType = changeType,
        };
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests --filter HistoryMappingTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.DiscordNet/DiscordNetMappingExtensions.cs tests/Persistord.Adapters.DiscordNet.Tests/HistoryMappingTests.cs
git commit -m "feat(adapters): add Discord.Net message history snapshot helper"
```

---

## Task 7: Package README & full verification

**Files:**

- Modify: `src/Persistord.Adapters.DiscordNet/README.md`

- [ ] **Step 1: Write the package README**

Replace the contents of `src/Persistord.Adapters.DiscordNet/README.md`:

```markdown
# Persistord.Adapters.DiscordNet

Opt-in adapter that maps [Discord.Net](https://github.com/discord-net/Discord.Net)
interface types to [Persistord](https://github.com/HandyS11/Persistord) entities.

Install only if you use Discord.Net — the core Persistord packages never reference a
Discord client library.

```bash
dotnet add package Persistord.Adapters.DiscordNet
```

## Usage

The adapter adds `.To*Entity()` extension methods on Discord.Net interfaces:

```csharp
using Persistord.Adapters.DiscordNet;

await using var db = await factory.CreateDbContextAsync();

db.Messages.Add(socketMessage.ToMessageEntity());        // embeds, attachments, reactions included
db.MessageHistory.Add(socketMessage.ToHistoryEntity(HistoryChangeType.Created));
await db.SaveChangesAsync();
```

All mappers:

| Method | Source | Target |
| --- | --- | --- |
| `ToGuildEntity()` | `IGuild` | `GuildEntity` |
| `ToChannelEntity()` | `IGuildChannel` | `ChannelEntity` |
| `ToUserEntity()` | `IUser` | `UserEntity` |
| `ToMemberEntity()` | `IGuildUser` | `MemberEntity` |
| `ToRoleEntity()` | `IRole` | `RoleEntity` |
| `ToMessageEntity()` | `IMessage` | `MessageEntity` |
| `ToHistoryEntity(changeType)` | `IMessage` | `MessageHistoryEntity` |

Mappers copy data fields only. Persistence-managed fields (`MessageEntity.IsDeleted`,
`DeletedAt`) and EF-generated surrogate keys are left at their defaults; child foreign
keys are filled by EF from the navigation collections on `SaveChanges`. Mappers tolerate
partial gateway data (null optional fields) and throw only on a null source argument.

Because they bind to Discord.Net **interfaces**, the mappers work for both gateway
(`Socket*`) and REST (`Rest*`) entities.

## Versioning

This package declares a **minimum** Discord.Net version and no upper bound, so you may
upgrade Discord.Net freely within its current major version. A new adapter release
follows each Discord.Net breaking major.

```

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build Persistord.slnx`
Expected: build succeeds with no warnings (the repo sets `TreatWarningsAsErrors`).

- [ ] **Step 3: Run the full adapter test suite**

Run: `dotnet test tests/Persistord.Adapters.DiscordNet.Tests`
Expected: PASS — 20 tests (5 channel-type + 8 core + 5 message + 2 history).

- [ ] **Step 4: Verify the package packs**

Run: `dotnet pack src/Persistord.Adapters.DiscordNet/Persistord.Adapters.DiscordNet.csproj -o artifacts/packtest`
Expected: produces `Persistord.Adapters.DiscordNet.1.0.0.nupkg`. Confirm the README is packed and `Discord.Net` appears as a dependency (not suppressed).

- [ ] **Step 5: Commit**

```bash
git add src/Persistord.Adapters.DiscordNet/README.md
git commit -m "docs(adapters): add Persistord.Adapters.DiscordNet readme"
```

---

## Self-Review Notes

- **Spec coverage:** §1 package structure → Task 1; §1 versioning policy → Task 7 README; §2 API (all seven methods) → Tasks 2–6; §3 mapping contract (data-only, no persistence fields, EF-filled FKs, channel-type table, partial tolerance) → Tasks 2/4/5 incl. explicit `does_not_set_persistence_fields` and null-tolerance tests; §4 NSubstitute interface mocking → all test tasks. NetCord/DSharpPlus (§5 sequencing) are out of scope by design — sibling plans.
- **Verification points to watch during execution:** Discord.Net property names assumed from the 3.x interface API — `IAttachment.Filename` (lowercase n), `IMessage.EditedTimestamp`, `IGuildUser.JoinedAt` (`DateTimeOffset?`), `GuildPermissions.RawValue` (ulong), `Color.RawValue` (uint), `ReactionMetadata.ReactionCount`, `INestedChannel.CategoryId`. If a name differs in the pinned 3.15.0 package, adjust the mapper and its test together.

```

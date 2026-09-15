---
title: Persistord
layout: landing
description: Persistord ships the EF Core 10 model every Discord bot rewrites — guilds, channels, users, members, roles and messages — without choosing your database provider or your Discord library.
---

<section class="pd-hero">
  <p class="pd-eyebrow"><img src="../icon.png" alt="" width="28" height="28"><span class="pd-wordmark">Persistord</span></p>
  <h1>Every Discord bot rewrites the same tables. Persistord ships them.</h1>
  <p class="pd-hero-lede">An EF Core 10 model for guilds, channels, users, members, roles and messages that never picks your database provider or your Discord library.</p>
  <div class="pd-hero-actions">
    <a class="pd-btn pd-btn-primary" href="articles/getting-started.md">Get started</a>
    <a class="pd-btn" href="articles/packages.md">Browse packages</a>
    <a class="pd-text-link" href="https://github.com/HandyS11/Persistord">GitHub</a>
  </div>
  <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
  <p class="pd-hero-meta">EF Core 10 · PostgreSQL · SQL Server · SQLite · Discord.Net · DSharpPlus · NetCord</p>
</section>

<section class="pd-section pd-transform">
  <h2>A gateway payload, a mapper call, a row</h2>
  <div class="pd-panes">
    <figure class="pd-pane">
      <figcaption class="pd-pane-bar">1 · gateway event</figcaption>
      <pre>{
  "id": "<span class="pd-snowflake">1547396186112000000</span>",
  "channel_id": "1547300000000000000",
  "content": "gg"
}</pre>
    </figure>
    <span class="pd-connector" aria-hidden="true">→</span>
    <figure class="pd-pane">
      <figcaption class="pd-pane-bar">2 · .ToMessageEntity()</figcaption>
      <pre>// Persistord.Adapters.DiscordNet
var row = message.ToMessageEntity();
db.Messages.Add(row);
await db.SaveChangesAsync();</pre>
    </figure>
    <span class="pd-connector" aria-hidden="true">→</span>
    <figure class="pd-pane">
      <figcaption class="pd-pane-bar">3 · messages row</figcaption>
      <pre>column     type     value
---------  -------  -------------------
Id         BIGINT   <span class="pd-row-id">1547396186112000000</span>
ChannelId  BIGINT   1547300000000000000
Content    TEXT     'gg'</pre>
    </figure>
  </div>
  <p class="pd-caption">Discord ids are unsigned 64-bit integers, and PostgreSQL and SQL Server have no native unsigned 64-bit type. <code>DiscordDbContext</code> converts every <code>ulong</code> to a signed <code>BIGINT</code>: the same 64 bits go in and come back out, and no id is ever annotated. <a href="articles/snowflake-conversion.md">How the conversion works<span aria-hidden="true"> →</span></a></p>
</section>

<section class="pd-section pd-model">
  <h2>The model you'd have written</h2>
  <p>The tables <a href="../src/Persistord.Core/README.md"><code>Persistord.Core</code></a>, <a href="../src/Persistord.Messages/README.md"><code>Persistord.Messages</code></a> and <a href="../src/Persistord.History/README.md"><code>Persistord.History</code></a> map, and every column that points from one to another.</p>
  <figure class="pd-model-figure">
    <div class="pd-model-scroll" role="region" aria-label="Entity model diagram" tabindex="0">
      <svg class="pd-model-svg" viewBox="0 0 1120 590" role="img" aria-labelledby="pd-model-title pd-model-desc">
        <title id="pd-model-title">The Persistord entity model</title>
        <desc id="pd-model-desc">Persistord.Core maps GuildEntity, UserEntity, MemberEntity, RoleEntity and ChannelEntity. Its only foreign key is ChannelEntity.ParentId, which points at a parent ChannelEntity. ChannelEntity.GuildId, RoleEntity.GuildId and MemberEntity.GuildId refer to GuildEntity, MemberEntity.UserId and GuildEntity.OwnerId refer to UserEntity, and none of them is a foreign key. Persistord.Messages maps MessageEntity. Embed, AttachmentEntity and ReactionEntity each have a MessageId foreign key to MessageEntity, and EmbedField has an EmbedId foreign key to Embed, which also owns a footer and an author. MessageEntity.ChannelId refers to ChannelEntity and MessageEntity.AuthorId to UserEntity, without foreign keys. Persistord.History maps MessageHistoryEntity, whose MessageId is a foreign key to MessageEntity.</desc>
        <defs>
          <marker id="pd-model-arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="9" markerHeight="9" markerUnits="userSpaceOnUse" orient="auto"><path class="pd-model-head" d="M0 0 L10 5 L0 10 z"/></marker>
        </defs>
        <g class="pd-model-group" data-package="Persistord.Core">
          <rect class="pd-model-frame" x="1" y="40" width="518" height="400" rx="12"/>
          <text class="pd-model-package" x="21" y="68">Persistord.Core</text>
          <g class="pd-model-node" data-node="UserEntity"><rect x="40" y="96" width="180" height="60" rx="8"/><text class="pd-model-name" x="130" y="122">UserEntity</text><text class="pd-model-cols" x="130" y="142">Id</text></g>
          <g class="pd-model-node" data-node="GuildEntity"><rect x="300" y="96" width="180" height="60" rx="8"/><text class="pd-model-name" x="390" y="122">GuildEntity</text><text class="pd-model-cols" x="390" y="142">Id · OwnerId</text></g>
          <g class="pd-model-node" data-node="MemberEntity"><rect x="40" y="250" width="155" height="60" rx="8"/><text class="pd-model-name" x="117.5" y="276">MemberEntity</text><text class="pd-model-cols" x="117.5" y="296">key (GuildId, UserId)</text></g>
          <g class="pd-model-node" data-node="RoleEntity"><rect x="215" y="250" width="125" height="60" rx="8"/><text class="pd-model-name" x="277.5" y="276">RoleEntity</text><text class="pd-model-cols" x="277.5" y="296">GuildId</text></g>
          <g class="pd-model-node" data-node="ChannelEntity"><rect x="360" y="250" width="140" height="60" rx="8"/><text class="pd-model-name" x="430" y="276">ChannelEntity</text><text class="pd-model-cols" x="430" y="296">GuildId · ParentId</text></g>
        </g>
        <g class="pd-model-group" data-package="Persistord.Messages">
          <rect class="pd-model-frame" x="560" y="40" width="559" height="400" rx="12"/>
          <text class="pd-model-package" x="580" y="68">Persistord.Messages</text>
          <g class="pd-model-node" data-node="MessageEntity"><rect x="600" y="96" width="220" height="60" rx="8"/><text class="pd-model-name" x="710" y="122">MessageEntity</text><text class="pd-model-cols" x="710" y="142">ChannelId · AuthorId</text></g>
          <g class="pd-model-node" data-node="Embed"><rect x="580" y="250" width="150" height="78" rx="8"/><text class="pd-model-name" x="655" y="276">Embed</text><text class="pd-model-cols" x="655" y="296">MessageId</text><text class="pd-model-cols" x="655" y="314">owns Footer, Author</text></g>
          <g class="pd-model-node" data-node="EmbedField"><rect x="580" y="362" width="150" height="60" rx="8"/><text class="pd-model-name" x="655" y="388">EmbedField</text><text class="pd-model-cols" x="655" y="408">EmbedId</text></g>
          <g class="pd-model-node" data-node="AttachmentEntity"><rect x="770" y="250" width="166" height="60" rx="8"/><text class="pd-model-name" x="853" y="276">AttachmentEntity</text><text class="pd-model-cols" x="853" y="296">MessageId</text></g>
          <g class="pd-model-node" data-node="ReactionEntity"><rect x="955" y="250" width="150" height="60" rx="8"/><text class="pd-model-name" x="1030" y="276">ReactionEntity</text><text class="pd-model-cols" x="1030" y="296">MessageId</text></g>
        </g>
        <g class="pd-model-group" data-package="Persistord.History">
          <rect class="pd-model-frame" x="1" y="470" width="518" height="110" rx="12"/>
          <text class="pd-model-package" x="21" y="498">Persistord.History</text>
          <g class="pd-model-node" data-node="MessageHistoryEntity"><rect x="280" y="496" width="220" height="60" rx="8"/><text class="pd-model-name" x="390" y="522">MessageHistoryEntity</text><text class="pd-model-cols" x="390" y="542">MessageId</text></g>
        </g>
        <g class="pd-model-edges">
          <path class="pd-model-edge" data-kind="fk" data-from="ChannelEntity" data-to="ChannelEntity" data-column="ParentId" d="M400 310 C400 360 460 360 460 310" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="Embed" data-to="MessageEntity" data-column="MessageId" d="M655 250 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="AttachmentEntity" data-to="MessageEntity" data-column="MessageId" d="M853 250 L790 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="ReactionEntity" data-to="MessageEntity" data-column="MessageId" d="M1030 250 L812 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="EmbedField" data-to="Embed" data-column="EmbedId" d="M655 362 V328" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="fk" data-from="MessageHistoryEntity" data-to="MessageEntity" data-column="MessageId" d="M500 526 H750 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="GuildEntity" data-to="UserEntity" data-column="OwnerId" d="M300 126 H220" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MemberEntity" data-to="UserEntity" data-column="UserId" d="M100 250 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MemberEntity" data-to="GuildEntity" data-column="GuildId" d="M170 250 L330 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="RoleEntity" data-to="GuildEntity" data-column="GuildId" d="M290 250 L400 156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="ChannelEntity" data-to="GuildEntity" data-column="GuildId" d="M440 250 V156" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MessageEntity" data-to="ChannelEntity" data-column="ChannelId" d="M600 140 H540 V280 H500" marker-end="url(#pd-model-arrow)"/>
          <path class="pd-model-edge" data-kind="id" data-from="MessageEntity" data-to="UserEntity" data-column="AuthorId" d="M790 96 V20 H180 V96" marker-end="url(#pd-model-arrow)"/>
        </g>
      </svg>
    </div>
    <figcaption class="pd-model-legend">
      <span class="pd-legend-item"><svg class="pd-legend-swatch" viewBox="0 0 36 10" aria-hidden="true"><line class="pd-model-edge" data-kind="fk" x1="0" y1="5" x2="36" y2="5"/></svg>Configured foreign key</span>
      <span class="pd-legend-item"><svg class="pd-legend-swatch" viewBox="0 0 36 10" aria-hidden="true"><line class="pd-model-edge" data-kind="id" x1="0" y1="5" x2="36" y2="5"/></svg>Snowflake id column, no foreign key</span>
      <span>Arrows point at the entity a column refers to.</span>
    </figcaption>
  </figure>
</section>

<section class="pd-section pd-stack">
  <h2>Build your stack</h2>
  <ol class="pd-steps">
    <li class="pd-step">
      <h3>Add the model</h3>
      <p>The meta package brings <a href="../src/Persistord.Core/README.md"><code>Persistord.Core</code></a>, <a href="../src/Persistord.Messages/README.md"><code>Persistord.Messages</code></a> and <a href="../src/Persistord.History/README.md"><code>Persistord.History</code></a> in one reference.</p>
      <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
    </li>
    <li class="pd-step">
      <h3>Pick one adapter</h3>
      <p>An adapter maps one Discord library's objects to Persistord entities. Take at most one, or write the mapping yourself.</p>
      <div class="pd-adapters" data-pd-tabs="Discord library">
        <section class="pd-adapter" id="adapter-discordnet" data-pd-tab>
          <h4>Discord.Net</h4>
          <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord.Adapters.DiscordNet</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
<pre><code class="lang-csharp">db.Guilds.Add(guild.ToGuildEntity());
db.Members.Add(guildUser.ToMemberEntity());
db.Messages.Add(message.ToMessageEntity());</code></pre>
          <p class="pd-adapter-links"><a href="../src/Persistord.Adapters.DiscordNet/README.md"><code>Persistord.Adapters.DiscordNet</code></a> · <a href="articles/discord-net-adapter.md">Discord.Net guide</a></p>
        </section>
        <section class="pd-adapter" id="adapter-dsharpplus" data-pd-tab>
          <h4>DSharpPlus</h4>
          <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord.Adapters.DSharpPlus</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
<pre><code class="lang-csharp">db.Guilds.Add(guild.ToGuildEntity());
db.Members.Add(member.ToMemberEntity(guild.Id));
db.Messages.Add(message.ToMessageEntity());</code></pre>
          <p class="pd-adapter-links"><a href="../src/Persistord.Adapters.DSharpPlus/README.md"><code>Persistord.Adapters.DSharpPlus</code></a> · <a href="articles/dsharpplus-adapter.md">DSharpPlus guide</a></p>
        </section>
        <section class="pd-adapter" id="adapter-netcord" data-pd-tab>
          <h4>NetCord</h4>
          <div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord.Adapters.NetCord</code><button class="pd-copy" type="button" aria-live="polite">Copy</button></div>
<pre><code class="lang-csharp">db.Guilds.Add(guild.ToGuildEntity());
db.Members.Add(guildUser.ToMemberEntity());
db.Messages.Add(message.ToMessageEntity());</code></pre>
          <p class="pd-adapter-links"><a href="../src/Persistord.Adapters.NetCord/README.md"><code>Persistord.Adapters.NetCord</code></a> · <a href="articles/netcord-adapter.md">NetCord guide</a></p>
        </section>
      </div>
    </li>
    <li class="pd-step">
      <h3>Add what you need</h3>
      <ul class="pd-addons">
        <li><a href="../src/Persistord.Managed/README.md"><code>Persistord.Managed</code></a><span>The categories, channels, anchored messages and webhooks your bot owns.</span></li>
        <li><a href="../src/Persistord.Protection/README.md"><code>Persistord.Protection</code></a><span>Encrypts <code>[Protected]</code> string columns at rest.</span></li>
        <li><a href="../src/Persistord.Testing/README.md"><code>Persistord.Testing</code></a><span>In-memory SQLite fixtures and EF Core model assertions.</span></li>
      </ul>
    </li>
  </ol>
  <p class="pd-more"><a href="articles/packages.md">Compare all ten packages<span aria-hidden="true"> →</span></a></p>
</section>

<section class="pd-section pd-box">
  <h2>What's in the box</h2>
  <ul class="pd-features">
    <li><a class="pd-feature" href="articles/snowflake-conversion.md"><strong>Snowflake conversion</strong><span>Registered once in <code>ConfigureConventions</code>: every <code>ulong</code> and <code>ulong?</code> in your model, never an annotation.</span></a></li>
    <li><a class="pd-feature" href="articles/core-graph.md"><strong>Core graph</strong><span>Guilds, channels, users, members and roles — five skeleton entities you opt into, or ignore entirely.</span></a></li>
    <li><a class="pd-feature" href="articles/upsert.md"><strong>Upsert</strong><span>Insert-or-update keyed on the snowflake, for the gateway events that arrive out of order.</span></a></li>
    <li><a class="pd-feature" href="articles/soft-delete-and-query-filters.md"><strong>Soft-delete &amp; query filters</strong><span>Deleted messages stay addressable so history rows keep a valid foreign key.</span></a></li>
    <li><a class="pd-feature" href="articles/history.md"><strong>History</strong><span>Append-only edit history, one row per revision, with a real FK back to the message.</span></a></li>
    <li><a class="pd-feature" href="articles/guild-lifecycle.md"><strong>Guild purge</strong><span>Joining and leaving a guild, both directions, without orphan rows.</span></a></li>
    <li><a class="pd-feature" href="articles/managed-resources.md"><strong>Managed resources</strong><span>Track the Discord objects your bot created and owns, apart from the ones it only mirrors.</span></a></li>
    <li><a class="pd-feature" href="articles/protection.md"><strong>Protection</strong><span>Encrypt marked string columns at rest through ASP.NET Core Data Protection.</span></a></li>
    <li><a class="pd-feature" href="articles/testing.md"><strong>Testing fixtures</strong><span>In-memory SQLite contexts and assertions over the built EF Core model.</span></a></li>
  </ul>
</section>

<section class="pd-section pd-start">
  <h2>Start here</h2>
  <div class="pd-start-cards">
    <a class="pd-card" href="articles/getting-started.md"><strong>Getting Started</strong><span>Install, derive a context, pick a provider, save your first rows.</span></a>
    <a class="pd-card" href="articles/introduction.md"><strong>Guides</strong><span>Concepts, providers, adapters, add-ons and recipes, one topic per guide.</span></a>
    <a class="pd-card" href="api/index.md"><strong>API Reference</strong><span>Generated from the XML doc comments across the nine packages that ship code.</span></a>
  </div>
</section>

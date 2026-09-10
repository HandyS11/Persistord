---
title: Persistord
layout: landing
---

<section class="pd-hero">
  <h1>Every Discord bot rewrites the same tables. Persistord ships them.</h1>
  <p class="pd-hero-lede">An EF Core 10 model for the graph every bot ends up storing — guilds, channels, users, members and roles in <code>Persistord.Core</code>, messages in <code>Persistord.Messages</code>. It never selects your database provider and never references a Discord client library, so the model is the only thing you take.</p>
  <div class="pd-hero-actions">
    <a class="pd-btn pd-btn-primary" href="articles/getting-started.md">Get started</a>
    <a class="pd-btn" href="articles/packages.md">Browse packages</a>
    <a class="pd-btn" href="https://github.com/HandyS11/Persistord">View source</a>
  </div>
</section>

<section class="pd-section pd-transform" data-draw="running">
  <h2>A gateway payload, a mapper call, a row</h2>
  <p>Discord hands you a 64-bit <em>unsigned</em> id. An adapter maps the payload to <code>MessageEntity</code>. EF Core writes it to a signed column — and reads back exactly what went in.</p>
  <div class="pd-panes">
    <div class="pd-pane">
      <span class="pd-pane-bar">1 · gateway event</span>
      <pre>{
  "id": "<span class="pd-snowflake">1547396186112000000</span>",
  "channel_id": "1547300000000000000",
  "content": "gg"
}</pre>
    </div>
    <div class="pd-pane">
      <span class="pd-pane-bar">2 · .ToMessageEntity()</span>
      <pre>// Persistord.Adapters.DiscordNet
var row = message.ToMessageEntity();
db.Messages.Add(row);
await db.SaveChangesAsync();</pre>
    </div>
    <div class="pd-pane">
      <span class="pd-pane-bar">3 · messages row</span>
      <pre>column     type     value
---------  -------  -------------------
Id         BIGINT   <span class="pd-row-id">1547396186112000000</span>
ChannelId  BIGINT   1547300000000000000
Content    TEXT     'gg'</pre>
    </div>
  </div>
  <p class="pd-note"><strong><code>BIGINT</code> is signed; there is no unsigned 64-bit column. The same 64 bits go in and come back out — for every <code>ulong</code>, not just the ones that fit.</strong> Without a converter EF Core cannot map a <code>ulong</code> at all, so this was never a rounding problem — it is a model that will not build. <code>DiscordDbContext</code> registers <code>UlongToLongConverter</code> in <code>ConfigureConventions</code>, so every <code>ulong</code> and <code>ulong?</code> in your model converts globally and you never annotate an id. The cast is <code>unchecked</code>, so it is bit-faithful across all 2<sup>64</sup> values — including the ones past 2<sup>63</sup> that a Steam64 id reaches today and a Discord snowflake will not reach until roughly 2084. <a href="articles/snowflake-conversion.md">How the conversion works</a></p>
</section>

<div class="pd-install"><span class="pd-prompt">$</span><code>dotnet add package Persistord</code><button class="pd-copy" type="button">Copy</button></div>

<dl class="pd-stats">
  <div class="pd-stat"><dt>3</dt><dd>Discord libraries adapted</dd></div>
  <div class="pd-stat"><dt>5</dt><dd>core-graph entities</dd></div>
  <div class="pd-stat"><dt>0</dt><dd>provider dependencies</dd></div>
  <div class="pd-stat"><dt>10</dt><dd>NuGet packages</dd></div>
</dl>

<section class="pd-section">
  <h2>Ten packages</h2>
  <p>Take the meta package, add at most one adapter, and add the opt-in packages only if you need them.</p>
  <div class="pd-packages">
    <div class="pd-package-group">
      <h3>The stack</h3>
      <ul>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord"><code>Persistord</code><span>Meta package — Core, Messages and History in one reference.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Core"><code>Persistord.Core</code><span>Snowflake conversion, <code>DiscordDbContext</code>, the skeleton graph, upsert and purge.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Messages"><code>Persistord.Messages</code><span>Soft-deleted messages with embeds, attachments and reactions.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.History"><code>Persistord.History</code><span>Append-only edit history with a real foreign key to messages.</span></a></li>
      </ul>
    </div>
    <div class="pd-package-group">
      <h3>Adapters — pick at most one</h3>
      <ul>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Adapters.DiscordNet"><code>Persistord.Adapters.DiscordNet</code><span><code>.To*Entity()</code> mappers for Discord.Net types.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Adapters.DSharpPlus"><code>Persistord.Adapters.DSharpPlus</code><span><code>.To*Entity()</code> mappers for DSharpPlus types.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Adapters.NetCord"><code>Persistord.Adapters.NetCord</code><span><code>.To*Entity()</code> mappers for NetCord types.</span></a></li>
      </ul>
    </div>
    <div class="pd-package-group">
      <h3>Opt-in</h3>
      <ul>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Managed"><code>Persistord.Managed</code><span>The categories, channels, anchored messages and webhooks your bot owns.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Protection"><code>Persistord.Protection</code><span>Encrypts <code>[Protected]</code> string columns at rest.</span></a></li>
        <li><a class="pd-package" href="https://www.nuget.org/packages/Persistord.Testing"><code>Persistord.Testing</code><span>In-memory SQLite fixtures and EF Core model assertions.</span></a></li>
      </ul>
    </div>
  </div>
  <p class="pd-note">The <a href="articles/packages.md">packages page</a> has the full matrix, the dependency graph, and the reasoning behind which three stay out of the meta package.</p>
</section>

<section class="pd-section">
  <h2>What you get</h2>
  <p>Every capability below is a guide, not a bullet point.</p>
  <div class="pd-cards">
    <a class="pd-card" href="articles/snowflake-conversion.md"><strong>Snowflake conversion</strong><span>Registered once in <code>ConfigureConventions</code>: every <code>ulong</code> and <code>ulong?</code> in your model, never an annotation.</span></a>
    <a class="pd-card" href="articles/core-graph.md"><strong>Core graph</strong><span>Guilds, channels, users, members and roles — five skeleton entities you opt into, or ignore entirely.</span></a>
    <a class="pd-card" href="articles/upsert.md"><strong>Upsert</strong><span>Insert-or-update keyed on the snowflake, for the gateway events that arrive out of order.</span></a>
    <a class="pd-card" href="articles/soft-delete-and-query-filters.md"><strong>Soft-delete &amp; query filters</strong><span>Deleted messages stay addressable so history rows keep a valid foreign key.</span></a>
    <a class="pd-card" href="articles/history.md"><strong>History</strong><span>Append-only edit history, one row per revision, with a real FK back to the message.</span></a>
    <a class="pd-card" href="articles/guild-lifecycle.md"><strong>Guild purge</strong><span>Joining and leaving a guild, both directions, without orphan rows.</span></a>
    <a class="pd-card" href="articles/managed-resources.md"><strong>Managed resources</strong><span>Track the Discord objects your bot created and owns, apart from the ones it only mirrors.</span></a>
    <a class="pd-card" href="articles/protection.md"><strong>Protection</strong><span>Encrypt marked string columns at rest through ASP.NET Core Data Protection.</span></a>
    <a class="pd-card" href="articles/testing.md"><strong>Testing fixtures</strong><span>In-memory SQLite contexts and assertions over the built EF Core model.</span></a>
  </div>
</section>

<section class="pd-section">
  <h2>Where to go next</h2>
  <div class="pd-next">
    <a class="pd-card" href="articles/getting-started.md"><strong>Getting Started</strong><span>Install, derive a context, pick a provider, save your first rows.</span></a>
    <a class="pd-card" href="articles/core-graph.md"><strong>Core Graph</strong><span>The conventions-only base context, the opt-in graph, and the entities it maps.</span></a>
    <a class="pd-card" href="articles/adapters.md"><strong>Choosing an Adapter</strong><span>Discord.Net, DSharpPlus and NetCord compared, mapper by mapper.</span></a>
    <a class="pd-card" href="articles/providers.md"><strong>Providers</strong><span>What changes on PostgreSQL, SQL Server and SQLite — and what does not.</span></a>
    <a class="pd-card" href="articles/recipes.md"><strong>Recipes</strong><span>Short answers to the questions that come up once the model is wired.</span></a>
    <a class="pd-card" href="articles/troubleshooting.md"><strong>Troubleshooting</strong><span>The errors you are most likely to hit, and what each one actually means.</span></a>
    <a class="pd-card" href="api/index.md"><strong>API Reference</strong><span>Generated from the XML doc comments across all ten packages.</span></a>
  </div>
</section>

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Persistord.Managed;
using Persistord.Managed.Entities;
using Persistord.Testing;
using Xunit;

namespace Persistord.Protection.Tests;

/// <summary>
/// Pins the one cross-package claim that justifies putting <c>ProtectedAttribute</c> in
/// <c>Persistord.Core.Abstractions</c>: that <c>ApplyProtection</c> actually encrypts
/// <see cref="ManagedWebhook.Token"/>, Persistord's own shipped <c>[Protected]</c> column, not just
/// a bespoke test entity shaped like it. Nothing else would catch the attribute being dropped from
/// <see cref="ManagedWebhook.Token"/>, moved, or made non-<see cref="string"/>.
/// </summary>
public class ManagedWebhookProtectionTests
{
    [Fact]
    public async Task ApplyProtection_encrypts_ManagedWebhook_Token()
    {
        await using var database = SqliteTestDatabase.Private(TestSchema.EnsureCreated);
        await using var context =
            database.CreateContext<ManagedWebhookContext>(o => new ManagedWebhookContext(o, new ReversingProvider()));

        await context.Webhooks.AddAsync(new ManagedWebhook
        {
            GuildId = 1UL, Key = "announcements", ChannelDiscordId = 2UL, Token = "webhook-token-value"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Token\" FROM \"ManagedWebhooks\"";
        var stored = await command.ExecuteScalarAsync() as string;

        Assert.NotEqual("webhook-token-value", stored);

        var row = await context.Webhooks.SingleAsync();
        Assert.Equal("webhook-token-value", row.Token);
    }

    /// <summary>
    /// A stand-in protector: reverses the payload. The converter only ever calls
    /// Protect/Unprotect, so a fake exercises the wiring faithfully without a key ring.
    /// </summary>
    private sealed class ReversingProvider : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => [.. plaintext.Reverse()];

        public byte[] Unprotect(byte[] protectedData) => [.. protectedData.Reverse()];
    }
}

public sealed class ManagedWebhookContext(
    DbContextOptions<ManagedWebhookContext> options,
    IDataProtectionProvider dataProtectionProvider) : Persistord.Core.DiscordDbContext(options)
{
    public DbSet<ManagedWebhook> Webhooks => Set<ManagedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyManagedModule();
        modelBuilder.ApplyProtection(dataProtectionProvider);
    }
}

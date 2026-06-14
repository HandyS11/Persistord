using Microsoft.EntityFrameworkCore;
using Persistord.Messages;
using Persistord.Messages.Configurations;
using Xunit;

namespace Persistord.Messages.Tests;

/// <summary>Pins the <c>ArgumentNullException.ThrowIfNull</c> guards on the Messages
/// configurations and the model-builder extension.</summary>
public class MessagesNullGuardTests
{
    [Fact]
    public void ApplyMessagesModule_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => ((ModelBuilder)null!).ApplyMessagesModule());

    [Fact]
    public void MessageConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() =>
            new MessageEntityConfiguration(filterDeleted: true).Configure(null!));

    [Fact]
    public void EmbedConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new EmbedEntityConfiguration().Configure(null!));

    [Fact]
    public void AttachmentConfiguration_throws_on_null() =>
        Assert.Throws<ArgumentNullException>(() => new AttachmentEntityConfiguration().Configure(null!));
}

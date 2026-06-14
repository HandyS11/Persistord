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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Xunit;

namespace Persistord.Messages.Tests;

/// <summary>
/// Asserts the EF model metadata produced by the Messages configurations: the
/// channel/message lookup index and the explicit child foreign keys that point back to
/// the owning row (rather than the shadow FKs EF would invent by convention).
/// </summary>
public class MessagesConfigurationTests
{
    private static IModel BuildModel()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            return context.Model;
        }
    }

    [Fact]
    public void Message_is_indexed_by_channel_then_id()
    {
        var indexes = BuildModel().FindEntityType(typeof(MessageEntity))!.GetIndexes();
        Assert.Contains(
            indexes,
            i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(MessageEntity.ChannelId), nameof(MessageEntity.Id)]));
    }

    [Theory]
    [InlineData(typeof(Embed), nameof(Embed.MessageId))]
    [InlineData(typeof(AttachmentEntity), nameof(AttachmentEntity.MessageId))]
    [InlineData(typeof(ReactionEntity), nameof(ReactionEntity.MessageId))]
    public void Message_children_use_explicit_message_foreign_key(Type child, string fkProperty)
    {
        var entity = BuildModel().FindEntityType(child)!;
        var fk = Assert.Single(entity.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MessageEntity));
        Assert.Equal(fkProperty, Assert.Single(fk.Properties).Name);
    }

    [Fact]
    public void EmbedField_uses_explicit_embed_foreign_key()
    {
        var field = BuildModel().FindEntityType(typeof(EmbedField))!;
        var fk = Assert.Single(field.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Embed));
        Assert.Equal(nameof(EmbedField.EmbedId), Assert.Single(fk.Properties).Name);
    }
}

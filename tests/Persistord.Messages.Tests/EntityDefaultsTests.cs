using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Xunit;

namespace Persistord.Messages.Tests;

/// <summary>Pins the <c>= string.Empty</c> defaults on required string properties.</summary>
public class EntityDefaultsTests
{
    [Fact]
    public void AttachmentEntity_filename_defaults_to_empty() =>
        Assert.Equal(string.Empty, new AttachmentEntity().FileName);

    [Fact]
    public void AttachmentEntity_url_defaults_to_empty() =>
        Assert.Equal(string.Empty, new AttachmentEntity().Url);

    [Fact]
    public void ReactionEntity_emoji_defaults_to_empty() =>
        Assert.Equal(string.Empty, new ReactionEntity().Emoji);

    [Fact]
    public void EmbedField_name_defaults_to_empty() =>
        Assert.Equal(string.Empty, new EmbedField().Name);

    [Fact]
    public void EmbedField_value_defaults_to_empty() =>
        Assert.Equal(string.Empty, new EmbedField().Value);
}

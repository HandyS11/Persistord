using Microsoft.EntityFrameworkCore;
using Persistord.Messages.Entities;
using Persistord.Messages.Owned;
using Xunit;

namespace Persistord.Messages.Tests;

public class MessagesModelTests
{
    [Fact]
    public void Message_WithEmbedsAndChildren_RoundTrips()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            context.Messages.Add(new MessageEntity
            {
                Id = 10UL,
                ChannelId = 20UL,
                AuthorId = 30UL,
                Content = "hello",
                Embeds = { new Embed { Title = "t", Fields = { new EmbedField { Name = "n", Value = "v" } } } },
                Attachments = { new AttachmentEntity { Id = 1UL, FileName = "a.png", Url = "http://x" } },
                Reactions = { new ReactionEntity { Emoji = "👍", Count = 2 } },
            });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var loaded = context.Messages
                .Include(m => m.Embeds).ThenInclude(e => e.Fields)
                .Include(m => m.Attachments)
                .Include(m => m.Reactions)
                .Single(m => m.Id == 10UL);

            Assert.Single(loaded.Embeds);
            Assert.Single(loaded.Embeds[0].Fields);
            Assert.Single(loaded.Attachments);
            Assert.Single(loaded.Reactions);
        }
    }
}

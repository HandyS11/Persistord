using Microsoft.EntityFrameworkCore;
using Persistord.History.Entities;
using Persistord.Messages.Entities;
using Xunit;

namespace Persistord.History.Tests;

/// <summary>Asserts the message foreign key the history configuration declares: it must
/// target <see cref="MessageEntity"/> through the snowflake <c>MessageId</c> and restrict
/// deletes so history outlives a (soft-)deleted message.</summary>
public class HistoryConfigurationTests
{
    [Fact]
    public void History_has_restricting_message_foreign_key()
    {
        var (connection, context) = TestContext.Create();
        using (connection)
        using (context)
        {
            var history = context.Model.FindEntityType(typeof(MessageHistoryEntity))!;
            var fk = Assert.Single(
                history.GetForeignKeys(),
                f => f.PrincipalEntityType.ClrType == typeof(MessageEntity));

            Assert.Equal(nameof(MessageHistoryEntity.MessageId), Assert.Single(fk.Properties).Name);
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }
    }
}

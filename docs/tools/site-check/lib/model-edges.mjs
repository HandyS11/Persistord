/* Whitespace-insensitive source, so a fluent chain split over lines matches. */
export const flatten = text => text.replace(/\s+/g, ' ')

/*
 * Every edge of the model Persistord ships. `fk`: a foreign key the model
 * configures, proven by the `evidence` statements ([repo path, statement]).
 * `id`: a snowflake column that refers to another entity with no foreign key.
 * Edges run from the entity holding the column to the entity it refers to.
 * The landing diagram and the Concepts articles' ER diagrams both draw these.
 */
const fk = (from, to, column, ...evidence) => ({ kind: 'fk', from, to, column, evidence })
const id = (from, to, column) => ({ kind: 'id', from, to, column })

const MESSAGE = 'src/Persistord.Messages/Entities/MessageEntity.cs'
const MESSAGE_CONFIG = 'src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs'

export const EDGES = [
  fk('ChannelEntity', 'ChannelEntity', 'ParentId',
    ['src/Persistord.Core/Configurations/ChannelEntityConfiguration.cs', 'builder.HasOne<ChannelEntity>() .WithMany() .HasForeignKey(c => c.ParentId)']),
  fk('Embed', 'MessageEntity', 'MessageId',
    [MESSAGE, 'public List<Embed> Embeds {'],
    [MESSAGE_CONFIG, 'builder.HasMany(m => m.Embeds).WithOne().HasForeignKey(e => e.MessageId);']),
  fk('AttachmentEntity', 'MessageEntity', 'MessageId',
    [MESSAGE, 'public List<AttachmentEntity> Attachments {'],
    [MESSAGE_CONFIG, 'builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);']),
  fk('ReactionEntity', 'MessageEntity', 'MessageId',
    [MESSAGE, 'public List<ReactionEntity> Reactions {'],
    [MESSAGE_CONFIG, 'builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);']),
  fk('EmbedField', 'Embed', 'EmbedId',
    ['src/Persistord.Messages/Owned/Embed.cs', 'public List<EmbedField> Fields {'],
    ['src/Persistord.Messages/Configurations/EmbedEntityConfiguration.cs', 'builder.HasMany(e => e.Fields).WithOne().HasForeignKey(f => f.EmbedId);']),
  fk('MessageHistoryEntity', 'MessageEntity', 'MessageId',
    ['src/Persistord.History/Configurations/MessageHistoryEntityConfiguration.cs', 'builder.HasOne<MessageEntity>() .WithMany() .HasForeignKey(h => h.MessageId)']),
  id('GuildEntity', 'UserEntity', 'OwnerId'),
  id('MemberEntity', 'UserEntity', 'UserId'),
  id('MemberEntity', 'GuildEntity', 'GuildId'),
  id('RoleEntity', 'GuildEntity', 'GuildId'),
  id('ChannelEntity', 'GuildEntity', 'GuildId'),
  id('MessageEntity', 'ChannelEntity', 'ChannelId'),
  id('MessageEntity', 'UserEntity', 'AuthorId'),
]

export const edgeKey = ({ kind, from, column, to }) => `${kind} ${from}.${column} -> ${to}`

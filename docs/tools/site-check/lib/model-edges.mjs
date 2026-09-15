/* Whitespace-insensitive source, so a fluent chain split over lines matches. */
export const flatten = text => text.replace(/\s+/g, ' ')

/*
 * Every edge of the model Persistord ships. `fk`: a foreign key the model
 * configures, proven by the `evidence` statements ([repo path, statement]).
 * `id`: a snowflake column that refers to another entity with no foreign key.
 * Edges run from the entity holding the column to the entity it refers to.
 * The landing diagram and the Concepts articles' ER diagrams both draw these.
 */
export const EDGES = [
  {
    kind: 'fk', from: 'ChannelEntity', to: 'ChannelEntity', column: 'ParentId',
    evidence: [
      ['src/Persistord.Core/Configurations/ChannelEntityConfiguration.cs', 'builder.HasOne<ChannelEntity>() .WithMany() .HasForeignKey(c => c.ParentId)'],
    ],
  },
  {
    kind: 'fk', from: 'Embed', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<Embed> Embeds {'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Embeds).WithOne().HasForeignKey(e => e.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'AttachmentEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<AttachmentEntity> Attachments {'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'ReactionEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.Messages/Entities/MessageEntity.cs', 'public List<ReactionEntity> Reactions {'],
      ['src/Persistord.Messages/Configurations/MessageEntityConfiguration.cs', 'builder.HasMany(m => m.Reactions).WithOne().HasForeignKey(r => r.MessageId);'],
    ],
  },
  {
    kind: 'fk', from: 'EmbedField', to: 'Embed', column: 'EmbedId',
    evidence: [
      ['src/Persistord.Messages/Owned/Embed.cs', 'public List<EmbedField> Fields {'],
      ['src/Persistord.Messages/Configurations/EmbedEntityConfiguration.cs', 'builder.HasMany(e => e.Fields).WithOne().HasForeignKey(f => f.EmbedId);'],
    ],
  },
  {
    kind: 'fk', from: 'MessageHistoryEntity', to: 'MessageEntity', column: 'MessageId',
    evidence: [
      ['src/Persistord.History/Configurations/MessageHistoryEntityConfiguration.cs', 'builder.HasOne<MessageEntity>() .WithMany() .HasForeignKey(h => h.MessageId)'],
    ],
  },
  { kind: 'id', from: 'GuildEntity', to: 'UserEntity', column: 'OwnerId' },
  { kind: 'id', from: 'MemberEntity', to: 'UserEntity', column: 'UserId' },
  { kind: 'id', from: 'MemberEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'RoleEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'ChannelEntity', to: 'GuildEntity', column: 'GuildId' },
  { kind: 'id', from: 'MessageEntity', to: 'ChannelEntity', column: 'ChannelId' },
  { kind: 'id', from: 'MessageEntity', to: 'UserEntity', column: 'AuthorId' },
]

export const edgeKey = ({ kind, from, column, to }) => `${kind} ${from}.${column} -> ${to}`

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistord.Sample.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<long>(type: "INTEGER", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Channels_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => new { x.GuildId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<long>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Permissions = table.Column<long>(type: "INTEGER", nullable: false),
                    Color = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    GlobalName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttachmentEntity_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Embed",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<int>(type: "INTEGER", nullable: true),
                    Footer_Text = table.Column<string>(type: "TEXT", nullable: true),
                    Footer_IconUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Author_Name = table.Column<string>(type: "TEXT", nullable: true),
                    Author_Url = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Embed", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Embed_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ChangeType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageHistory_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReactionEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactionEntity_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmbedField",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmbedId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Inline = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbedField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmbedField_Embed_EmbedId",
                        column: x => x.EmbedId,
                        principalTable: "Embed",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentEntity_MessageId",
                table: "AttachmentEntity",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_ParentId",
                table: "Channels",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Embed_MessageId",
                table: "Embed",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbedField_EmbedId",
                table: "EmbedField",
                column: "EmbedId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageHistory_MessageId_RecordedAt",
                table: "MessageHistory",
                columns: new[] { "MessageId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChannelId_Id",
                table: "Messages",
                columns: new[] { "ChannelId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ReactionEntity_MessageId",
                table: "ReactionEntity",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_GuildId",
                table: "Roles",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentEntity");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "EmbedField");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "MessageHistory");

            migrationBuilder.DropTable(
                name: "ReactionEntity");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Embed");

            migrationBuilder.DropTable(
                name: "Messages");
        }
    }
}

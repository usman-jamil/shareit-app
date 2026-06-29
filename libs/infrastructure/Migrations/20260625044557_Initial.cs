using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "public");

        migrationBuilder.CreateTable(
            name: "users",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                email = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "api_keys",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                key_id = table.Column<string>(type: "text", nullable: false),
                key_hash = table.Column<string>(type: "text", nullable: false),
                prefix = table.Column<string>(type: "text", nullable: false),
                label = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_api_keys", x => x.id);
                table.ForeignKey(
                    name: "fk_api_keys_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "shares",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                configured_ttl_minutes = table.Column<int>(type: "integer", nullable: false),
                total_bytes = table.Column<long>(type: "bigint", nullable: false),
                file_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_shares", x => x.id);
                table.ForeignKey(
                    name: "fk_shares_users_owner_user_id",
                    column: x => x.owner_user_id,
                    principalSchema: "public",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "files",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                share_id = table.Column<Guid>(type: "uuid", nullable: false),
                relative_path = table.Column<string>(type: "text", nullable: false),
                sha256 = table.Column<string>(type: "text", nullable: false),
                content_type = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                size = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_files", x => x.id);
                table.ForeignKey(
                    name: "fk_files_shares_share_id",
                    column: x => x.share_id,
                    principalSchema: "public",
                    principalTable: "shares",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_key_id",
            schema: "public",
            table: "api_keys",
            column: "key_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_user_id",
            schema: "public",
            table: "api_keys",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_files_share_id",
            schema: "public",
            table: "files",
            column: "share_id");

        migrationBuilder.CreateIndex(
            name: "ix_shares_expires_at",
            schema: "public",
            table: "shares",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_shares_owner_user_id",
            schema: "public",
            table: "shares",
            column: "owner_user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "api_keys",
            schema: "public");

        migrationBuilder.DropTable(
            name: "files",
            schema: "public");

        migrationBuilder.DropTable(
            name: "shares",
            schema: "public");

        migrationBuilder.DropTable(
            name: "users",
            schema: "public");
    }
}

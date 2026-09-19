using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LH.Main.Backend.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameIdentityColumnsToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_password_credentials_users_UserId",
                table: "password_credentials");

            migrationBuilder.DropForeignKey(
                name: "FK_player_profiles_users_UserId",
                table: "player_profiles");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "NormalizedUsername",
                table: "users",
                newName: "normalized_username");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "users",
                newName: "created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_users_NormalizedUsername",
                table: "users",
                newName: "ix_users_normalized_username");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "player_profiles",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "player_profiles",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "player_profiles",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "password_credentials",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "password_credentials",
                newName: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_password_credentials_users_user_id",
                table: "password_credentials",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_player_profiles_users_user_id",
                table: "player_profiles",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("ALTER TABLE users RENAME CONSTRAINT \"PK_users\" TO pk_users;");
            migrationBuilder.Sql("ALTER TABLE password_credentials RENAME CONSTRAINT \"PK_password_credentials\" TO pk_password_credentials;");
            migrationBuilder.Sql("ALTER TABLE player_profiles RENAME CONSTRAINT \"PK_player_profiles\" TO pk_player_profiles;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_password_credentials_users_user_id",
                table: "password_credentials");

            migrationBuilder.DropForeignKey(
                name: "fk_player_profiles_users_user_id",
                table: "player_profiles");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "normalized_username",
                table: "users",
                newName: "NormalizedUsername");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "users",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "ix_users_normalized_username",
                table: "users",
                newName: "IX_users_NormalizedUsername");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "player_profiles",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "player_profiles",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "player_profiles",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "password_credentials",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "password_credentials",
                newName: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_password_credentials_users_UserId",
                table: "password_credentials",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_player_profiles_users_UserId",
                table: "player_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("ALTER TABLE users RENAME CONSTRAINT pk_users TO \"PK_users\";");
            migrationBuilder.Sql("ALTER TABLE password_credentials RENAME CONSTRAINT pk_password_credentials TO \"PK_password_credentials\";");
            migrationBuilder.Sql("ALTER TABLE player_profiles RENAME CONSTRAINT pk_player_profiles TO \"PK_player_profiles\";");
        }
    }
}

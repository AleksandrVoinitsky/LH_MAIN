using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LH.Main.Backend.Api.Persistence.Migrations;

public partial class InitialIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                NormalizedUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "password_credentials",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_password_credentials", x => x.UserId);
                table.ForeignKey("FK_password_credentials_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "player_profiles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_player_profiles", x => x.UserId);
                table.ForeignKey("FK_player_profiles_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_users_NormalizedUsername",
            table: "users",
            column: "NormalizedUsername",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "password_credentials");
        migrationBuilder.DropTable(name: "player_profiles");
        migrationBuilder.DropTable(name: "users");
    }
}

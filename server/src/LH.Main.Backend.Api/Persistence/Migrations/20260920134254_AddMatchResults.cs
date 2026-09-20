using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LH.Main.Backend.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_match_results_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_result_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    survived_seconds = table.Column<int>(type: "integer", nullable: false),
                    damage_taken = table.Column<int>(type: "integer", nullable: false),
                    damage_applied = table.Column<int>(type: "integer", nullable: false),
                    reward_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_result_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_match_result_participants_match_results_match_result_id",
                        column: x => x.match_result_id,
                        principalTable: "match_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reward_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reward_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reward_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_reward_transactions_match_results_match_result_id",
                        column: x => x.match_result_id,
                        principalTable: "match_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_match_result_participants_match_result_id",
                table: "match_result_participants",
                column: "match_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_results_match_id",
                table: "match_results",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reward_transactions_match_result_id",
                table: "reward_transactions",
                column: "match_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_reward_transactions_player_result_reward",
                table: "reward_transactions",
                columns: new[] { "player_id", "match_result_id", "reward_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_result_participants");

            migrationBuilder.DropTable(
                name: "reward_transactions");

            migrationBuilder.DropTable(
                name: "match_results");
        }
    }
}

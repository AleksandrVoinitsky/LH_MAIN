using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LH.Main.Backend.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_server_slots",
                columns: table => new
                {
                    server_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    public_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    public_port = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_match_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_server_slots", x => x.server_id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_game_server_slots_server_id",
                        column: x => x.server_id,
                        principalTable: "game_server_slots",
                        principalColumn: "server_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_users_player_id",
                        column: x => x.player_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_queue_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_match_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_queue_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_queue_entries_matches_assigned_match_id",
                        column: x => x.assigned_match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_match_queue_entries_users_player_id",
                        column: x => x.player_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ticket_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_tickets_game_server_slots_server_id",
                        column: x => x.server_id,
                        principalTable: "game_server_slots",
                        principalColumn: "server_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_match_tickets_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_tickets_users_player_id",
                        column: x => x.player_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_server_slots_status",
                table: "game_server_slots",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_match_queue_entries_assigned_match_id",
                table: "match_queue_entries",
                column: "assigned_match_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_queue_entries_player_id_status",
                table: "match_queue_entries",
                columns: new[] { "player_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_match_tickets_match_id",
                table: "match_tickets",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_tickets_player_id",
                table: "match_tickets",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_tickets_server_id",
                table: "match_tickets",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_tickets_ticket_hash",
                table: "match_tickets",
                column: "ticket_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_player_id_status",
                table: "matches",
                columns: new[] { "player_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_server_id",
                table: "matches",
                column: "server_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_queue_entries");

            migrationBuilder.DropTable(
                name: "match_tickets");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "game_server_slots");
        }
    }
}

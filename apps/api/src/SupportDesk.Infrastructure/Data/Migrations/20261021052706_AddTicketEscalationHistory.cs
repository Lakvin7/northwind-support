using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEscalationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    FromPriority = table.Column<int>(type: "int", nullable: false),
                    ToPriority = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PreviousDueAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    NewDueAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PreviousAgentId = table.Column<int>(type: "int", nullable: true),
                    NewAgentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalations", x => x.Id);
                    table.CheckConstraint("CK_TicketEscalations_FromPriority", "[FromPriority] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_TicketEscalations_ReasonLength", "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 5 AND 500");
                    table.CheckConstraint("CK_TicketEscalations_ToPriority", "[ToPriority] = [FromPriority] + 1");
                    table.ForeignKey(
                        name: "FK_TicketEscalations_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_TicketId_EscalatedAtUtc",
                table: "TicketEscalations",
                columns: new[] { "TicketId", "EscalatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketEscalations");
        }
    }
}

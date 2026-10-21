using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.TicketEscalations', N'EscalatedBy') IS NULL
                BEGIN
                    ALTER TABLE [TicketEscalations] ADD [EscalatedBy] nvarchar(100) NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Agents', N'PasswordHash') IS NULL
                BEGIN
                    ALTER TABLE [Agents] ADD [PasswordHash] nvarchar(512) NULL;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.TicketEscalations', N'EscalatedBy') IS NOT NULL
                BEGIN
                    ALTER TABLE [TicketEscalations] DROP COLUMN [EscalatedBy];
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Agents', N'PasswordHash') IS NOT NULL
                BEGIN
                    ALTER TABLE [Agents] DROP COLUMN [PasswordHash];
                END;
                """);
        }
    }
}

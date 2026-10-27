using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Etamid.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Cycle3Deals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractHash",
                table: "Deals",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractSnapshot",
                table: "Deals",
                type: "nvarchar(max)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedById",
                table: "Deals",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputedAt",
                table: "Deals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionEndsAt",
                table: "Deals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_Type",
                table: "LedgerAccounts",
                column: "Type",
                unique: true,
                filter: "[OwnerUserId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_Status_InspectionEndsAt",
                table: "Deals",
                columns: new[] { "Status", "InspectionEndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerAccounts_Type",
                table: "LedgerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Deals_Status_InspectionEndsAt",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ContractHash",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ContractSnapshot",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "DisputedAt",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "InspectionEndsAt",
                table: "Deals");
        }
    }
}

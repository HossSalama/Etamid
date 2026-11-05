using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Etamid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DisputeSettlementProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InfoDeadlineAt",
                table: "Disputes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisputeSettlementProposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisputeId = table.Column<long>(type: "bigint", nullable: false),
                    ProposedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SellerPercent = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeSettlementProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeSettlementProposals_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisputeSettlementProposals_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisputeSettlementProposals_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeSettlementProposals_DecidedByUserId",
                table: "DisputeSettlementProposals",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeSettlementProposals_DisputeId",
                table: "DisputeSettlementProposals",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeSettlementProposals_ProposedByUserId",
                table: "DisputeSettlementProposals",
                column: "ProposedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisputeSettlementProposals");

            migrationBuilder.DropColumn(
                name: "InfoDeadlineAt",
                table: "Disputes");
        }
    }
}

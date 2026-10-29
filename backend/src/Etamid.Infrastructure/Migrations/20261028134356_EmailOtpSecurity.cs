using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Etamid.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailOtpSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailOtp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EmailNormalized = table.Column<string>(type: "nvarchar(190)", maxLength: 190, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResendAvailableAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResetTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResetTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResetTokenUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOtp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailOtp_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtp_Purpose_ExpiresAt",
                table: "EmailOtp",
                columns: new[] { "Purpose", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtp_UserId_Purpose",
                table: "EmailOtp",
                columns: new[] { "UserId", "Purpose" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOtp");
        }
    }
}

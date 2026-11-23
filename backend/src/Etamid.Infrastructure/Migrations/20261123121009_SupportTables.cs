using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Etamid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-added: the model snapshot already contained SupportTickets
            // and SupportTicketMessages (drift: no earlier migration ever
            // created them), so scaffolding skipped both tables and only
            // generated GuestContactMessages below. The definitions here are
            // copied from the model (Designer.cs) — verified by scaffolding
            // a follow-up migration afterwards: it must come out empty.
            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignedAdminId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_AdminUsers_AssignedAdminId",
                        column: x => x.AssignedAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Lookups_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Lookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    SenderAdminId = table.Column<long>(type: "bigint", nullable: true),
                    SenderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    TicketId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_AdminUsers_SenderAdminId",
                        column: x => x.SenderAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuestContactMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestContactMessages", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "AnswerAr", "AnswerEn" },
                values: new object[] { "منصة اعتمد هي منصة وساطة مالية تحفظ أموال الصفقة بين المشتري والبائع حتى يتم التسليم والموافقة على المنتج.", "Etamid is a financial escrow platform that holds the deal amount between the buyer and the seller until the product is delivered and approved." });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "AnswerAr", "AnswerEn", "QuestionAr", "QuestionEn" },
                values: new object[] { "سجّل برقم جوالك وكلمة مرور، ثم أدخل رمز التحقق المرسل إلى بريدك الإلكتروني. بعدها يمكنك إكمال بيانات حسابك، وإذا كنت بائعاً أنشئ متجرك من صفحة حسابي.", "Register with your phone number and a password, then enter the verification code sent to your email. After that you can complete your profile, and if you are a seller, create your store from your account page.", "كيف يمكنني إنشاء حساب جديد؟", "How do I create a new account?" });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "AnswerAr", "AnswerEn", "QuestionAr", "QuestionEn" },
                values: new object[] { "ادفع من صفحة الصفقة بعد قبولها؛ يتم حجز المبلغ في حساب الضمان ولا يصل إلى البائع إلا بعد استلامك المنتج وموافقتك عليه.", "Pay from the deal page after it is accepted; the amount is held in the escrow account and reaches the seller only after you receive the product and approve it.", "كيف يمكنني الدفع في المنصة؟", "How do I pay on the platform?" });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "AnswerAr", "AnswerEn", "QuestionAr", "QuestionEn" },
                values: new object[] { "نعم، بياناتك محمية ولا تُشارك مع الطرف الآخر إلا ما يلزم لإتمام الصفقة، وأموال الصفقات محفوظة في حساب الضمان.", "Yes, your data is protected and is only shared with the other party as needed to complete the deal, and deal funds are held in the escrow account.", "هل بياناتي الشخصية والمالية آمنة؟", "Is my personal and financial data safe?" });

            migrationBuilder.InsertData(
                table: "Faqs",
                columns: new[] { "Id", "AnswerAr", "AnswerEn", "IsActive", "QuestionAr", "QuestionEn", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 5L, "رسوم المنصة ثابتة قدرها 50 ريال على البائع لكل صفقة، ولا توجد رسوم على المشتري.", "The platform fee is a fixed 50 SAR per deal, paid by the seller. There is no fee for the buyer.", true, "هل توجد رسوم لاستخدام المنصة؟", "Are there fees for using the platform?", 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6L, "من صفحة صفقاتي تتابع حالة كل صفقة خطوة بخطوة، ومن صفحة طلبات الدعم تتابع ردود فريق الدعم على طلباتك.", "From the My Deals page you can follow each deal step by step, and from the Support Requests page you can follow the support team's replies to your requests.", true, "كيف يمكنني متابعة طلباتي؟", "How do I track my requests?", 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Lookups",
                columns: new[] { "Id", "Code", "IconFileId", "IsActive", "NameAr", "NameEn", "SortOrder", "Type" },
                values: new object[,]
                {
                    { 26L, "payment", null, true, "الدفع والتحويلات", "Payments and transfers", 1, "ticket_subject" },
                    { 27L, "deal", null, true, "صفقة", "A deal", 2, "ticket_subject" },
                    { 28L, "account", null, true, "الحساب والتحقق", "Account and verification", 3, "ticket_subject" },
                    { 29L, "technical", null, true, "مشكلة تقنية", "Technical issue", 4, "ticket_subject" },
                    { 30L, "other", null, true, "موضوع آخر", "Other", 5, "ticket_subject" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestContactMessages_CreatedAt",
                table: "GuestContactMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedAdminId",
                table: "SupportTickets",
                column: "AssignedAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_DealId",
                table: "SupportTickets",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Number",
                table: "SupportTickets",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_SubjectId",
                table: "SupportTickets",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId_CreatedAt",
                table: "SupportTickets",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_FileId",
                table: "SupportTicketMessages",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_SenderAdminId",
                table: "SupportTicketMessages",
                column: "SenderAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_SenderUserId",
                table: "SupportTicketMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_TicketId_CreatedAt",
                table: "SupportTicketMessages",
                columns: new[] { "TicketId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportTicketMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "GuestContactMessages");

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 30L);

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "AnswerAr", "AnswerEn" },
                values: new object[] { "منصة وساطة مالية تحفظ أموال الصفقة بين المشتري والبائع حتى يتم التسليم والموافقة.", "A financial escrow platform that holds the deal amount between buyer and seller until delivery is confirmed." });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "AnswerAr", "AnswerEn", "QuestionAr", "QuestionEn" },
                values: new object[] { "رسوم ثابتة قدرها 50 ريال على البائع لكل صفقة.", "A fixed fee of 50 SAR per deal, paid by the seller.", "كم رسوم المنصة؟", "What are the platform fees?" });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "AnswerAr", "AnswerEn", "QuestionAr", "QuestionEn" },
                values: new object[] { "الدفع عن طريق التحويل البنكي إلى حساب المنصة وسيتم تأكيده بعد التحقق.", "Payment is by bank transfer to the platform account and is confirmed after verification.", "كيف أدفع قيمة الصفقة؟", "How do I pay for a deal?" });

            migrationBuilder.UpdateData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "AnswerAr", "AnswerEn", "QuestionAr", "QuestionEn" },
                values: new object[] { "يمكنك فتح نزاع من صفحة الصفقة وسيتم تجميد المبلغ حتى صدور القرار.", "You can open a dispute from the deal page and the amount stays frozen until a decision is made.", "ماذا أفعل إذا لم أستلم المنتج؟", "What if I do not receive the product?" });
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Etamid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Cycle2Seeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Faqs",
                columns: new[] { "Id", "AnswerAr", "AnswerEn", "IsActive", "QuestionAr", "QuestionEn", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1L, "منصة وساطة مالية تحفظ أموال الصفقة بين المشتري والبائع حتى يتم التسليم والموافقة.", "A financial escrow platform that holds the deal amount between buyer and seller until delivery is confirmed.", true, "ما هي منصة اعتمد؟", "What is Etamid?", 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2L, "رسوم ثابتة قدرها 50 ريال على البائع لكل صفقة.", "A fixed fee of 50 SAR per deal, paid by the seller.", true, "كم رسوم المنصة؟", "What are the platform fees?", 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3L, "الدفع عن طريق التحويل البنكي إلى حساب المنصة وسيتم تأكيده بعد التحقق.", "Payment is by bank transfer to the platform account and is confirmed after verification.", true, "كيف أدفع قيمة الصفقة؟", "How do I pay for a deal?", 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4L, "يمكنك فتح نزاع من صفحة الصفقة وسيتم تجميد المبلغ حتى صدور القرار.", "You can open a dispute from the deal page and the amount stays frozen until a decision is made.", true, "ماذا أفعل إذا لم أستلم المنتج؟", "What if I do not receive the product?", 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Lookups",
                columns: new[] { "Id", "Code", "IconFileId", "IsActive", "NameAr", "NameEn", "SortOrder", "Type" },
                values: new object[,]
                {
                    { 21L, "electronics", null, true, "أجهزة إلكترونية", "Electronics", 1, "store_category" },
                    { 22L, "fashion", null, true, "أزياء وإكسسوارات", "Fashion and accessories", 2, "store_category" },
                    { 23L, "home", null, true, "المنزل والأثاث", "Home and furniture", 3, "store_category" },
                    { 24L, "vehicles", null, true, "مركبات وقطع غيار", "Vehicles and parts", 4, "store_category" },
                    { 25L, "other", null, true, "أخرى", "Other", 5, "store_category" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "Faqs",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "Lookups",
                keyColumn: "Id",
                keyValue: 25L);
        }
    }
}

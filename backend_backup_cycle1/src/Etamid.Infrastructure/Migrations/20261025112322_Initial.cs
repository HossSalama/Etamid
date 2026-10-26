using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Etamid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "DealNumbers",
                startValue: 45872L);

            migrationBuilder.CreateTable(
                name: "AdminPermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(190)", maxLength: 190, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByAdminId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    PhoneNormalized = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Event = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DeviceId = table.Column<long>(type: "bigint", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    QuestionEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AnswerAr = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    AnswerEn = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResponseStatus = table.Column<int>(type: "int", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => new { x.UserId, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    SignatureValid = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TermsVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemsAr = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    ItemsEn = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermsVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminRolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_AdminRolePermissions_AdminPermissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "AdminPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminRolePermissions_AdminRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminUserRoles",
                columns: table => new
                {
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserRoles", x => new { x.AdminUserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_AdminRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastCampaigns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TitleAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AudienceFilter = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecipientsCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByAdminId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BroadcastCampaigns_AdminUsers_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    District = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BuildingNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AdditionalNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FullText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    HolderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IbanEncrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IbanLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    IbanHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BankId = table.Column<long>(type: "bigint", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LogoFileId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyDeals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR DealNumbers"),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CounterpartyUserId = table.Column<long>(type: "bigint", nullable: true),
                    CounterpartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CounterpartyPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractFileId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovalSendDaysBefore = table.Column<int>(type: "int", nullable: true),
                    ApprovalResponseDays = table.Column<int>(type: "int", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PenaltyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PenaltyGraceDays = table.Column<int>(type: "int", nullable: false),
                    PenaltyClauses = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WizardStep = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RejectReasonId = table.Column<long>(type: "bigint", nullable: true),
                    RejectNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyDealStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyDealId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDealStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyDealStatusHistory_CompanyDeals_CompanyDealId",
                        column: x => x.CompanyDealId,
                        principalTable: "CompanyDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyProfiles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CommercialRegNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TaxNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "DealEditRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    ProposedById = table.Column<long>(type: "bigint", nullable: false),
                    PrevAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrevDurationValue = table.Column<int>(type: "int", nullable: false),
                    PrevDurationUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PrevDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NewAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewDurationValue = table.Column<int>(type: "int", nullable: true),
                    NewDurationUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NewDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedById = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealEditRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR DealNumbers"),
                    BuyerId = table.Column<long>(type: "bigint", nullable: false),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DurationValue = table.Column<int>(type: "int", nullable: false),
                    DurationUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NeedShipping = table.Column<bool>(type: "bit", nullable: false),
                    ShippingCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    ShippingAddressId = table.Column<long>(type: "bigint", nullable: true),
                    ShippingAddressSnapshot = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PaymentMethodCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ImageFileId = table.Column<long>(type: "bigint", nullable: true),
                    RejectReasonId = table.Column<long>(type: "bigint", nullable: true),
                    RejectNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RespondDeadlineAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FundedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SellerNetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDisputed = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.Id);
                    table.CheckConstraint("CK_Deals_DifferentParties", "[BuyerId] <> [SellerId]");
                    table.ForeignKey(
                        name: "FK_Deals_Addresses_ShippingAddressId",
                        column: x => x.ShippingAddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DealStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealStatusHistory_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisputeAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisputeId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: true),
                    FileId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisputeMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisputeId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    SenderAdminId = table.Column<long>(type: "bigint", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    IsInternalNote = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeMessages_AdminUsers_SenderAdminId",
                        column: x => x.SenderAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Disputes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    OpenedById = table.Column<long>(type: "bigint", nullable: false),
                    AgainstUserId = table.Column<long>(type: "bigint", nullable: false),
                    ReasonId = table.Column<long>(type: "bigint", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedByAdminId = table.Column<long>(type: "bigint", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedAdminId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disputes_AdminUsers_AssignedAdminId",
                        column: x => x.AssignedAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_AdminUsers_ResolvedByAdminId",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disputes_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscrowHolds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    BuyerId = table.Column<long>(type: "bigint", nullable: false),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    HeldAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscrowHolds_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lookups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IconFileId = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lookups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lookups_Files_IconFileId",
                        column: x => x.IconFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShippingCompanies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LogoFileId = table.Column<long>(type: "bigint", nullable: true),
                    IntegrationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShippingCompanies_Files_LogoFileId",
                        column: x => x.LogoFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PhoneNormalized = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(190)", maxLength: 190, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PhoneVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AvatarFileId = table.Column<long>(type: "bigint", nullable: true),
                    NafathVerified = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPhoneAuthAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Files_AvatarFileId",
                        column: x => x.AvatarFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    ShippingCompanyId = table.Column<long>(type: "bigint", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    WaybillFileId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shipments_Files_WaybillFileId",
                        column: x => x.WaybillFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shipments_ShippingCompanies_ShippingCompanyId",
                        column: x => x.ShippingCompanyId,
                        principalTable: "ShippingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OwnerUserId = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerAccounts_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NafathVerifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RandomNumber = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NationalIdHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    VerifiedName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NafathVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NafathVerifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyDealId = table.Column<long>(type: "bigint", nullable: true),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: true),
                    DisputeId = table.Column<long>(type: "bigint", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PushSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealId = table.Column<long>(type: "bigint", nullable: false),
                    RaterId = table.Column<long>(type: "bigint", nullable: false),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    Stars = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                    table.CheckConstraint("CK_Ratings_Stars", "[Stars] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Ratings_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ratings_Users_RaterId",
                        column: x => x.RaterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ratings_Users_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReplacedByTokenId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreProfiles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    BannerFileId = table.Column<long>(type: "bigint", nullable: true),
                    RatingAvg = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    RatingCount = table.Column<int>(type: "int", nullable: false),
                    SuccessfulDeals = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StoreProfiles_Files_BannerFileId",
                        column: x => x.BannerFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreProfiles_Lookups_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Lookups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: true),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AssignedAdminId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "TermsAcceptances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TermsVersionId = table.Column<long>(type: "bigint", nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyDealId = table.Column<long>(type: "bigint", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    DeviceId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermsAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermsAcceptances_CompanyDeals_CompanyDealId",
                        column: x => x.CompanyDealId,
                        principalTable: "CompanyDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TermsAcceptances_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TermsAcceptances_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TermsAcceptances_TermsVersions_TermsVersionId",
                        column: x => x.TermsVersionId,
                        principalTable: "TermsVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TermsAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentId = table.Column<long>(type: "bigint", nullable: false),
                    EventAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LedgerAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Activated = table.Column<bool>(type: "bit", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailableBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.CheckConstraint("CK_Wallets_Balance", "[AvailableBalance] >= 0");
                    table.ForeignKey(
                        name: "FK_Wallets_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Wallets_Users_UserId",
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
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: true),
                    SenderAdminId = table.Column<long>(type: "bigint", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: false),
                    FileId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_AdminUsers_SenderAdminId",
                        column: x => x.SenderAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    BankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    IbanSnapshotMasked = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ApprovedByAdminId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_AdminUsers_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstallmentApprovals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: false),
                    DecidedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentApprovals_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyDealId = table.Column<long>(type: "bigint", nullable: true),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: true),
                    WithdrawalId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedByType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_CompanyDeals_CompanyDealId",
                        column: x => x.CompanyDealId,
                        principalTable: "CompanyDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_WithdrawalRequests_WithdrawalId",
                        column: x => x.WithdrawalId,
                        principalTable: "WithdrawalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentInstallments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyDealId = table.Column<long>(type: "bigint", nullable: false),
                    Order = table.Column<byte>(type: "tinyint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovalRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalDeadlineAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentInstallments", x => x.Id);
                    table.CheckConstraint("CK_PaymentInstallments_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentInstallments_CompanyDeals_CompanyDealId",
                        column: x => x.CompanyDealId,
                        principalTable: "CompanyDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DealId = table.Column<long>(type: "bigint", nullable: true),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 255, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransferReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ProofFileId = table.Column<long>(type: "bigint", nullable: true),
                    ConfirmedByAdminId = table.Column<long>(type: "bigint", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_AdminUsers_ConfirmedByAdminId",
                        column: x => x.ConfirmedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Files_ProofFileId",
                        column: x => x.ProofFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentInstallments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "PaymentInstallments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PenaltyCharges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyDealId = table.Column<long>(type: "bigint", nullable: false),
                    PenaltyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DaysLate = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LedgerTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WaivedByAdminId = table.Column<long>(type: "bigint", nullable: true),
                    WaiverReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenaltyCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PenaltyCharges_AdminUsers_WaivedByAdminId",
                        column: x => x.WaivedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PenaltyCharges_CompanyDeals_CompanyDealId",
                        column: x => x.CompanyDealId,
                        principalTable: "CompanyDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PenaltyCharges_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PenaltyCharges_PaymentInstallments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "PaymentInstallments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Key", "Description", "UpdatedAt", "UpdatedByAdminId", "Value", "ValueType" },
                values: new object[,]
                {
                    { "currency", "Default currency", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "SAR", "String" },
                    { "force_update", "Block old app versions", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "false", "Bool" },
                    { "min_version_android", "Minimum supported Android app version", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "1.0.0", "String" },
                    { "min_version_ios", "Minimum supported iOS app version", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "1.0.0", "String" },
                    { "platform_fee_amount", "Fixed platform fee per deal, in SAR", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "50", "Decimal" },
                    { "platform_fee_paid_by", "Who pays the platform fee", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "seller", "String" },
                    { "support_whatsapp", "Support WhatsApp number (placeholder)", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "966500000000", "String" }
                });

            migrationBuilder.InsertData(
                table: "Banks",
                columns: new[] { "Id", "Code", "IsActive", "LogoFileId", "NameAr", "NameEn" },
                values: new object[,]
                {
                    { 1L, "80", true, null, "مصرف الراجحي", "Al Rajhi Bank" },
                    { 2L, "10", true, null, "البنك الأهلي السعودي", "Saudi National Bank" },
                    { 3L, "20", true, null, "بنك الرياض", "Riyad Bank" },
                    { 4L, "45", true, null, "البنك السعودي الأول", "Saudi Awwal Bank" },
                    { 5L, "55", true, null, "البنك السعودي الفرنسي", "Banque Saudi Fransi" },
                    { 6L, "60", true, null, "بنك الجزيرة", "Bank AlJazira" },
                    { 7L, "05", true, null, "بنك الإنماء", "Alinma Bank" },
                    { 8L, "15", true, null, "بنك البلاد", "Bank Albilad" },
                    { 9L, "30", true, null, "البنك العربي الوطني", "Arab National Bank" },
                    { 10L, "65", true, null, "البنك السعودي للاستثمار", "Saudi Investment Bank" }
                });

            migrationBuilder.InsertData(
                table: "Lookups",
                columns: new[] { "Id", "Code", "IconFileId", "IsActive", "NameAr", "NameEn", "SortOrder", "Type" },
                values: new object[,]
                {
                    { 1L, "product_mismatch", null, true, "المنتج غير مطابق للوصف", "Product does not match the description", 1, "reject_reason" },
                    { 2L, "price_change", null, true, "تغيير في السعر", "Price change", 2, "reject_reason" },
                    { 3L, "unavailable", null, true, "المنتج غير متوفر حالياً", "Product is not available", 3, "reject_reason" },
                    { 4L, "buyer_communication", null, true, "مشكلة في التواصل مع المشتري", "Communication problem with the buyer", 4, "reject_reason" },
                    { 5L, "spec_mismatch", null, true, "المنتج لا يطابق المواصفات", "Product does not match the specs", 5, "reject_reason" },
                    { 6L, "other", null, true, "سبب آخر", "Other reason", 6, "reject_reason" },
                    { 7L, "not_received", null, true, "لم أستلم المنتج", "I did not receive the product", 1, "dispute_reason" },
                    { 8L, "not_as_described", null, true, "المنتج مختلف عن الوصف المتفق عليه", "Product differs from the agreed description", 2, "dispute_reason" },
                    { 9L, "damaged", null, true, "المنتج تالف أو معيب", "Product is damaged or faulty", 3, "dispute_reason" },
                    { 10L, "wrong_item", null, true, "استلمت منتجاً خاطئاً", "I received the wrong product", 4, "dispute_reason" },
                    { 11L, "seller_breach", null, true, "البائع لم يلتزم بالاتفاق", "The seller did not comply with the agreement", 5, "dispute_reason" },
                    { 12L, "other", null, true, "سبب آخر", "Other reason", 6, "dispute_reason" },
                    { 13L, "terms_not_acceptable", null, true, "الشروط غير مقبولة", "Terms not acceptable", 1, "company_reject_reason" },
                    { 14L, "schedule_not_acceptable", null, true, "جدول الدفعات غير مقبول", "Payment schedule not acceptable", 2, "company_reject_reason" },
                    { 15L, "penalty_not_acceptable", null, true, "الشرط الجزائي غير مقبول", "Penalty clause not acceptable", 3, "company_reject_reason" },
                    { 16L, "wrong_details", null, true, "بيانات الشركة غير صحيحة", "Incorrect company details", 4, "company_reject_reason" },
                    { 17L, "other", null, true, "سبب آخر", "Other reason", 5, "company_reject_reason" },
                    { 18L, "hour", null, true, "ساعة", "Hour", 1, "duration_unit" },
                    { 19L, "day", null, true, "يوم", "Day", 2, "duration_unit" },
                    { 20L, "bank_transfer", null, true, "تحويل بنكي", "Bank transfer", 1, "payment_method" }
                });

            migrationBuilder.InsertData(
                table: "ShippingCompanies",
                columns: new[] { "Id", "Code", "IntegrationType", "IsActive", "LogoFileId", "NameAr", "NameEn", "SortOrder" },
                values: new object[] { 1L, "aramex", "Aramex", true, null, "أرامكس", "Aramex", 1 });

            migrationBuilder.InsertData(
                table: "TermsVersions",
                columns: new[] { "Id", "IsActive", "ItemsAr", "ItemsEn", "PublishedAt", "TitleAr", "TitleEn", "Type", "Version" },
                values: new object[,]
                {
                    { 1L, true, "[\"يجب تقديم بيانات صحيحة عند التسجيل.\",\"تُحجز الأموال إلى حين انتهاء المعاملة.\",\"يحق للمنصة إيقاف الحسابات المخالفة.\"]", "[\"You must provide accurate information when registering.\",\"Funds are held until the deal ends.\",\"Etamid may suspend accounts that violate these terms.\"]", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), "شروط وأحكام المنصة", "Platform Terms and Conditions", "Platform", "1.0" },
                    { 2L, true, "[\"يُحوَّل المبلغ للبائع بعد تأكيد المشتري للاستلام.\",\"على البائع تسليم المنتج كما هو متفق عليه.\",\"عند وجود مشكلة تُطبَّق سياسة النزاعات.\"]", "[\"The amount is transferred to the seller after the buyer confirms receipt.\",\"The seller must deliver the product as agreed.\",\"If there is a problem, the dispute policy applies.\"]", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), "شروط وأحكام المعاملة", "Deal Terms and Conditions", "Deal", "1.0" },
                    { 3L, true, "[\"تُسدَّد الدفعات وفق الجدول المتفق عليه.\",\"تحتاج كل دفعة إلى اعتماد الطرف الآخر.\",\"يُطبَّق الشرط الجزائي تلقائياً بعد انتهاء مهلة السماح.\"]", "[\"Payments follow the agreed schedule.\",\"Each payment needs the other party's approval.\",\"The penalty clause applies automatically after the grace period.\"]", new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), "شروط وأحكام معاملات الشركات", "Company Deal Terms and Conditions", "Company", "1.0" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminPermissions_Code",
                table: "AdminPermissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminRolePermissions_PermissionId",
                table: "AdminRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRoles_Name",
                table: "AdminRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserRoles_RoleId",
                table: "AdminUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Email",
                table: "AdminUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorType_ActorId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "ActorType", "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthAuditLogs_Ip_CreatedAt",
                table: "AuthAuditLogs",
                columns: new[] { "Ip", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthAuditLogs_PhoneNormalized_CreatedAt",
                table: "AuthAuditLogs",
                columns: new[] { "PhoneNormalized", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthAuditLogs_UserId_CreatedAt",
                table: "AuthAuditLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankId",
                table: "BankAccounts",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_IbanHash",
                table: "BankAccounts",
                column: "IbanHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_UserId",
                table: "BankAccounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Code",
                table: "Banks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Banks_LogoFileId",
                table: "Banks",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastCampaigns_CreatedByAdminId",
                table: "BroadcastCampaigns",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_ContractFileId",
                table: "CompanyDeals",
                column: "ContractFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_CounterpartyUserId_Status",
                table: "CompanyDeals",
                columns: new[] { "CounterpartyUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_CreatorUserId_Status",
                table: "CompanyDeals",
                columns: new[] { "CreatorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_Number",
                table: "CompanyDeals",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_RejectReasonId",
                table: "CompanyDeals",
                column: "RejectReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDealStatusHistory_ActorUserId",
                table: "CompanyDealStatusHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDealStatusHistory_CompanyDealId_CreatedAt",
                table: "CompanyDealStatusHistory",
                columns: new[] { "CompanyDealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_CommercialRegNo",
                table: "CompanyProfiles",
                column: "CommercialRegNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealEditRequests_DealId",
                table: "DealEditRequests",
                column: "DealId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_DealEditRequests_ProposedById",
                table: "DealEditRequests",
                column: "ProposedById");

            migrationBuilder.CreateIndex(
                name: "IX_DealEditRequests_RespondedById",
                table: "DealEditRequests",
                column: "RespondedById");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_BuyerId_Status",
                table: "Deals",
                columns: new[] { "BuyerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ImageFileId",
                table: "Deals",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_Number",
                table: "Deals",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deals_RejectReasonId",
                table: "Deals",
                column: "RejectReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_SellerId_Status",
                table: "Deals",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ShippingAddressId",
                table: "Deals",
                column: "ShippingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ShippingCompanyId",
                table: "Deals",
                column: "ShippingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_Status_ExpiresAt",
                table: "Deals",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DealStatusHistory_ActorUserId",
                table: "DealStatusHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DealStatusHistory_DealId_CreatedAt",
                table: "DealStatusHistory",
                columns: new[] { "DealId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceToken",
                table: "Devices",
                column: "DeviceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeAttachments_DisputeId",
                table: "DisputeAttachments",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeAttachments_FileId",
                table: "DisputeAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeAttachments_MessageId",
                table: "DisputeAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeAttachments_UploadedByUserId",
                table: "DisputeAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_DisputeId_CreatedAt",
                table: "DisputeMessages",
                columns: new[] { "DisputeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_SenderAdminId",
                table: "DisputeMessages",
                column: "SenderAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_SenderUserId",
                table: "DisputeMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_AgainstUserId",
                table: "Disputes",
                column: "AgainstUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_AssignedAdminId",
                table: "Disputes",
                column: "AssignedAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_DealId",
                table: "Disputes",
                column: "DealId",
                unique: true,
                filter: "[Status] IN ('UnderReview','AwaitingInfo')");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Number",
                table: "Disputes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_OpenedById",
                table: "Disputes",
                column: "OpenedById");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ReasonId",
                table: "Disputes",
                column: "ReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ResolvedByAdminId",
                table: "Disputes",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowHolds_BuyerId",
                table: "EscrowHolds",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowHolds_DealId",
                table: "EscrowHolds",
                column: "DealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscrowHolds_PaymentId",
                table: "EscrowHolds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowHolds_SellerId",
                table: "EscrowHolds",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerUserId",
                table: "Files",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_PublicId",
                table: "Files",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentApprovals_DecidedByUserId",
                table: "InstallmentApprovals",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentApprovals_InstallmentId",
                table: "InstallmentApprovals",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_OwnerUserId",
                table: "LedgerAccounts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_AccountId_CreatedAt",
                table: "LedgerEntries",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TransactionId",
                table: "LedgerEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_CompanyDealId",
                table: "LedgerTransactions",
                column: "CompanyDealId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_DealId",
                table: "LedgerTransactions",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_IdempotencyKey",
                table: "LedgerTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_InstallmentId",
                table: "LedgerTransactions",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_PaymentId",
                table: "LedgerTransactions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_PublicId",
                table: "LedgerTransactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_WithdrawalId",
                table: "LedgerTransactions",
                column: "WithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_IconFileId",
                table: "Lookups",
                column: "IconFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_Type_Code",
                table: "Lookups",
                columns: new[] { "Type", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NafathVerifications_RequestId",
                table: "NafathVerifications",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NafathVerifications_UserId",
                table: "NafathVerifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Code",
                table: "NotificationTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentInstallments_CompanyDealId_Order",
                table: "PaymentInstallments",
                columns: new[] { "CompanyDealId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentInstallments_PaymentId",
                table: "PaymentInstallments",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentInstallments_Status_DueDate",
                table: "PaymentInstallments",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConfirmedByAdminId",
                table: "Payments",
                column: "ConfirmedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DealId",
                table: "Payments",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InstallmentId",
                table: "Payments",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProofFileId",
                table: "Payments",
                column: "ProofFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderPaymentId",
                table: "Payments",
                column: "ProviderPaymentId",
                unique: true,
                filter: "[ProviderPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PublicId",
                table: "Payments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId",
                table: "Payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_ProviderEventId",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyCharges_CompanyDealId",
                table: "PenaltyCharges",
                column: "CompanyDealId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyCharges_InstallmentId",
                table: "PenaltyCharges",
                column: "InstallmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyCharges_LedgerTransactionId",
                table: "PenaltyCharges",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PenaltyCharges_WaivedByAdminId",
                table: "PenaltyCharges",
                column: "WaivedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_DealId",
                table: "Ratings",
                column: "DealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_RaterId",
                table: "Ratings",
                column: "RaterId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_SellerId_CreatedAt",
                table: "Ratings",
                columns: new[] { "SellerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeviceId",
                table: "RefreshTokens",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_ShipmentId_EventAt_Code",
                table: "ShipmentEvents",
                columns: new[] { "ShipmentId", "EventAt", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_DealId",
                table: "Shipments",
                column: "DealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShippingCompanyId",
                table: "Shipments",
                column: "ShippingCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_WaybillFileId",
                table: "Shipments",
                column: "WaybillFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingCompanies_Code",
                table: "ShippingCompanies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShippingCompanies_LogoFileId",
                table: "ShippingCompanies",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreProfiles_BannerFileId",
                table: "StoreProfiles",
                column: "BannerFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreProfiles_CategoryId",
                table: "StoreProfiles",
                column: "CategoryId");

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
                name: "IX_TermsAcceptances_CompanyDealId",
                table: "TermsAcceptances",
                column: "CompanyDealId");

            migrationBuilder.CreateIndex(
                name: "IX_TermsAcceptances_DealId",
                table: "TermsAcceptances",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_TermsAcceptances_DeviceId",
                table: "TermsAcceptances",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TermsAcceptances_TermsVersionId",
                table: "TermsAcceptances",
                column: "TermsVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TermsAcceptances_UserId",
                table: "TermsAcceptances",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TermsVersions_Type_Version",
                table: "TermsVersions",
                columns: new[] { "Type", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarFileId",
                table: "Users",
                column: "AvatarFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Code",
                table: "Users",
                column: "Code",
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNormalized",
                table: "Users",
                column: "PhoneNormalized",
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                table: "Users",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_LedgerAccountId",
                table: "Wallets",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId",
                table: "Wallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_ApprovedByAdminId",
                table: "WithdrawalRequests",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_BankAccountId",
                table: "WithdrawalRequests",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_IdempotencyKey",
                table: "WithdrawalRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_UserId_CreatedAt",
                table: "WithdrawalRequests",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_WalletId",
                table: "WithdrawalRequests",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Users_UserId",
                table: "Addresses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Banks_BankId",
                table: "BankAccounts",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Users_UserId",
                table: "BankAccounts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Banks_Files_LogoFileId",
                table: "Banks",
                column: "LogoFileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyDeals_Files_ContractFileId",
                table: "CompanyDeals",
                column: "ContractFileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyDeals_Lookups_RejectReasonId",
                table: "CompanyDeals",
                column: "RejectReasonId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyDeals_Users_CounterpartyUserId",
                table: "CompanyDeals",
                column: "CounterpartyUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyDeals_Users_CreatorUserId",
                table: "CompanyDeals",
                column: "CreatorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyDealStatusHistory_Users_ActorUserId",
                table: "CompanyDealStatusHistory",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyProfiles_Users_UserId",
                table: "CompanyProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DealEditRequests_Deals_DealId",
                table: "DealEditRequests",
                column: "DealId",
                principalTable: "Deals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DealEditRequests_Users_ProposedById",
                table: "DealEditRequests",
                column: "ProposedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DealEditRequests_Users_RespondedById",
                table: "DealEditRequests",
                column: "RespondedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Files_ImageFileId",
                table: "Deals",
                column: "ImageFileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Lookups_RejectReasonId",
                table: "Deals",
                column: "RejectReasonId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_ShippingCompanies_ShippingCompanyId",
                table: "Deals",
                column: "ShippingCompanyId",
                principalTable: "ShippingCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_BuyerId",
                table: "Deals",
                column: "BuyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_SellerId",
                table: "Deals",
                column: "SellerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DealStatusHistory_Users_ActorUserId",
                table: "DealStatusHistory",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Users_UserId",
                table: "Devices",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeAttachments_DisputeMessages_MessageId",
                table: "DisputeAttachments",
                column: "MessageId",
                principalTable: "DisputeMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeAttachments_Disputes_DisputeId",
                table: "DisputeAttachments",
                column: "DisputeId",
                principalTable: "Disputes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeAttachments_Files_FileId",
                table: "DisputeAttachments",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeAttachments_Users_UploadedByUserId",
                table: "DisputeAttachments",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeMessages_Disputes_DisputeId",
                table: "DisputeMessages",
                column: "DisputeId",
                principalTable: "Disputes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeMessages_Users_SenderUserId",
                table: "DisputeMessages",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Lookups_ReasonId",
                table: "Disputes",
                column: "ReasonId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Users_AgainstUserId",
                table: "Disputes",
                column: "AgainstUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Users_OpenedById",
                table: "Disputes",
                column: "OpenedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowHolds_Payments_PaymentId",
                table: "EscrowHolds",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowHolds_Users_BuyerId",
                table: "EscrowHolds",
                column: "BuyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowHolds_Users_SellerId",
                table: "EscrowHolds",
                column: "SellerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                table: "Files",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InstallmentApprovals_PaymentInstallments_InstallmentId",
                table: "InstallmentApprovals",
                column: "InstallmentId",
                principalTable: "PaymentInstallments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_LedgerTransactions_TransactionId",
                table: "LedgerEntries",
                column: "TransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerTransactions_PaymentInstallments_InstallmentId",
                table: "LedgerTransactions",
                column: "InstallmentId",
                principalTable: "PaymentInstallments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerTransactions_Payments_PaymentId",
                table: "LedgerTransactions",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentInstallments_Payments_PaymentId",
                table: "PaymentInstallments",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_Users_UserId",
                table: "Addresses");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyDeals_Users_CounterpartyUserId",
                table: "CompanyDeals");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyDeals_Users_CreatorUserId",
                table: "CompanyDeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_BuyerId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_SellerId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AdminUsers_ConfirmedByAdminId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyDeals_Files_ContractFileId",
                table: "CompanyDeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Files_ImageFileId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Lookups_Files_IconFileId",
                table: "Lookups");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Files_ProofFileId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShippingCompanies_Files_LogoFileId",
                table: "ShippingCompanies");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyDeals_Lookups_RejectReasonId",
                table: "CompanyDeals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Lookups_RejectReasonId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentInstallments_CompanyDeals_CompanyDealId",
                table: "PaymentInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Deals_DealId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentInstallments_Payments_PaymentId",
                table: "PaymentInstallments");

            migrationBuilder.DropTable(
                name: "AdminRolePermissions");

            migrationBuilder.DropTable(
                name: "AdminUserRoles");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AuthAuditLogs");

            migrationBuilder.DropTable(
                name: "BroadcastCampaigns");

            migrationBuilder.DropTable(
                name: "CompanyDealStatusHistory");

            migrationBuilder.DropTable(
                name: "CompanyProfiles");

            migrationBuilder.DropTable(
                name: "DealEditRequests");

            migrationBuilder.DropTable(
                name: "DealStatusHistory");

            migrationBuilder.DropTable(
                name: "DisputeAttachments");

            migrationBuilder.DropTable(
                name: "EscrowHolds");

            migrationBuilder.DropTable(
                name: "Faqs");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropTable(
                name: "InstallmentApprovals");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "NafathVerifications");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropTable(
                name: "PenaltyCharges");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ShipmentEvents");

            migrationBuilder.DropTable(
                name: "StoreProfiles");

            migrationBuilder.DropTable(
                name: "SupportTicketMessages");

            migrationBuilder.DropTable(
                name: "TermsAcceptances");

            migrationBuilder.DropTable(
                name: "AdminPermissions");

            migrationBuilder.DropTable(
                name: "AdminRoles");

            migrationBuilder.DropTable(
                name: "DisputeMessages");

            migrationBuilder.DropTable(
                name: "LedgerTransactions");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "TermsVersions");

            migrationBuilder.DropTable(
                name: "Disputes");

            migrationBuilder.DropTable(
                name: "WithdrawalRequests");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropTable(
                name: "LedgerAccounts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Lookups");

            migrationBuilder.DropTable(
                name: "CompanyDeals");

            migrationBuilder.DropTable(
                name: "Deals");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "ShippingCompanies");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PaymentInstallments");

            migrationBuilder.DropSequence(
                name: "DealNumbers");
        }
    }
}

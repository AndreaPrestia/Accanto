using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accanto.Admin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdminCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_audit_logs_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_operations_admin_users_RequestedByAdminUserId",
                        column: x => x.RequestedByAdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_sessions_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_user_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminRoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_user_roles_admin_roles_AdminRoleId",
                        column: x => x.AdminRoleId,
                        principalTable: "admin_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_user_roles_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_Action",
                table: "admin_audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_AdminUserId",
                table: "admin_audit_logs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_CreatedAt",
                table: "admin_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_TargetId",
                table: "admin_audit_logs",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_TargetType",
                table: "admin_audit_logs",
                column: "TargetType");

            migrationBuilder.CreateIndex(
                name: "IX_admin_operations_CreatedAt",
                table: "admin_operations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_operations_OperationType",
                table: "admin_operations",
                column: "OperationType");

            migrationBuilder.CreateIndex(
                name: "IX_admin_operations_RequestedByAdminUserId",
                table: "admin_operations",
                column: "RequestedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_operations_Status",
                table: "admin_operations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_admin_operations_TargetUserId",
                table: "admin_operations",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_roles_Name",
                table: "admin_roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_sessions_AdminUserId",
                table: "admin_sessions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_sessions_ExpiresAt",
                table: "admin_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_sessions_RefreshTokenHash",
                table: "admin_sessions",
                column: "RefreshTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_admin_user_roles_AdminRoleId",
                table: "admin_user_roles",
                column: "AdminRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_user_roles_AdminUserId_AdminRoleId",
                table: "admin_user_roles",
                columns: new[] { "AdminUserId", "AdminRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_Email",
                table: "admin_users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audit_logs");

            migrationBuilder.DropTable(
                name: "admin_operations");

            migrationBuilder.DropTable(
                name: "admin_sessions");

            migrationBuilder.DropTable(
                name: "admin_user_roles");

            migrationBuilder.DropTable(
                name: "admin_roles");

            migrationBuilder.DropTable(
                name: "admin_users");
        }
    }
}

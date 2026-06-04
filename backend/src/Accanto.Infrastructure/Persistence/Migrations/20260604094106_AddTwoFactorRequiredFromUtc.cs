using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accanto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorRequiredFromUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TwoFactorRequiredFromUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Rollout morbido: ogni Owner esistente ha 7gg dal deploy per
            // configurare 2FA. Chi non e' (ancora) Owner resta NULL e verra'
            // settato lazy alla prima promozione.
            migrationBuilder.Sql(@"
                UPDATE users u
                SET ""TwoFactorRequiredFromUtc"" = NOW() AT TIME ZONE 'UTC' + INTERVAL '7 days'
                WHERE u.""TwoFactorRequiredFromUtc"" IS NULL
                  AND EXISTS (
                    SELECT 1 FROM care_circle_members m
                    WHERE m.""UserId"" = u.""Id"" AND m.""Role"" = 'Owner'
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoFactorRequiredFromUtc",
                table: "users");
        }
    }
}

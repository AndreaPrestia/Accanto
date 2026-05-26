using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accanto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiEnabledToCareCircle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiEnabled",
                table: "care_circles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiEnabled",
                table: "care_circles");
        }
    }
}

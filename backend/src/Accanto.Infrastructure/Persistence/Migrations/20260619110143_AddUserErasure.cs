using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accanto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ErasedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErasureReason",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsErased",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErasedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ErasureReason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsErased",
                table: "users");
        }
    }
}

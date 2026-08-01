using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budgie.Api.Migrations.Finances
{
    /// <inheritdoc />
    public partial class ReplaceHasRepaidWithRemainingAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasRepaid",
                table: "TransactionParticipants");

            migrationBuilder.AddColumn<bool>(
                name: "IsTrackingOnly",
                table: "TransactionParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "TransactionParticipants",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTrackingOnly",
                table: "TransactionParticipants");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "TransactionParticipants");

            migrationBuilder.AddColumn<bool>(
                name: "HasRepaid",
                table: "TransactionParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

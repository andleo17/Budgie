using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budgie.Api.Migrations.Finances
{
    /// <inheritdoc />
    public partial class AddAccountBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AnnualInterestRate",
                table: "Accounts",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingCycleDay",
                table: "Accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BillingCycleDayInclusive",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentDueDay",
                table: "Accounts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualInterestRate",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BillingCycleDay",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BillingCycleDayInclusive",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PaymentDueDay",
                table: "Accounts");
        }
    }
}

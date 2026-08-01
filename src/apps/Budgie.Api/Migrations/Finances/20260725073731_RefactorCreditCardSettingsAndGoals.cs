using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budgie.Api.Migrations.Finances
{
    /// <inheritdoc />
    public partial class RefactorCreditCardSettingsAndGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetAmount",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TargetDate",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "PaymentDueDay",
                table: "Accounts",
                newName: "CreditCardSettings_PaymentDueDay");

            migrationBuilder.RenameColumn(
                name: "CreditLimit",
                table: "Accounts",
                newName: "CreditCardSettings_CreditLimit");

            migrationBuilder.RenameColumn(
                name: "CreditConsumptionMode",
                table: "Accounts",
                newName: "CreditCardSettings_CreditConsumptionMode");

            migrationBuilder.RenameColumn(
                name: "BillingCycleDayInclusive",
                table: "Accounts",
                newName: "CreditCardSettings_BillingCycleDayInclusive");

            migrationBuilder.RenameColumn(
                name: "BillingCycleDay",
                table: "Accounts",
                newName: "CreditCardSettings_BillingCycleDay");

            migrationBuilder.RenameColumn(
                name: "AnnualInterestRate",
                table: "Accounts",
                newName: "CreditCardSettings_AnnualInterestRate");

            migrationBuilder.AlterColumn<bool>(
                name: "CreditCardSettings_BillingCycleDayInclusive",
                table: "Accounts",
                type: "boolean",
                nullable: true,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TargetDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_PaymentDueDay",
                table: "Accounts",
                newName: "PaymentDueDay");

            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_CreditLimit",
                table: "Accounts",
                newName: "CreditLimit");

            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_CreditConsumptionMode",
                table: "Accounts",
                newName: "CreditConsumptionMode");

            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_BillingCycleDayInclusive",
                table: "Accounts",
                newName: "BillingCycleDayInclusive");

            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_BillingCycleDay",
                table: "Accounts",
                newName: "BillingCycleDay");

            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_AnnualInterestRate",
                table: "Accounts",
                newName: "AnnualInterestRate");

            migrationBuilder.AlterColumn<bool>(
                name: "BillingCycleDayInclusive",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetAmount",
                table: "Accounts",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetDate",
                table: "Accounts",
                type: "timestamp without time zone",
                nullable: true);
        }
    }
}

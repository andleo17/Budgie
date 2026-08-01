using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budgie.Api.Migrations.Finances
{
    /// <inheritdoc />
    public partial class FixRecurringEndDateNullGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreditCardSettings_CreditLimit",
                table: "Accounts",
                newName: "CreditLimit");

            migrationBuilder.AddColumn<string>(
                name: "ContributedCurrency",
                table: "TransactionParticipants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RemainingCurrency",
                table: "TransactionParticipants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContributedCurrency",
                table: "RecurringTransactionParticipants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RemainingCurrency",
                table: "RecurringTransactionParticipants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetCurrency",
                table: "Goals",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Accounts",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "CreditLimitCurrency",
                table: "Accounts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributedCurrency",
                table: "TransactionParticipants");

            migrationBuilder.DropColumn(
                name: "RemainingCurrency",
                table: "TransactionParticipants");

            migrationBuilder.DropColumn(
                name: "ContributedCurrency",
                table: "RecurringTransactionParticipants");

            migrationBuilder.DropColumn(
                name: "RemainingCurrency",
                table: "RecurringTransactionParticipants");

            migrationBuilder.DropColumn(
                name: "TargetCurrency",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "CreditLimitCurrency",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "CreditLimit",
                table: "Accounts",
                newName: "CreditCardSettings_CreditLimit");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "Accounts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }
    }
}

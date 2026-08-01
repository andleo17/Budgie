using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budgie.Api.Migrations.Finances
{
    /// <inheritdoc />
    public partial class AddInstallmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstallmentGroupId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentNumber",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "Transactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalInstallments",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CreditCardSettings_InterestCalculationMethod",
                table: "Accounts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallmentGroupId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "InstallmentNumber",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TotalInstallments",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreditCardSettings_InterestCalculationMethod",
                table: "Accounts");
        }
    }
}

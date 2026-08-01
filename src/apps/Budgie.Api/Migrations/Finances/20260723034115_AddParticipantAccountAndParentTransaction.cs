using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Budgie.Api.Migrations.Finances
{
    /// <inheritdoc />
    public partial class AddParticipantAccountAndParentTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentTransactionId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DestinationAccountId",
                table: "TransactionParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ParentTransactionId",
                table: "Transactions",
                column: "ParentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionParticipants_DestinationAccountId",
                table: "TransactionParticipants",
                column: "DestinationAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionParticipants_Accounts_DestinationAccountId",
                table: "TransactionParticipants",
                column: "DestinationAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_ParentTransactionId",
                table: "Transactions",
                column: "ParentTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionParticipants_Accounts_DestinationAccountId",
                table: "TransactionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_ParentTransactionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ParentTransactionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TransactionParticipants_DestinationAccountId",
                table: "TransactionParticipants");

            migrationBuilder.DropColumn(
                name: "ParentTransactionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DestinationAccountId",
                table: "TransactionParticipants");
        }
    }
}

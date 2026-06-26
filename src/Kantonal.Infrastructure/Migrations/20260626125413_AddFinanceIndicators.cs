using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kantonal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "balance_sheet_surplus_quotient",
                table: "finance_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "capital_service_share",
                table: "finance_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_debt_share",
                table: "finance_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "interest_burden_share",
                table: "finance_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "investment_share",
                table: "finance_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_debt_quotient",
                table: "finance_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "self_financing_share",
                table: "finance_records",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "balance_sheet_surplus_quotient",
                table: "finance_records");

            migrationBuilder.DropColumn(
                name: "capital_service_share",
                table: "finance_records");

            migrationBuilder.DropColumn(
                name: "gross_debt_share",
                table: "finance_records");

            migrationBuilder.DropColumn(
                name: "interest_burden_share",
                table: "finance_records");

            migrationBuilder.DropColumn(
                name: "investment_share",
                table: "finance_records");

            migrationBuilder.DropColumn(
                name: "net_debt_quotient",
                table: "finance_records");

            migrationBuilder.DropColumn(
                name: "self_financing_share",
                table: "finance_records");
        }
    }
}

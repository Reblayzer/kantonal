using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kantonal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_records",
                columns: table => new
                {
                    bfs_number = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    municipality_name = table.Column<string>(type: "text", nullable: false),
                    self_financing_ratio = table.Column<decimal>(type: "numeric", nullable: true),
                    net_debt_per_capita_chf = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_records", x => new { x.bfs_number, x.year });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_records");
        }
    }
}

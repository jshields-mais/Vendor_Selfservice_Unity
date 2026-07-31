using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTaxClassificationExemptPayeeW9OnFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExemptPayee",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "TaxClassification",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "W9OnFile",
                table: "Vendors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExemptPayee",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxClassification",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "W9OnFile",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}

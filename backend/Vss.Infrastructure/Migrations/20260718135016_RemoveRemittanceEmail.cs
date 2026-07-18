using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRemittanceEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemittanceEmail",
                table: "Vendors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemittanceEmail",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}

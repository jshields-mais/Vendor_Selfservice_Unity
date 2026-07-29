using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vss.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SalesEmail",
                table: "Vendors",
                newName: "ContactTitle");

            migrationBuilder.RenameColumn(
                name: "SalesContactName",
                table: "Vendors",
                newName: "ContactPhone");

            migrationBuilder.RenameColumn(
                name: "PrimaryTitle",
                table: "Vendors",
                newName: "ContactMobile");

            migrationBuilder.RenameColumn(
                name: "PrimaryContact",
                table: "Vendors",
                newName: "ContactLastName");

            migrationBuilder.RenameColumn(
                name: "ApEmail",
                table: "Vendors",
                newName: "ContactFunction");

            migrationBuilder.RenameColumn(
                name: "ApContactName",
                table: "Vendors",
                newName: "ContactFirstName");

            migrationBuilder.AddColumn<string>(
                name: "ContactDepartment",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFax",
                table: "Vendors",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactDepartment",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "ContactFax",
                table: "Vendors");

            migrationBuilder.RenameColumn(
                name: "ContactTitle",
                table: "Vendors",
                newName: "SalesEmail");

            migrationBuilder.RenameColumn(
                name: "ContactPhone",
                table: "Vendors",
                newName: "SalesContactName");

            migrationBuilder.RenameColumn(
                name: "ContactMobile",
                table: "Vendors",
                newName: "PrimaryTitle");

            migrationBuilder.RenameColumn(
                name: "ContactLastName",
                table: "Vendors",
                newName: "PrimaryContact");

            migrationBuilder.RenameColumn(
                name: "ContactFunction",
                table: "Vendors",
                newName: "ApEmail");

            migrationBuilder.RenameColumn(
                name: "ContactFirstName",
                table: "Vendors",
                newName: "ApContactName");
        }
    }
}

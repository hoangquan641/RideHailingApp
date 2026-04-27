using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideHailingApp.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseAndTypeVehicleToUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LicensePlate",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicensePlate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "Users");
        }
    }
}

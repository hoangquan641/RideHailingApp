using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideHailingApp.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestedVehicleType",
                table: "Rides",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedVehicleType",
                table: "Rides");
        }
    }
}

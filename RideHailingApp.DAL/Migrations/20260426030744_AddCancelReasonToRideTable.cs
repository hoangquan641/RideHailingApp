using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideHailingApp.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelReasonToRideTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Rides",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Rides");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideHailingApp.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDeclinedDriversColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeclinedDriverIds",
                table: "Rides",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclinedDriverIds",
                table: "Rides");
        }
    }
}

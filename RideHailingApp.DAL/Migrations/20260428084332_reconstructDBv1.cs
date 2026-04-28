using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideHailingApp.DAL.Migrations
{
    /// <inheritdoc />
    public partial class reconstructDBv1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreditBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentLat",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentLng",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDriverAvailable",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LicensePlate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "DriverProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IsDriverAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CurrentLat = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    CurrentLng = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    LicensePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_DriverProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWallets",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CashBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWallets", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserWallets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverProfiles");

            migrationBuilder.DropTable(
                name: "UserWallets");

            migrationBuilder.AddColumn<decimal>(
                name: "CashBalance",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditBalance",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLat",
                table: "Users",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLng",
                table: "Users",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDriverAvailable",
                table: "Users",
                type: "bit",
                nullable: true);

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
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _365MigrationTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalCountsToMetricsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalDevices",
                table: "MetricSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalGroups",
                table: "MetricSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalUsers",
                table: "MetricSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalDevices",
                table: "MetricSnapshots");

            migrationBuilder.DropColumn(
                name: "TotalGroups",
                table: "MetricSnapshots");

            migrationBuilder.DropColumn(
                name: "TotalUsers",
                table: "MetricSnapshots");
        }
    }
}

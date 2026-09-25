using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _365MigrationTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMigratedUsersToMetricSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MigratedUsers",
                table: "MetricSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MigratedUsers",
                table: "MetricSnapshots");
        }
    }
}

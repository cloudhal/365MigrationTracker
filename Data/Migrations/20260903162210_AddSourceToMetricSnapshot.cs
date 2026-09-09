using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _365MigrationTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceToMetricSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "MetricSnapshots",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "MetricSnapshots");
        }
    }
}

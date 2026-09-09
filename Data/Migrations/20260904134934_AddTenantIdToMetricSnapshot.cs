using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _365MigrationTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToMetricSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MetricSnapshots",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshot_TenantId_CapturedAtUtc",
                table: "MetricSnapshots",
                columns: new[] { "TenantId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetricSnapshot_TenantId_CapturedAtUtc",
                table: "MetricSnapshots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MetricSnapshots");
        }
    }
}

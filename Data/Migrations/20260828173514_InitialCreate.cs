using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _365MigrationTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SyncedUsers = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncedGroups = table.Column<int>(type: "INTEGER", nullable: false),
                    HybridDevices = table.Column<int>(type: "INTEGER", nullable: false),
                    PendingHybridDevices = table.Column<int>(type: "INTEGER", nullable: false),
                    EntraJoinedDevices = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectionSucceeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    CollectionDurationMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshot_CapturedAtUtc",
                table: "MetricSnapshots",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshot_CollectionSucceeded",
                table: "MetricSnapshots",
                column: "CollectionSucceeded");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricSnapshots");
        }
    }
}


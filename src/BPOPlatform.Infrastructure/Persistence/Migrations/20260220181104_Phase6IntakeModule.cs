using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPOPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6IntakeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BusinessUnit = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    QueuePriority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ChatHistoryJson = table.Column<string>(type: "TEXT", nullable: false),
                    AiBrief = table.Column<string>(type: "TEXT", nullable: true),
                    AiCheckpointsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiActionablesJson = table.Column<string>(type: "TEXT", nullable: true),
                    PromotedProcessId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntakeRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ArtifactType = table.Column<int>(type: "INTEGER", nullable: false),
                    BlobPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ExtractedText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeArtifacts_IntakeRequests_IntakeRequestId",
                        column: x => x.IntakeRequestId,
                        principalTable: "IntakeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeArtifacts_IntakeRequestId",
                table: "IntakeArtifacts",
                column: "IntakeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_OwnerId",
                table: "IntakeRequests",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeArtifacts");

            migrationBuilder.DropTable(
                name: "IntakeRequests");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPOPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ExtractedText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "Artifacts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "Artifacts");
        }
    }
}

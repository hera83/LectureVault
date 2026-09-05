using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLectures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LectureId",
                table: "FileMetadata",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Lectures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lectures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_LectureId",
                table: "FileMetadata",
                column: "LectureId");

            migrationBuilder.CreateIndex(
                name: "IX_Lectures_OwnerId",
                table: "Lectures",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMetadata_Lectures_LectureId",
                table: "FileMetadata",
                column: "LectureId",
                principalTable: "Lectures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMetadata_Lectures_LectureId",
                table: "FileMetadata");

            migrationBuilder.DropTable(
                name: "Lectures");

            migrationBuilder.DropIndex(
                name: "IX_FileMetadata_LectureId",
                table: "FileMetadata");

            migrationBuilder.DropColumn(
                name: "LectureId",
                table: "FileMetadata");
        }
    }
}

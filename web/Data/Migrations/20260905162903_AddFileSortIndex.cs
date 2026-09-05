using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortIndex",
                table: "FileMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill: give existing lecture files a sort order matching their current
            // upload-time order (tie-broken by Id), so switching to manual sorting doesn't
            // scramble the display/processing order of files uploaded before this migration.
            migrationBuilder.Sql(
                """
                UPDATE FileMetadata
                SET SortIndex = (
                    SELECT COUNT(*)
                    FROM FileMetadata AS f2
                    WHERE f2.LectureId = FileMetadata.LectureId
                      AND (f2.CreatedAtUtc < FileMetadata.CreatedAtUtc
                           OR (f2.CreatedAtUtc = FileMetadata.CreatedAtUtc AND f2.Id < FileMetadata.Id))
                )
                WHERE LectureId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortIndex",
                table: "FileMetadata");
        }
    }
}

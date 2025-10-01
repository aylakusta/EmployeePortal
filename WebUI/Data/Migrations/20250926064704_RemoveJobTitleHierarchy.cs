using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebUI.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveJobTitleHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobTitles_JobTitles_ParentId",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_JobTitles_Name",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_JobTitles_ParentId",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "JobTitles");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "JobTitles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { true, "Yönetici" });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsActive",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "JobTitles");

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "JobTitles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rank",
                table: "JobTitles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ParentId", "Rank" },
                values: new object[] { null, 1 });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ParentId", "Rank" },
                values: new object[] { 1, 2 });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ParentId", "Rank" },
                values: new object[] { 2, 3 });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Name", "ParentId", "Rank" },
                values: new object[] { "Takım Lideri", 3, 4 });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "ParentId", "Rank" },
                values: new object[] { 4, 5 });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ParentId", "Rank" },
                values: new object[] { 5, 6 });

            migrationBuilder.UpdateData(
                table: "JobTitles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "ParentId", "Rank" },
                values: new object[] { 6, 7 });

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_Name",
                table: "JobTitles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_ParentId",
                table: "JobTitles",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobTitles_JobTitles_ParentId",
                table: "JobTitles",
                column: "ParentId",
                principalTable: "JobTitles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

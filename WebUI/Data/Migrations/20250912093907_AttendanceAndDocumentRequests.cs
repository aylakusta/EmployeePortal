using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebUI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceAndDocumentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Attendances");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Attendances",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "Present",
                table: "Attendances",
                newName: "IsResolved");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Attendances",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Attendances",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Attendances",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    AttendanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UploadedFilePath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    UploadedFileName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRequests_Attendances_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "Attendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequests_AttendanceId",
                table: "DocumentRequests",
                column: "AttendanceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentRequests");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Attendances");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "Attendances",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "IsResolved",
                table: "Attendances",
                newName: "Present");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Attendances",
                newName: "Date");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Attendances",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Attendances",
                type: "TEXT",
                nullable: true);
        }
    }
}

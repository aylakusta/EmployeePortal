using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebUI.Data.Migrations
{
    public partial class Employees_DepartmentIdNullable_DropDepartment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔸 Önce DepartmentId'yi nullable yapıyoruz (SQLite bunu rebuild ile yapar)
            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Employees",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // 🔸 Eski yanlış default 0 değerleri varsa NULL'a çek (FK hatası yaşamamak için güvenli)
            migrationBuilder.Sql("UPDATE Employees SET DepartmentId = NULL WHERE DepartmentId = 0;");

            // 🔸 Eski string kolon 'Department' varsa kaldır
            // (Sende hâlâ DB'de durduğu için insert anında NOT NULL hatası alıyordun)
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Employees");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alım: string kolon geri gelsin (NOT NULL olduğu için default verelim)
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Employees",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            // DepartmentId'yi tekrar NOT NULL'a çeker (gerekirse)
            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Employees",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}

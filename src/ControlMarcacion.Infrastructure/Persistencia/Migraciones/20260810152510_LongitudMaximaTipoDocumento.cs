using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class LongitudMaximaTipoDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "longitud_maxima",
                table: "tipo_documento",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: 1L,
                column: "longitud_maxima",
                value: null);

            migrationBuilder.UpdateData(
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: 2L,
                column: "longitud_maxima",
                value: 10);

            migrationBuilder.UpdateData(
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: 3L,
                column: "longitud_maxima",
                value: 10);

            migrationBuilder.UpdateData(
                table: "tipo_documento",
                keyColumn: "id",
                keyValue: 4L,
                column: "longitud_maxima",
                value: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "longitud_maxima",
                table: "tipo_documento");
        }
    }
}

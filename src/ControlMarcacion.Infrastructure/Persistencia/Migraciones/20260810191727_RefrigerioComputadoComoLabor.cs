using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RefrigerioComputadoComoLabor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 1L,
                column: "minutos_refrigerio",
                value: 0);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 2L,
                column: "minutos_refrigerio",
                value: 0);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 3L,
                column: "minutos_refrigerio",
                value: 0);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 4L,
                column: "minutos_refrigerio",
                value: 0);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 5L,
                column: "minutos_refrigerio",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 1L,
                column: "minutos_refrigerio",
                value: 60);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 2L,
                column: "minutos_refrigerio",
                value: 60);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 3L,
                column: "minutos_refrigerio",
                value: 45);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 4L,
                column: "minutos_refrigerio",
                value: 45);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 5L,
                column: "minutos_refrigerio",
                value: 60);
        }
    }
}

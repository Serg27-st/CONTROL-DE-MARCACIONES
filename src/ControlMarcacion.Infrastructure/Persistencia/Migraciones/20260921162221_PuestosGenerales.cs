using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PuestosGenerales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 1L,
                column: "nombre",
                value: "OPERARIO");

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 2L,
                column: "nombre",
                value: "SUPERVISOR");

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 3L,
                column: "nombre",
                value: "AUXILIAR");

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 4L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 10L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 11L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 12L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 13L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 14L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 15L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 16L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 17L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 18L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 20L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 21L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 22L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 23L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 24L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 25L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 26L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 27L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 30L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 31L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 32L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 33L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 34L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 35L,
                column: "activo",
                value: false);

            migrationBuilder.InsertData(
                table: "puesto",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 40L, true, "OPEMAQ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "OPERARIO DE MÁQUINA" },
                    { 41L, true, "OPETRA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "OPERARIO TRASPALET" },
                    { 42L, true, "SUPPRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "SUPERVISOR DE PRODUCCIÓN" },
                    { 43L, true, "SUPSSO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "SUPERVISOR SSOMA" },
                    { 44L, true, "INSSSO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "INSPECTOR SSOMA" },
                    { 45L, true, "COORD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "COORDINADOR" },
                    { 46L, true, "CORRHH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "COORDINADOR RRHH" },
                    { 47L, true, "BATID", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "BATIDOR" },
                    { 48L, true, "BOLET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "BOLETERA" },
                    { 49L, true, "EMPL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "EMPLEADO" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 40L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 41L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 42L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 43L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 44L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 45L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 46L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 47L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 48L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 49L);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 1L,
                column: "nombre",
                value: "Operario");

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 2L,
                column: "nombre",
                value: "Supervisor");

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 3L,
                column: "nombre",
                value: "Auxiliar");

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 4L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 10L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 11L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 12L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 13L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 14L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 15L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 16L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 17L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 18L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 20L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 21L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 22L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 23L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 24L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 25L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 26L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 27L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 30L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 31L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 32L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 33L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 34L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 35L,
                column: "activo",
                value: true);
        }
    }
}

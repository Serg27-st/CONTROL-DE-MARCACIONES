using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PuestosRealesConsultingAdvisers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "empresa",
                keyColumn: "id",
                keyValue: 1L,
                column: "razon_social",
                value: "CONSULTING ADVISERS S.A.C");

            migrationBuilder.InsertData(
                table: "puesto",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 10L, true, "26001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operario de producción" },
                    { 11L, true, "26002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operador transpalet" },
                    { 12L, true, "26006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operario de máquina" },
                    { 13L, true, "26007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operario transpalet" },
                    { 14L, true, "26104", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Coordinador de producción" },
                    { 15L, true, "26148", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Inspector SSOMA" },
                    { 16L, true, "26216", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Supervisor" },
                    { 17L, true, "26223", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Supervisor de producción" },
                    { 18L, true, "26229", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Supervisor SSOMA" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 18L);

            migrationBuilder.UpdateData(
                table: "empresa",
                keyColumn: "id",
                keyValue: 1L,
                column: "razon_social",
                value: "Consulting Advisers");
        }
    }
}

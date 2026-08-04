using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CatalogosProvisionales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "area",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre", "planta_id" },
                values: new object[,]
                {
                    { 1L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 1L },
                    { 2L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 2L },
                    { 3L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 3L },
                    { 4L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 4L }
                });

            migrationBuilder.InsertData(
                table: "puesto",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, "OPER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Operario" },
                    { 2L, true, "SUPV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Supervisor" },
                    { 3L, true, "AUX", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Auxiliar" },
                    { 4L, true, "ANAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Analista" }
                });

            migrationBuilder.InsertData(
                table: "seccion",
                columns: new[] { "id", "activo", "area_id", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, 1L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" },
                    { 2L, true, 2L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" },
                    { 3L, true, 3L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" },
                    { 4L, true, 4L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 4L);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AreasRealesPlantaDetergentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "area",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre", "planta_id" },
                values: new object[,]
                {
                    { 10L, true, "ENV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "ENVASADO", 1L },
                    { 11L, true, "LAV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "LAVAVAJILLAS", 1L },
                    { 12L, true, "PROC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PROCESOS", 1L },
                    { 13L, true, "SUPPRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "SUP. PRODUCCION", 1L },
                    { 14L, true, "VOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "VOLANTES", 1L },
                    { 15L, true, "INSSSO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "INSP. SSOMA", 1L },
                    { 16L, true, "CRRHH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "COORD. RRHH", 1L },
                    { 17L, true, "SUPSSO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "SUP. SSOMA", 1L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 17L);
        }
    }
}

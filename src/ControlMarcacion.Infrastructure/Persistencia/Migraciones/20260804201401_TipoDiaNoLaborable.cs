using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class TipoDiaNoLaborable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "es_dia_no_laborable",
                table: "tareo_diario");

            migrationBuilder.AddColumn<string>(
                name: "tipo_dia_no_laborable",
                table: "tareo_diario",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "periodo",
                columns: new[] { "id", "activo", "cerrado_en", "cerrado_por", "codigo", "creado_en", "creado_por", "estado", "fecha_fin", "fecha_inicio", "modificado_en", "modificado_por", "motivo_reapertura", "nombre", "reabierto_en", "reabierto_por" },
                values: new object[] { 1L, true, null, null, "2026-07", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Abierto", new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 1), null, null, null, "Julio 2026", null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "periodo",
                keyColumn: "id",
                keyValue: 1L);

            migrationBuilder.DropColumn(
                name: "tipo_dia_no_laborable",
                table: "tareo_diario");

            migrationBuilder.AddColumn<bool>(
                name: "es_dia_no_laborable",
                table: "tareo_diario",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}

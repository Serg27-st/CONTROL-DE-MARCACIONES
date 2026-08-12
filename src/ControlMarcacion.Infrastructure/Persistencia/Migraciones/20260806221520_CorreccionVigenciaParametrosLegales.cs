using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CorreccionVigenciaParametrosLegales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "parametro_legal",
                keyColumn: "id",
                keyValue: 2L,
                column: "vigente_desde",
                value: new DateOnly(2025, 1, 1));

            migrationBuilder.UpdateData(
                table: "parametro_legal",
                keyColumn: "id",
                keyValue: 3L,
                column: "vigente_desde",
                value: new DateOnly(2025, 1, 1));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "parametro_legal",
                keyColumn: "id",
                keyValue: 2L,
                column: "vigente_desde",
                value: new DateOnly(2026, 1, 1));

            migrationBuilder.UpdateData(
                table: "parametro_legal",
                keyColumn: "id",
                keyValue: 3L,
                column: "vigente_desde",
                value: new DateOnly(2026, 1, 1));
        }
    }
}

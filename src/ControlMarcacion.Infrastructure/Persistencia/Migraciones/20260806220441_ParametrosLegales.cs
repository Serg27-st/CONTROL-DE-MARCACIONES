using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ParametrosLegales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parametro_legal",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    valor = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    base_legal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametro_legal", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "parametro_legal",
                columns: new[] { "id", "activo", "base_legal", "creado_en", "creado_por", "modificado_en", "modificado_por", "tipo", "valor", "vigente_desde", "vigente_hasta" },
                values: new object[,]
                {
                    { 1L, true, "D.S. 006-2024-TR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "RemuneracionMinimaVital", 1130.00m, new DateOnly(2025, 1, 1), null },
                    { 2L, true, "Ley 25129", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PorcentajeAsignacionFamiliar", 10.00m, new DateOnly(2026, 1, 1), null },
                    { 3L, true, "Política interna GCA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "ValorMovilidadDiaria", 3.00m, new DateOnly(2026, 1, 1), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_parametro_legal_tipo_vigente_desde_vigente_hasta",
                table: "parametro_legal",
                columns: new[] { "tipo", "vigente_desde", "vigente_hasta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parametro_legal");
        }
    }
}

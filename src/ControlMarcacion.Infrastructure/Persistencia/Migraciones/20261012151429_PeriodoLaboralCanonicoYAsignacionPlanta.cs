using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PeriodoLaboralCanonicoYAsignacionPlanta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asignacion_planta",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    periodo_laboral_id = table.Column<long>(type: "INTEGER", nullable: false),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asignacion_planta", x => x.id);
                    table.CheckConstraint("CK_asignacion_planta_fechas", "[fecha_fin] IS NULL OR [fecha_fin] >= [fecha_inicio]");
                    table.ForeignKey(
                        name: "fk_asignacion_planta_periodo_laboral_periodo_laboral_id",
                        column: x => x.periodo_laboral_id,
                        principalTable: "periodo_laboral",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asignacion_planta_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_periodo_laboral_abierto",
                table: "periodo_laboral",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_cese] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_planta_periodo_laboral_id_fecha_inicio_fecha_fin",
                table: "asignacion_planta",
                columns: new[] { "periodo_laboral_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_planta_planta_id",
                table: "asignacion_planta",
                column: "planta_id");

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_planta_abierta",
                table: "asignacion_planta",
                column: "periodo_laboral_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignacion_planta");

            migrationBuilder.DropIndex(
                name: "uq_periodo_laboral_abierto",
                table: "periodo_laboral");
        }
    }
}

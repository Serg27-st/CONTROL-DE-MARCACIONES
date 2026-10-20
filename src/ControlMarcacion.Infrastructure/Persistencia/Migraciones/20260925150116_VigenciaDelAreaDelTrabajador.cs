using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VigenciaDelAreaDelTrabajador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asignacion_area",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    area_id = table.Column<long>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("pk_asignacion_area", x => x.id);
                    table.ForeignKey(
                        name: "fk_asignacion_area_area_area_id",
                        column: x => x.area_id,
                        principalTable: "area",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asignacion_area_trabajador_trabajador_id",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_area_area_id",
                table: "asignacion_area",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_area_trabajador_id_fecha_inicio_fecha_fin",
                table: "asignacion_area",
                columns: new[] { "trabajador_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_area_abierta",
                table: "asignacion_area",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            // Relleno: cada trabajador con área abre su primera vigencia desde su INGRESO.
            //
            // No sabemos desde cuándo tiene esa área —hasta hoy no se guardaba—, y el ingreso
            // es lo más temprano que pudo tenerla. La consecuencia es la buscada: el día de la
            // migración **ningún reporte cambia de resultado**, porque todo rango cae dentro
            // de esa única vigencia, que lleva exactamente el área de hoy. A partir de aquí,
            // cada cambio abre una fila nueva y el pasado deja de moverse.
            //
            // Los que no tienen área quedan sin fila: ResolverAreasDelRangoAsync conserva
            // entonces lo proyectado desde la ficha, que es como se venía mostrando.
            migrationBuilder.Sql(
                """
                INSERT INTO asignacion_area (
                    trabajador_id, area_id, fecha_inicio, fecha_fin, creado_en, creado_por, activo)
                SELECT t.id, t.area_id, t.fecha_ingreso, NULL, datetime('now'), 'migracion', 1
                FROM trabajador t
                WHERE t.area_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM asignacion_area a WHERE a.trabajador_id = t.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignacion_area");
        }
    }
}

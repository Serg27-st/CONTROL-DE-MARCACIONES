using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class SolicitudCambioTurnoPuntual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitud_cambio_turno",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_efectiva = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    turno_solicitado_id = table.Column<long>(type: "INTEGER", nullable: false),
                    turno_anterior_id = table.Column<long>(type: "INTEGER", nullable: true),
                    cuadrilla_anterior_id = table.Column<long>(type: "INTEGER", nullable: true),
                    motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    solicitado_por_usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    solicitado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    resuelto_por_usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    resuelto_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    motivo_resolucion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitud_cambio_turno", x => x.id);
                    table.ForeignKey(
                        name: "fk_solicitud_cambio_turno_turno_turno_solicitado_id",
                        column: x => x.turno_solicitado_id,
                        principalTable: "turno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_cambio_turno_estado",
                table: "solicitud_cambio_turno",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_cambio_turno_trabajador_id_fecha_efectiva",
                table: "solicitud_cambio_turno",
                columns: new[] { "trabajador_id", "fecha_efectiva" },
                unique: true,
                filter: "estado IN ('Pendiente', 'Aprobada')");

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_cambio_turno_turno_solicitado_id",
                table: "solicitud_cambio_turno",
                column: "turno_solicitado_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitud_cambio_turno");
        }
    }
}

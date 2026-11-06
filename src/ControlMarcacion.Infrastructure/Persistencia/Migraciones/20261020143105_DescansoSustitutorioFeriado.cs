using System;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones;

[DbContext(typeof(MarcacionDbContext))]
[Migration("20261020143105_DescansoSustitutorioFeriado")]
public partial class DescansoSustitutorioFeriado : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "descanso_sustitutorio",
            columns: table => new
            {
                id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                fecha_feriado_origen = table.Column<DateOnly>(type: "TEXT", nullable: false),
                fecha_descanso = table.Column<DateOnly>(type: "TEXT", nullable: false),
                estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                aprobado_por_usuario = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                aprobado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                anulado_por_usuario = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                anulado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                motivo_anulacion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                version = table.Column<int>(type: "INTEGER", nullable: false),
                creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                activo = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_descanso_sustitutorio", x => x.id);
                table.ForeignKey(
                    name: "fk_descanso_sustitutorio_trabajador_trabajador_id",
                    column: x => x.trabajador_id,
                    principalTable: "trabajador",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_descanso_sustitutorio_fecha_descanso",
            table: "descanso_sustitutorio",
            column: "fecha_descanso");
        migrationBuilder.CreateIndex(
            name: "uq_descanso_sustitutorio_fecha_activa",
            table: "descanso_sustitutorio",
            columns: new[] { "trabajador_id", "fecha_descanso" },
            unique: true,
            filter: "activo = 1");
        migrationBuilder.CreateIndex(
            name: "uq_descanso_sustitutorio_feriado_activo",
            table: "descanso_sustitutorio",
            columns: new[] { "trabajador_id", "fecha_feriado_origen" },
            unique: true,
            filter: "activo = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "descanso_sustitutorio");
}

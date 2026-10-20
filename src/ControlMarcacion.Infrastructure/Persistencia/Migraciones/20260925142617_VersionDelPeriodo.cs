using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VersionDelPeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "periodo",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "version_turno",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    turno_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    hora_inicio = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    minutos_refrigerio = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_tolerancia = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_anticipacion_entrada = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_anticipacion_salida = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_gracia_salida = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_jornada_ordinaria = table.Column<int>(type: "INTEGER", nullable: false),
                    dias_aplicables = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version_turno", x => x.id);
                    table.ForeignKey(
                        name: "fk_version_turno_turno_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "periodo",
                keyColumn: "id",
                keyValue: 1L,
                column: "version",
                value: 0);

            migrationBuilder.CreateIndex(
                name: "ix_version_turno_turno_id_fecha_inicio",
                table: "version_turno",
                columns: new[] { "turno_id", "fecha_inicio" });

            migrationBuilder.CreateIndex(
                name: "uq_version_turno_abierta",
                table: "version_turno",
                column: "turno_id",
                unique: true,
                filter: "[fecha_fin] IS NULL AND [activo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "version_turno");

            migrationBuilder.DropColumn(
                name: "version",
                table: "periodo");
        }
    }
}

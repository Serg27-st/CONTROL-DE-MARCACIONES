using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RotacionPorCuadrilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "turno_id",
                table: "asignacion_turno",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<long>(
                name: "cuadrilla_id",
                table: "asignacion_turno",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cuadrilla",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuadrilla", x => x.id);
                    table.ForeignKey(
                        name: "fk_cuadrilla_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "programacion_cuadrilla",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    cuadrilla_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    fecha_ancla = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_programacion_cuadrilla", x => x.id);
                    table.ForeignKey(
                        name: "fk_programacion_cuadrilla_cuadrilla_cuadrilla_id",
                        column: x => x.cuadrilla_id,
                        principalTable: "cuadrilla",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tramo_rotacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    programacion_id = table.Column<long>(type: "INTEGER", nullable: false),
                    orden = table.Column<int>(type: "INTEGER", nullable: false),
                    turno_id = table.Column<long>(type: "INTEGER", nullable: false),
                    dias = table.Column<int>(type: "INTEGER", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tramo_rotacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_tramo_rotacion_programacion_cuadrilla_programacion_id",
                        column: x => x.programacion_id,
                        principalTable: "programacion_cuadrilla",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tramo_rotacion_turno_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_turno_cuadrilla_id",
                table: "asignacion_turno",
                column: "cuadrilla_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_asignacion_turno_turno_o_cuadrilla",
                table: "asignacion_turno",
                sql: "([turno_id] IS NULL) <> ([cuadrilla_id] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_cuadrilla_planta_id_codigo",
                table: "cuadrilla",
                columns: new[] { "planta_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programacion_cuadrilla_cuadrilla_id_fecha_inicio_fecha_fin",
                table: "programacion_cuadrilla",
                columns: new[] { "cuadrilla_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_tramo_rotacion_programacion_id_orden",
                table: "tramo_rotacion",
                columns: new[] { "programacion_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tramo_rotacion_turno_id",
                table: "tramo_rotacion",
                column: "turno_id");

            migrationBuilder.AddForeignKey(
                name: "fk_asignacion_turno_cuadrilla_cuadrilla_id",
                table: "asignacion_turno",
                column: "cuadrilla_id",
                principalTable: "cuadrilla",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asignacion_turno_cuadrilla_cuadrilla_id",
                table: "asignacion_turno");

            migrationBuilder.DropTable(
                name: "tramo_rotacion");

            migrationBuilder.DropTable(
                name: "programacion_cuadrilla");

            migrationBuilder.DropTable(
                name: "cuadrilla");

            migrationBuilder.DropIndex(
                name: "ix_asignacion_turno_cuadrilla_id",
                table: "asignacion_turno");

            migrationBuilder.DropCheckConstraint(
                name: "CK_asignacion_turno_turno_o_cuadrilla",
                table: "asignacion_turno");

            migrationBuilder.DropColumn(
                name: "cuadrilla_id",
                table: "asignacion_turno");

            migrationBuilder.AlterColumn<long>(
                name: "turno_id",
                table: "asignacion_turno",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}

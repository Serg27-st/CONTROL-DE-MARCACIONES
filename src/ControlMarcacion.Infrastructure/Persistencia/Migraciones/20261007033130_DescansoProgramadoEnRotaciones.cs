using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DescansoProgramadoEnRotaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "turno_id",
                table: "tramo_rotacion",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "tipo",
                table: "tramo_rotacion",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Trabajo");

            migrationBuilder.AddColumn<string>(
                name: "coincidencia_feriado_descanso",
                table: "tareo_diario",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Ninguna");

            migrationBuilder.AddColumn<bool>(
                name: "es_descanso_programado",
                table: "tareo_diario",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_tramo_rotacion_descanso_un_dia",
                table: "tramo_rotacion",
                sql: "tipo <> 'Descanso' OR dias = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tramo_rotacion_dias_positivos",
                table: "tramo_rotacion",
                sql: "dias > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tramo_rotacion_orden_positivo",
                table: "tramo_rotacion",
                sql: "orden > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tramo_rotacion_tipo_turno",
                table: "tramo_rotacion",
                sql: "(tipo = 'Trabajo' AND turno_id IS NOT NULL) OR (tipo = 'Descanso' AND turno_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tramo_rotacion_descanso_un_dia",
                table: "tramo_rotacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tramo_rotacion_dias_positivos",
                table: "tramo_rotacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tramo_rotacion_orden_positivo",
                table: "tramo_rotacion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tramo_rotacion_tipo_turno",
                table: "tramo_rotacion");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "tramo_rotacion");

            migrationBuilder.DropColumn(
                name: "coincidencia_feriado_descanso",
                table: "tareo_diario");

            migrationBuilder.DropColumn(
                name: "es_descanso_programado",
                table: "tareo_diario");

            migrationBuilder.AlterColumn<long>(
                name: "turno_id",
                table: "tramo_rotacion",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}

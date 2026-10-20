using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VigenciaDelHorarioDeTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "version_turno",
                columns: new[] { "id", "activo", "creado_en", "creado_por", "dias_aplicables", "fecha_fin", "fecha_inicio", "hora_fin", "hora_inicio", "minutos_anticipacion_entrada", "minutos_anticipacion_salida", "minutos_gracia_salida", "minutos_jornada_ordinaria", "minutos_refrigerio", "minutos_tolerancia", "modificado_en", "modificado_por", "turno_id" },
                values: new object[,]
                {
                    { 1L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "LunesASabado", null, new DateOnly(2000, 1, 1), new TimeOnly(19, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 0, 480, 0, 5, null, null, 1L },
                    { 2L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "LunesASabado", null, new DateOnly(2000, 1, 1), new TimeOnly(7, 0, 0), new TimeOnly(19, 0, 0), 15, 5, 0, 480, 0, 5, null, null, 2L },
                    { 3L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Sabado", null, new DateOnly(2000, 1, 1), new TimeOnly(15, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 0, 480, 0, 5, null, null, 3L },
                    { 4L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Domingo", null, new DateOnly(2000, 1, 1), new TimeOnly(15, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 0, 480, 0, 5, null, null, 4L },
                    { 5L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Domingo", null, new DateOnly(2000, 1, 1), new TimeOnly(19, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 0, 480, 0, 5, null, null, 5L }
                });

            // Relleno de los turnos que NO son los cinco sembrados: los que dio de alta el
            // coordinador. Sin versión, el cálculo cae en el turno tal como está hoy —que es
            // exactamente el defecto que esta migración viene a cerrar—, así que cada uno
            // abre la suya con los valores que tiene ahora y vigencia desde el origen.
            //
            // ⚠️ La fecha de inicio es muy anterior a cualquier dato real (el padrón más
            // antiguo ingresa en 2020) a propósito: el día de la migración ningún recálculo
            // debe cambiar de resultado. Todo el historial cae dentro de esta única versión,
            // que lleva exactamente los valores de hoy.
            //
            // El NOT EXISTS deja fuera a los cinco de arriba, así que insertar y rellenar en
            // la misma migración no los duplica.
            migrationBuilder.Sql(
                """
                INSERT INTO version_turno (
                    turno_id, fecha_inicio, fecha_fin, hora_inicio, hora_fin,
                    minutos_refrigerio, minutos_tolerancia, minutos_anticipacion_entrada,
                    minutos_anticipacion_salida, minutos_gracia_salida, minutos_jornada_ordinaria,
                    dias_aplicables, creado_en, creado_por, activo)
                SELECT
                    t.id, '2000-01-01', NULL, t.hora_inicio, t.hora_fin,
                    t.minutos_refrigerio, t.minutos_tolerancia, t.minutos_anticipacion_entrada,
                    t.minutos_anticipacion_salida, t.minutos_gracia_salida, t.minutos_jornada_ordinaria,
                    t.dias_aplicables, datetime('now'), 'migracion', 1
                FROM turno t
                WHERE NOT EXISTS (SELECT 1 FROM version_turno v WHERE v.turno_id = t.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "version_turno",
                keyColumn: "id",
                keyValue: 5L);
        }
    }
}

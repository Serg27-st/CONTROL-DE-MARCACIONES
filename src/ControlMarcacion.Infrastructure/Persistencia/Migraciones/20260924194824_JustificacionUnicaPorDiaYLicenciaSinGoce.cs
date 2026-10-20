using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Tres correcciones de cálculo: la licencia sin goce deja de pagarse, el permiso por
    /// horas deja de imputarse a tardanza, y una sola justificación por trabajador y día
    /// pasa a garantizarse en la base.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>El índice único puede fallar sobre una base con duplicados.</b> Hasta ahora la
    /// unicidad se comprobaba solo en C#, con un consultar-e-insertar sin transacción, así
    /// que dos operaciones simultáneas podían dejar dos filas del mismo trabajador y día.
    /// Antes de desplegar conviene verificarlo:
    /// <code>
    /// SELECT trabajador_id, fecha, COUNT(*)
    /// FROM justificacion
    /// GROUP BY trabajador_id, fecha
    /// HAVING COUNT(*) > 1;
    /// </code>
    /// Si devuelve filas hay que resolverlas a mano antes de migrar. No se borran aquí a
    /// propósito: elegir cuál de las dos justificaciones sobrevive es una decisión con
    /// autor, y una migración que borra justificaciones en silencio destruye justamente la
    /// evidencia que este sistema existe para conservar.
    /// </remarks>
    public partial class JustificacionUnicaPorDiaYLicenciaSinGoce : Migration
    {
        /// <summary>
        /// Justificaciones de permisos por horas que se guardaron como tardanza.
        /// </summary>
        /// <remarks>
        /// Se convierten para que el recálculo de un periodo abierto las ubique contra el
        /// turno en vez de descontarlas siempre del inicio de la jornada. Solo alcanza a las
        /// que vienen de una solicitud de un tipo con <c>es_por_horas</c>: una tardanza
        /// perdonada de verdad no la toca.
        /// </remarks>
        private const string PermisosGuardadosComoTardanza = @"
            UPDATE justificacion
               SET tipo = '{0}'
             WHERE tipo = '{1}'
               AND solicitud_ausencia_id IS NOT NULL
               AND solicitud_ausencia_id IN (
                     SELECT s.id
                       FROM solicitud_ausencia s
                       JOIN tipo_ausencia t ON t.id = s.tipo_ausencia_id
                      WHERE t.es_por_horas = 1);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion");

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 4L,
                column: "computa_como_asistencia",
                value: false);

            migrationBuilder.Sql(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                PermisosGuardadosComoTardanza,
                "PermisoParcial",
                "Tardanza"));

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion",
                columns: new[] { "trabajador_id", "fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion");

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 4L,
                column: "computa_como_asistencia",
                value: true);

            migrationBuilder.Sql(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                PermisosGuardadosComoTardanza,
                "Tardanza",
                "PermisoParcial"));

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion",
                columns: new[] { "trabajador_id", "fecha" });
        }
    }
}

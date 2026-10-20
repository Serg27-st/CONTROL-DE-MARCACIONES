using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// El modelo de incidencias y regularizaciones: qué hay que resolver en un día, la hora
    /// que el marcador no registró, y el ciclo de aprobación de esa corrección.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es una migración puramente <b>aditiva</b>: ninguna columna se borra, ninguna fila se
    /// toca. Todo lo que ya existía sigue calculándose igual — las columnas nuevas nacen
    /// vacías y el motor todavía no las lee. Quien las llena es la fase siguiente.
    /// </para>
    /// <para>
    /// ⚠️ <b>El valor por omisión de <c>estado</c> se fijó a mano.</b> EF genera
    /// <c>defaultValue: ""</c> para un enum persistido como texto, y esa cadena vacía no se
    /// puede leer de vuelta como <c>EstadoJustificacion</c>: cada justificación ya existente
    /// reventaría al materializarse. Van todas a <c>Aprobada</c>, que es lo correcto además
    /// de lo seguro — las que vienen de una solicitud ya pasaron por su propia autorización,
    /// y dejarlas pendientes desharía ausencias ya decididas.
    /// </para>
    /// <para>
    /// El índice único de <c>(trabajador_id, fecha)</c> pasa a estar <b>filtrado</b>: una
    /// regularización rechazada se conserva como evidencia, pero no puede bloquear ese día
    /// para siempre. Con todas las filas existentes en <c>Aprobada</c> el filtro no cambia
    /// nada hoy, y el índice sigue cubriendo exactamente lo mismo que cubría.
    /// </para>
    /// </remarks>
    public partial class IncidenciasYRegularizaciones : Migration
    {
        /// <summary>Estado con el que nacen las justificaciones que ya existían.</summary>
        private const string EstadoDeLoYaExistente = "Aprobada";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion");

            migrationBuilder.AddColumn<int>(
                name: "marcaciones_fuera_ventana",
                table: "tareo_diario",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "requiere_validacion",
                table: "tareo_diario",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tipo_incidencia",
                table: "tareo_diario",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "entrada_regularizada",
                table: "justificacion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "justificacion",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: EstadoDeLoYaExistente);

            migrationBuilder.AddColumn<string>(
                name: "motivo_resolucion",
                table: "justificacion",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resuelta_en",
                table: "justificacion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resuelta_por_usuario",
                table: "justificacion",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "salida_regularizada",
                table: "justificacion",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_incidencia",
                table: "justificacion",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "justificacion",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_tareo_diario_pendientes_de_validacion",
                table: "tareo_diario",
                columns: new[] { "periodo_id", "requiere_validacion" },
                filter: "requiere_validacion = 1");

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_estado",
                table: "justificacion",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion",
                columns: new[] { "trabajador_id", "fecha" },
                unique: true,
                filter: "estado NOT IN ('Rechazada', 'Cancelada')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tareo_diario_pendientes_de_validacion",
                table: "tareo_diario");

            migrationBuilder.DropIndex(
                name: "ix_justificacion_estado",
                table: "justificacion");

            migrationBuilder.DropIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "marcaciones_fuera_ventana",
                table: "tareo_diario");

            migrationBuilder.DropColumn(
                name: "requiere_validacion",
                table: "tareo_diario");

            migrationBuilder.DropColumn(
                name: "tipo_incidencia",
                table: "tareo_diario");

            migrationBuilder.DropColumn(
                name: "entrada_regularizada",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "motivo_resolucion",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "resuelta_en",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "resuelta_por_usuario",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "salida_regularizada",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "tipo_incidencia",
                table: "justificacion");

            migrationBuilder.DropColumn(
                name: "version",
                table: "justificacion");

            // ⚠️ Vuelve al índice SIN filtro. Si para entonces existe alguna justificación
            // rechazada o cancelada que conviva con otra vigente del mismo día —cosa que el
            // índice filtrado permite a propósito—, esta reversión falla. Es la señal
            // correcta: revertir el modelo de regularizaciones exige decidir antes qué pasa
            // con esas filas, y no es una migración la que debe elegirlo.
            migrationBuilder.CreateIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion",
                columns: new[] { "trabajador_id", "fecha" },
                unique: true);
        }
    }
}

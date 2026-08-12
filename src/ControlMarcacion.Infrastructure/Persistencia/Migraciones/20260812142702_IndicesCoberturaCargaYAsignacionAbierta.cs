using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Índices de dos orígenes distintos. El nombre los menciona a ambos porque una
    /// migración que dice una cosa y hace otra es peor que una migración larga.
    /// </summary>
    /// <remarks>
    /// <b>1. Cobertura de la carga.</b> <c>ix_marcacion_cruda_importacion_id_fecha_hora</c>
    /// reemplaza al índice que solo tenía <c>importacion_id</c>: la pantalla de marcaciones
    /// pregunta el MIN y el MAX de la fecha por cada archivo cargado —qué días trae dentro—
    /// y con la fecha dentro del índice eso se resuelve sin tocar la tabla, que es la más
    /// grande del sistema.
    ///
    /// <b>2. Una sola asignación abierta por trabajador.</b> Los tres índices únicos
    /// filtrados sobre <c>asignacion_marcador</c>, <c>asignacion_turno</c> y
    /// <c>asignacion_sueldo</c> estaban configurados en <c>ConfiguracionesPersonal</c>
    /// desde antes, con su justificación, pero <b>nunca llegaron a la base</b>: su filtro
    /// decía <c>[FechaFin]</c> —el nombre de la propiedad— y la columna se llama
    /// <c>fecha_fin</c>, así que el CREATE INDEX fallaba con "Invalid column name". Al
    /// corregir el filtro, EF los arrastra a la primera migración que se genere, que es
    /// esta. La defensa contra dos asignaciones vigentes a la vez empieza a existir aquí.
    ///
    /// ⚠️ Son <b>únicos</b>: si algún trabajador quedó con dos asignaciones abiertas del
    /// mismo tipo, la migración falla al crearlos. Eso es lo correcto —el dato está mal y
    /// hay que cerrar la sobrante antes—, pero conviene comprobarlo antes de aplicarla
    /// sobre una base con datos reales.
    /// </remarks>
    public partial class IndicesCoberturaCargaYAsignacionAbierta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_marcacion_cruda_importacion_id",
                table: "marcacion_cruda");

            migrationBuilder.CreateIndex(
                name: "ix_marcacion_cruda_importacion_id_fecha_hora",
                table: "marcacion_cruda",
                columns: new[] { "importacion_id", "fecha_hora" });

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_turno_abierta",
                table: "asignacion_turno",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_sueldo_abierta",
                table: "asignacion_sueldo",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_marcador_abierta",
                table: "asignacion_marcador",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_marcacion_cruda_importacion_id_fecha_hora",
                table: "marcacion_cruda");

            migrationBuilder.DropIndex(
                name: "uq_asignacion_turno_abierta",
                table: "asignacion_turno");

            migrationBuilder.DropIndex(
                name: "uq_asignacion_sueldo_abierta",
                table: "asignacion_sueldo");

            migrationBuilder.DropIndex(
                name: "uq_asignacion_marcador_abierta",
                table: "asignacion_marcador");

            migrationBuilder.CreateIndex(
                name: "ix_marcacion_cruda_importacion_id",
                table: "marcacion_cruda",
                column: "importacion_id");
        }
    }
}

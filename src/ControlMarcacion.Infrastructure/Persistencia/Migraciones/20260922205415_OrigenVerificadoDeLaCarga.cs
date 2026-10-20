using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class OrigenVerificadoDeLaCarga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origen_verificado",
                table: "importacion_marcador",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                // Las cargas anteriores nunca se verificaron: la serie no se leía del
                // nombre y ningún equipo tenía la suya. Un texto vacío, además, no se puede
                // leer como OrigenCarga y rompería el historial.
                defaultValue: "NoVerificado");

            migrationBuilder.AddColumn<string>(
                name: "serie_archivo",
                table: "importacion_marcador",
                type: "TEXT",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origen_verificado",
                table: "importacion_marcador");

            migrationBuilder.DropColumn(
                name: "serie_archivo",
                table: "importacion_marcador");
        }
    }
}

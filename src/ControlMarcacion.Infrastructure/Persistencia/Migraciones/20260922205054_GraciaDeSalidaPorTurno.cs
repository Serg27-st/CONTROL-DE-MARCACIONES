using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Gracia de salida por turno: minutos tras el fin que no cuentan como sobretiempo.
    /// </summary>
    /// <remarks>
    /// Nace en 0 en todos los turnos, así que ningún tareo cambia hasta que GCA la fije.
    /// Las <c>UpdateData</c> que generó la herramienta para los cinco turnos sembrados se
    /// quitaron: escribían 0 sobre una columna que ya nace en 0, y en la base de producción
    /// habrían tocado filas que el coordinador pudo haber editado.
    /// </remarks>
    public partial class GraciaDeSalidaPorTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "minutos_gracia_salida",
                table: "turno",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "minutos_gracia_salida",
                table: "turno");
        }
    }
}

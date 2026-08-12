using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DesgloseNocturnoPorTramo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "minutos_extra25_nocturnos",
                table: "tareo_diario",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "minutos_extra35_nocturnos",
                table: "tareo_diario",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "minutos_extra25_nocturnos",
                table: "tareo_diario");

            migrationBuilder.DropColumn(
                name: "minutos_extra35_nocturnos",
                table: "tareo_diario");
        }
    }
}

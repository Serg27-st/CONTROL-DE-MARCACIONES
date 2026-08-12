using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DetalleClinicoSolicitud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "centro_atencion",
                table: "solicitud_ausencia",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contingencia",
                table: "solicitud_ausencia",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "diagnostico",
                table: "solicitud_ausencia",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "centro_atencion",
                table: "solicitud_ausencia");

            migrationBuilder.DropColumn(
                name: "contingencia",
                table: "solicitud_ausencia");

            migrationBuilder.DropColumn(
                name: "diagnostico",
                table: "solicitud_ausencia");
        }
    }
}

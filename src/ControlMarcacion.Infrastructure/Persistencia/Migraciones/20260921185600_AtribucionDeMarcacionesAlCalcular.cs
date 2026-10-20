using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AtribucionDeMarcacionesAlCalcular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_marcacion_cruda_trabajador_id_fecha_hora",
                table: "marcacion_cruda");

            migrationBuilder.DropColumn(
                name: "trabajador_id",
                table: "marcacion_cruda");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "trabajador_id",
                table: "marcacion_cruda",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_marcacion_cruda_trabajador_id_fecha_hora",
                table: "marcacion_cruda",
                columns: new[] { "trabajador_id", "fecha_hora" });
        }
    }
}

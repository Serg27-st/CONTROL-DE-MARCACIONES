using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VentanasAnticipacionTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "minutos_anticipacion_entrada",
                table: "turno",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "minutos_anticipacion_salida",
                table: "turno",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "minutos_anticipacion_entrada", "minutos_anticipacion_salida" },
                values: new object[] { 15, 5 });

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "minutos_anticipacion_entrada", "minutos_anticipacion_salida" },
                values: new object[] { 15, 5 });

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "minutos_anticipacion_entrada", "minutos_anticipacion_salida" },
                values: new object[] { 15, 5 });

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "minutos_anticipacion_entrada", "minutos_anticipacion_salida" },
                values: new object[] { 15, 5 });

            migrationBuilder.UpdateData(
                table: "turno",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "minutos_anticipacion_entrada", "minutos_anticipacion_salida" },
                values: new object[] { 15, 5 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "minutos_anticipacion_entrada",
                table: "turno");

            migrationBuilder.DropColumn(
                name: "minutos_anticipacion_salida",
                table: "turno");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class LimitesSolicitudYVacaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "maximo_dias_por_anio_calendario",
                table: "tipo_ausencia",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "maximo_dias_por_evento",
                table: "tipo_ausencia",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "periodo_vacacional",
                table: "solicitud_ausencia",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subtipo_vacaciones",
                table: "solicitud_ausencia",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { 20, null });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 6L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, 10 });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 7L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, 98 });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 8L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "tipo_ausencia",
                keyColumn: "id",
                keyValue: 9L,
                columns: new[] { "maximo_dias_por_anio_calendario", "maximo_dias_por_evento" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "maximo_dias_por_anio_calendario",
                table: "tipo_ausencia");

            migrationBuilder.DropColumn(
                name: "maximo_dias_por_evento",
                table: "tipo_ausencia");

            migrationBuilder.DropColumn(
                name: "periodo_vacacional",
                table: "solicitud_ausencia");

            migrationBuilder.DropColumn(
                name: "subtipo_vacaciones",
                table: "solicitud_ausencia");
        }
    }
}

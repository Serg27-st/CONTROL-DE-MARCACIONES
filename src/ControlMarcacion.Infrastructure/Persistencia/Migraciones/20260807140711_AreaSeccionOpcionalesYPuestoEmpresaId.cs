using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AreaSeccionOpcionalesYPuestoEmpresaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "seccion_id",
                table: "trabajador",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "area_id",
                table: "trabajador",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "empresa_id",
                table: "puesto",
                type: "bigint",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 1L,
                column: "empresa_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 2L,
                column: "empresa_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 3L,
                column: "empresa_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 4L,
                column: "empresa_id",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "empresa_id",
                table: "puesto");

            migrationBuilder.AlterColumn<long>(
                name: "seccion_id",
                table: "trabajador",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "area_id",
                table: "trabajador",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}

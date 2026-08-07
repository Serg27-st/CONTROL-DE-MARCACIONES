using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class SueldoBasicoTrabajador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asignacion_sueldo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    sueldo_basico_mensual = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asignacion_sueldo", x => x.id);
                    table.ForeignKey(
                        name: "fk_asignacion_sueldo_trabajador_trabajador_id",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_sueldo_trabajador_id_fecha_inicio_fecha_fin",
                table: "asignacion_sueldo",
                columns: new[] { "trabajador_id", "fecha_inicio", "fecha_fin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignacion_sueldo");
        }
    }
}

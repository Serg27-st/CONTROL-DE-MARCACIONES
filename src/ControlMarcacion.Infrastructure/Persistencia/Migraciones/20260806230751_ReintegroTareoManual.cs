using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ReintegroTareoManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reintegro_tareo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    horas_normales = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_extra25 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_extra35 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_no_laborable = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    monto_movilidad = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_nocturnas = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_extra25_nocturnas = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_extra35_nocturnas = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    horas_no_laborable_nocturnas = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    viatico = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    dias_descanso_medico = table.Column<int>(type: "int", nullable: false),
                    dias_subsidio_maternidad = table.Column<int>(type: "int", nullable: false),
                    dias_subsidio_accidente = table.Column<int>(type: "int", nullable: false),
                    dias_licencia_paternidad = table.Column<int>(type: "int", nullable: false),
                    licencia_con_goce = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    reintegro_asignacion_familiar = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    reintegro_afecto = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    descuento_pago_indebido = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reintegro_tareo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_reintegro_tareo_trabajador_periodo",
                table: "reintegro_tareo",
                columns: new[] { "trabajador_id", "periodo_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reintegro_tareo");
        }
    }
}

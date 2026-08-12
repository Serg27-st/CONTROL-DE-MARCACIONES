using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AuditoriaHistorialYCatalogoMarcadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "motivo_cese",
                table: "trabajador",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacion_cese",
                table: "trabajador",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "centro",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "marcador",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    planta_id = table.Column<long>(type: "bigint", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marcador", x => x.id);
                    table.ForeignKey(
                        name: "fk_marcador_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "periodo_laboral",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    empresa_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha_ingreso = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_cese = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo_cese = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    observacion_cese = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_periodo_laboral", x => x.id);
                    table.ForeignKey(
                        name: "fk_periodo_laboral_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_periodo_laboral_trabajador_trabajador_id",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registro_auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tabla = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    clave_registro = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    operacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    usuario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    detalle = table.Column<string>(type: "NVARCHAR(MAX)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registro_auditoria", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 1L,
                column: "descripcion",
                value: "PLANTA INTRACLORO");

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 2L,
                column: "descripcion",
                value: "CENTRO LURÍN");

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 3L,
                column: "descripcion",
                value: "PLANTA SULFONACIÓN");

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 4L,
                column: "descripcion",
                value: "PLANTA DETERGENTES LURÍN");

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 5L,
                column: "descripcion",
                value: "PLANTA INTRACAN/FEM");

            migrationBuilder.CreateIndex(
                name: "ix_marcador_codigo",
                table: "marcador",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marcador_planta_id_activo",
                table: "marcador",
                columns: new[] { "planta_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_periodo_laboral_empresa_id",
                table: "periodo_laboral",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_periodo_laboral_trabajador_id_fecha_ingreso",
                table: "periodo_laboral",
                columns: new[] { "trabajador_id", "fecha_ingreso" });

            migrationBuilder.CreateIndex(
                name: "ix_registro_auditoria_fecha_hora",
                table: "registro_auditoria",
                column: "fecha_hora",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_registro_auditoria_tabla_clave_registro_fecha_hora",
                table: "registro_auditoria",
                columns: new[] { "tabla", "clave_registro", "fecha_hora" });

            migrationBuilder.CreateIndex(
                name: "ix_registro_auditoria_usuario_fecha_hora",
                table: "registro_auditoria",
                columns: new[] { "usuario", "fecha_hora" });

            // ----------------------------------------------------------------
            // Relleno de los datos que ya existen.
            //
            // Sin esto, el catálogo de marcadores nacería vacío y la validación nueva de
            // Personal — "el ID debe existir en el catálogo y ser de la planta del
            // trabajador" — rechazaría el guardado de todas las fichas que ya tienen un ID
            // asignado. Nadie podría editar a nadie hasta dar de alta el catálogo a mano.
            // ----------------------------------------------------------------

            // Un marcador por cada código en uso, atribuido a la planta del trabajador que
            // lo tuvo más recientemente: es la única fuente que hay, porque hasta ahora el
            // ID se escribía a mano y no estaba vinculado a ninguna planta.
            migrationBuilder.Sql("""
                INSERT INTO marcador (codigo, planta_id, descripcion, creado_en, creado_por, activo)
                SELECT  x.id_marcador,
                        x.planta_id,
                        N'Dado de alta automáticamente desde las asignaciones existentes',
                        SYSDATETIME(),
                        N'sistema',
                        1
                FROM (
                    SELECT  a.id_marcador,
                            t.planta_id,
                            ROW_NUMBER() OVER (
                                PARTITION BY a.id_marcador
                                ORDER BY a.fecha_inicio DESC, a.id DESC) AS orden
                    FROM asignacion_marcador AS a
                    INNER JOIN trabajador AS t ON t.id = a.trabajador_id
                ) AS x
                WHERE x.orden = 1
                  AND NOT EXISTS (SELECT 1 FROM marcador AS m WHERE m.codigo = x.id_marcador);
                """);

            // Un vínculo laboral abierto (o ya cerrado) por cada ficha existente, copiando
            // las fechas que hoy viven como campos planos del trabajador. Así la pantalla
            // de historial no aparece vacía para el padrón que ya está cargado.
            migrationBuilder.Sql("""
                INSERT INTO periodo_laboral (
                    trabajador_id, empresa_id, fecha_ingreso, fecha_cese,
                    motivo_cese, observacion_cese, creado_en, creado_por, activo)
                SELECT  t.id,
                        t.empresa_id,
                        t.fecha_ingreso,
                        t.fecha_cese,
                        t.motivo_cese,
                        t.observacion_cese,
                        SYSDATETIME(),
                        N'sistema',
                        1
                FROM trabajador AS t
                WHERE NOT EXISTS (
                    SELECT 1 FROM periodo_laboral AS p WHERE p.trabajador_id = t.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marcador");

            migrationBuilder.DropTable(
                name: "periodo_laboral");

            migrationBuilder.DropTable(
                name: "registro_auditoria");

            migrationBuilder.DropColumn(
                name: "motivo_cese",
                table: "trabajador");

            migrationBuilder.DropColumn(
                name: "observacion_cese",
                table: "trabajador");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "centro");
        }
    }
}

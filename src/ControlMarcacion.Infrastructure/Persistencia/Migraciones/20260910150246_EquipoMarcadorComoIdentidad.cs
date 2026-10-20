using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class EquipoMarcadorComoIdentidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_marcador_codigo",
                table: "marcador");

            migrationBuilder.DropIndex(
                name: "uq_marcacion_cruda_trabajador_fecha_marcador",
                table: "marcacion_cruda");

            migrationBuilder.DropIndex(
                name: "ix_asignacion_marcador_id_marcador_fecha_inicio_fecha_fin",
                table: "asignacion_marcador");

            migrationBuilder.AddColumn<long>(
                name: "equipo_id",
                table: "marcador",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "equipo_id",
                table: "marcacion_cruda",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "equipo_id",
                table: "importacion_marcador",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "equipo_id",
                table: "asignacion_marcador",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "equipo_marcador",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    serie = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false),
                    descripcion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo_marcador", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipo_marcador_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ---------------------------------------------------------------------------
            // RELLENO DE LO QUE YA EXISTE.
            //
            // Va aquí, entre crear la tabla y crear los índices únicos, porque las columnas
            // equipo_id nacen en 0 —un valor que no apunta a ningún equipo— y los índices
            // nuevos las incluyen.
            //
            // El relleno es INEQUÍVOCO y por eso se puede automatizar: hasta esta migración
            // el código de marcador era único en todo el sistema, así que cada identificador
            // pertenece a una sola planta y no hay ninguna ambigüedad que resolver. Se crea
            // un equipo por planta y todo lo suyo cuelga de él. Ninguna atribución histórica
            // cambia: las marcaciones siguen apuntando al mismo trabajador que antes.
            //
            // Después de migrar hay que dar de alta los equipos REALES y reasignar a cada
            // persona al que le corresponde. Eso no lo puede hacer la migración: qué gente
            // marca en qué aparato es un dato que solo tiene la planta.
            // ---------------------------------------------------------------------------
            migrationBuilder.Sql("""
                INSERT INTO equipo_marcador
                    (codigo, nombre, planta_id, descripcion, creado_en, creado_por, activo)
                SELECT
                    'EQ-' || p.id,
                    'Marcador de ' || p.nombre,
                    p.id,
                    'Creado al migrar: agrupa los identificadores que ya existian en esta planta.',
                    '2026-09-10 00:00:00',
                    'migracion',
                    1
                FROM planta p
                WHERE EXISTS (SELECT 1 FROM marcador m WHERE m.planta_id = p.id);
                """);

            // ⚠️ Red de seguridad: si no se creó ningún equipo —catálogo de marcadores
            // vacío— pero sí hay asignaciones o marcaciones de cuando el ID se escribía a
            // mano, esas filas necesitan un equipo al que apuntar. Sin esto quedaban en
            // cero y la clave foránea reventaba la migración entera.
            migrationBuilder.Sql("""
                INSERT INTO equipo_marcador
                    (codigo, nombre, planta_id, descripcion, creado_en, creado_por, activo)
                SELECT
                    'EQ-MIG',
                    'Marcador migrado',
                    (SELECT MIN(id) FROM planta),
                    'Creado al migrar para no dejar huerfanas las asignaciones sin catalogo.',
                    '2026-09-10 00:00:00',
                    'migracion',
                    1
                WHERE NOT EXISTS (SELECT 1 FROM equipo_marcador)
                  AND EXISTS (SELECT 1 FROM planta);
                """);

            migrationBuilder.Sql("""
                UPDATE marcador
                SET equipo_id = COALESCE(
                    (SELECT e.id FROM equipo_marcador e WHERE e.planta_id = marcador.planta_id),
                    (SELECT MIN(id) FROM equipo_marcador));
                """);

            // Las asignaciones y las marcaciones heredan el equipo por el código, que hasta
            // ahora identificaba a un único marcador en todo el sistema. El respaldo por
            // planta cubre los identificadores escritos a mano que nunca entraron al
            // catálogo; ninguno puede quedar en cero.
            migrationBuilder.Sql("""
                UPDATE asignacion_marcador
                SET equipo_id = COALESCE(
                    (SELECT m.equipo_id FROM marcador m
                      WHERE m.codigo = asignacion_marcador.id_marcador),
                    (SELECT e.id FROM equipo_marcador e
                      WHERE e.planta_id = (SELECT t.planta_id FROM trabajador t
                                            WHERE t.id = asignacion_marcador.trabajador_id)),
                    (SELECT MIN(id) FROM equipo_marcador));
                """);

            migrationBuilder.Sql("""
                UPDATE marcacion_cruda
                SET equipo_id = COALESCE(
                    (SELECT m.equipo_id FROM marcador m
                      WHERE m.codigo = marcacion_cruda.id_marcador),
                    (SELECT e.id FROM equipo_marcador e
                      WHERE e.planta_id = (SELECT t.planta_id FROM trabajador t
                                            WHERE t.id = marcacion_cruda.trabajador_id)),
                    (SELECT MIN(id) FROM equipo_marcador));
                """);

            // Una importación toma el equipo de las marcaciones que trajo; si no insertó
            // ninguna —un archivo entero rechazado— cae al primero, porque tampoco alimenta
            // ningún tareo y solo necesita un valor válido.
            migrationBuilder.Sql("""
                UPDATE importacion_marcador
                SET equipo_id = COALESCE(
                    (SELECT mc.equipo_id FROM marcacion_cruda mc
                      WHERE mc.importacion_id = importacion_marcador.id LIMIT 1),
                    (SELECT MIN(id) FROM equipo_marcador));
                """);

            migrationBuilder.CreateIndex(
                name: "uq_marcador_equipo_codigo",
                table: "marcador",
                columns: new[] { "equipo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_marcacion_cruda_equipo_marcador_fecha",
                table: "marcacion_cruda",
                columns: new[] { "equipo_id", "id_marcador", "fecha_hora" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_importacion_marcador_equipo_id_fecha_carga",
                table: "importacion_marcador",
                columns: new[] { "equipo_id", "fecha_carga" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_marcador_equipo_id_id_marcador_fecha_inicio_fecha_fin",
                table: "asignacion_marcador",
                columns: new[] { "equipo_id", "id_marcador", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_marcador_equipo_codigo_abierta",
                table: "asignacion_marcador",
                columns: new[] { "equipo_id", "id_marcador" },
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_marcador_codigo",
                table: "equipo_marcador",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipo_marcador_planta_id_activo",
                table: "equipo_marcador",
                columns: new[] { "planta_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "uq_equipo_marcador_serie",
                table: "equipo_marcador",
                column: "serie",
                unique: true,
                filter: "[serie] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_asignacion_marcador_equipo_marcador_equipo_id",
                table: "asignacion_marcador",
                column: "equipo_id",
                principalTable: "equipo_marcador",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_importacion_marcador_equipo_marcador_equipo_id",
                table: "importacion_marcador",
                column: "equipo_id",
                principalTable: "equipo_marcador",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_marcacion_cruda_equipo_marcador_equipo_id",
                table: "marcacion_cruda",
                column: "equipo_id",
                principalTable: "equipo_marcador",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_marcador_equipo_marcador_equipo_id",
                table: "marcador",
                column: "equipo_id",
                principalTable: "equipo_marcador",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asignacion_marcador_equipo_marcador_equipo_id",
                table: "asignacion_marcador");

            migrationBuilder.DropForeignKey(
                name: "fk_importacion_marcador_equipo_marcador_equipo_id",
                table: "importacion_marcador");

            migrationBuilder.DropForeignKey(
                name: "fk_marcacion_cruda_equipo_marcador_equipo_id",
                table: "marcacion_cruda");

            migrationBuilder.DropForeignKey(
                name: "fk_marcador_equipo_marcador_equipo_id",
                table: "marcador");

            migrationBuilder.DropTable(
                name: "equipo_marcador");

            migrationBuilder.DropIndex(
                name: "uq_marcador_equipo_codigo",
                table: "marcador");

            migrationBuilder.DropIndex(
                name: "uq_marcacion_cruda_equipo_marcador_fecha",
                table: "marcacion_cruda");

            migrationBuilder.DropIndex(
                name: "ix_importacion_marcador_equipo_id_fecha_carga",
                table: "importacion_marcador");

            migrationBuilder.DropIndex(
                name: "ix_asignacion_marcador_equipo_id_id_marcador_fecha_inicio_fecha_fin",
                table: "asignacion_marcador");

            migrationBuilder.DropIndex(
                name: "uq_asignacion_marcador_equipo_codigo_abierta",
                table: "asignacion_marcador");

            migrationBuilder.DropColumn(
                name: "equipo_id",
                table: "marcador");

            migrationBuilder.DropColumn(
                name: "equipo_id",
                table: "marcacion_cruda");

            migrationBuilder.DropColumn(
                name: "equipo_id",
                table: "importacion_marcador");

            migrationBuilder.DropColumn(
                name: "equipo_id",
                table: "asignacion_marcador");

            migrationBuilder.CreateIndex(
                name: "ix_marcador_codigo",
                table: "marcador",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_marcacion_cruda_trabajador_fecha_marcador",
                table: "marcacion_cruda",
                columns: new[] { "trabajador_id", "fecha_hora", "id_marcador" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_marcador_id_marcador_fecha_inicio_fecha_fin",
                table: "asignacion_marcador",
                columns: new[] { "id_marcador", "fecha_inicio", "fecha_fin" });
        }
    }
}

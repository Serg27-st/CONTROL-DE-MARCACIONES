using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MarcacionesProvisionales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prov_importacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nombre_archivo = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    equipo_id = table.Column<long>(type: "INTEGER", nullable: false),
                    hash_contenido = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "TEXT", nullable: false),
                    cargado_por_usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    filas_leidas = table.Column<int>(type: "INTEGER", nullable: false),
                    filas_insertadas = table.Column<int>(type: "INTEGER", nullable: false),
                    filas_rechazadas = table.Column<int>(type: "INTEGER", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prov_importacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_prov_importacion_equipo_marcador_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo_marcador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prov_error_importacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    importacion_provisional_id = table.Column<long>(type: "INTEGER", nullable: false),
                    numero_linea = table.Column<int>(type: "INTEGER", nullable: false),
                    contenido_linea = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    motivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prov_error_importacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_prov_error_importacion_prov_importacion_importacion_provisional_id",
                        column: x => x.importacion_provisional_id,
                        principalTable: "prov_importacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prov_marcacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    equipo_id = table.Column<long>(type: "INTEGER", nullable: false),
                    id_marcador = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modo_verificacion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    importacion_provisional_id = table.Column<long>(type: "INTEGER", nullable: false),
                    linea_original = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prov_marcacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_prov_marcacion_equipo_marcador_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo_marcador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prov_marcacion_prov_importacion_importacion_provisional_id",
                        column: x => x.importacion_provisional_id,
                        principalTable: "prov_importacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prov_error_importacion",
                table: "prov_error_importacion",
                column: "importacion_provisional_id");

            migrationBuilder.CreateIndex(
                name: "ix_prov_importacion_equipo_fecha",
                table: "prov_importacion",
                columns: new[] { "equipo_id", "fecha_carga" });

            migrationBuilder.CreateIndex(
                name: "ix_prov_importacion_hash",
                table: "prov_importacion",
                column: "hash_contenido");

            migrationBuilder.CreateIndex(
                name: "ix_prov_marcacion_fecha",
                table: "prov_marcacion",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "ix_prov_marcacion_importacion",
                table: "prov_marcacion",
                column: "importacion_provisional_id");

            migrationBuilder.CreateIndex(
                name: "uq_prov_marcacion_equipo_marcador_fecha",
                table: "prov_marcacion",
                columns: new[] { "equipo_id", "id_marcador", "fecha_hora" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prov_error_importacion");

            migrationBuilder.DropTable(
                name: "prov_marcacion");

            migrationBuilder.DropTable(
                name: "prov_importacion");
        }
    }
}

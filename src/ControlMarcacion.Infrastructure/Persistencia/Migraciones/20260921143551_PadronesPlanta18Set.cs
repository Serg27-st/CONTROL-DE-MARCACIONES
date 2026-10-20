using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PadronesPlanta18Set : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "planta_id",
                table: "centro",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 1L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 2L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 10L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 11L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 12L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 13L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 14L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 15L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 16L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 17L,
                column: "activo",
                value: false);

            migrationBuilder.InsertData(
                table: "area",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre", "planta_id" },
                values: new object[,]
                {
                    { 20L, true, "ENVDET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado Detergentes Lurín", 1L },
                    { 21L, true, "ENVLAV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado Lavavajillas en pote", 1L },
                    { 22L, true, "ENVPOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado Polvo/Pulidor", 1L },
                    { 23L, true, "GINDET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Gastos Indirectos Detergentes / Lavavajilla", 1L },
                    { 24L, true, "MANDET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Manufactura Detergentes Lurín", 1L },
                    { 25L, true, "ETIQ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción Etiquetado/Codificado", 1L },
                    { 26L, true, "SEGDET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Seguridad Detergentes", 1L },
                    { 30L, true, "ACAFIB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Acabados fibras, paños y otros", 2L },
                    { 31L, true, "GINDET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Gastos Indirectos Detergentes / Lavavajilla", 2L },
                    { 32L, true, "MANFIB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Manufactura Fibras", 2L },
                    { 33L, true, "ETIQ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción Etiquetado/Codificado", 2L },
                    { 40L, true, "CENLUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Centro Lurin", 3L },
                    { 41L, true, "ENVAER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado de Aerosoles/Insecticidas", 3L },
                    { 42L, true, "ENVLEJ", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado de Lejia", 3L },
                    { 43L, true, "ENVLIM", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado de Limpiatodo y LavavajillaLiq", 3L },
                    { 44L, true, "ENVSAN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "ENVASADO SANITARIOS ACUOSO", 3L },
                    { 45L, true, "ENVWC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Envasado WC liquido", 3L },
                    { 46L, true, "ENVCOS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "ENVASES COSMETICOS ACUOSOS", 3L },
                    { 47L, true, "GINLUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Gastos Indirectos Lurin", 3L },
                    { 48L, true, "GERPRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Gerencia de Produccion", 3L },
                    { 49L, true, "MANCOS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Manufactura Cosmeticos Acuosos", 3L },
                    { 50L, true, "MANPAS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "MANUFACTURA PASTA DENTAL", 3L },
                    { 51L, true, "MANSAN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Manufactura Sanitarios Acuosos", 3L },
                    { 52L, true, "MEZRES", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Mezclado de Resinas", 3L },
                    { 53L, true, "OFEINT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "OFERTADO INTRALAB", 3L },
                    { 54L, true, "PASWC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PASTILLAS WC", 3L },
                    { 55L, true, "TUBPAS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PROD TUBO PASTA DENTAL", 3L },
                    { 56L, true, "CEPILL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción Cepillos dentales", 3L },
                    { 57L, true, "ENSAMB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción Ensamblajes", 3L },
                    { 58L, true, "INYECC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción Inyección", 3L },
                    { 59L, true, "OFELUR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PRODUCCION OFERTADO LURIN 02", 3L },
                    { 60L, true, "SOPLPE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción Soplado PE", 3L },
                    { 61L, true, "SEGIND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "SEGURIDAD INDUSTRIAL", 3L }
                });

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 1L,
                column: "planta_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 2L,
                column: "planta_id",
                value: 3L);

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 3L,
                column: "planta_id",
                value: 4L);

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 4L,
                column: "planta_id",
                value: 1L);

            migrationBuilder.UpdateData(
                table: "centro",
                keyColumn: "id",
                keyValue: 5L,
                column: "planta_id",
                value: 2L);

            migrationBuilder.InsertData(
                table: "centro_costo",
                columns: new[] { "id", "activo", "centro_id", "codigo", "creado_en", "creado_por", "descripcion", "modificado_en", "modificado_por" },
                values: new object[,]
                {
                    { 107L, true, 5L, "PE21400607", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN ETIQUETADO/CODIFICADO", null, null },
                    { 108L, true, 2L, "PE21400002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PASTILLAS WC", null, null },
                    { 109L, true, 2L, "PE21400105", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "OFERTADO INTRALAB", null, null },
                    { 110L, true, 2L, "PE21400705", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO WC LIQUIDO", null, null },
                    { 111L, true, 2L, "PE21400713", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PASTA DENTAL", null, null },
                    { 112L, true, 2L, "PE21400714", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENV. COSMETIC. ACUOSOS", null, null },
                    { 113L, true, 2L, "PE21400716", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA COSMETICOS ACUOSOS", null, null }
                });

            migrationBuilder.UpdateData(
                table: "empresa",
                keyColumn: "id",
                keyValue: 2L,
                column: "razon_social",
                value: "CA OUTSOURCING SAC");

            migrationBuilder.UpdateData(
                table: "planta",
                keyColumn: "id",
                keyValue: 1L,
                column: "nombre",
                value: "PLANTA DETERGENTES LURÍN");

            migrationBuilder.UpdateData(
                table: "planta",
                keyColumn: "id",
                keyValue: 2L,
                column: "nombre",
                value: "PLANTA INTRACAN/FEM");

            migrationBuilder.UpdateData(
                table: "planta",
                keyColumn: "id",
                keyValue: 3L,
                column: "nombre",
                value: "PLANTA 1");

            migrationBuilder.InsertData(
                table: "puesto",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 20L, true, "CA-OPERARIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "OPERARIO" },
                    { 21L, true, "CA-OMAQUINA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "O. MAQUINA" },
                    { 22L, true, "CA-OTRASPALET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "O. TRASPALET" },
                    { 23L, true, "CA-SSOMA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "SSOMA" },
                    { 24L, true, "CA-COORDINADOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "COORDINADOR" },
                    { 25L, true, "CA-BATIDOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "BATIDOR" },
                    { 26L, true, "CA-COORDRRHH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "COORD. RRHH" },
                    { 27L, true, "CA-BOLETERA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "BOLETERA" },
                    { 30L, true, "CAO-OPERARIO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "OPERARIO" },
                    { 31L, true, "CAO-MAQUINISTA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "MAQUINISTA" },
                    { 32L, true, "CAO-SUPERVISOR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "SUPERVISOR" },
                    { 33L, true, "CAO-INSPSSOMA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "INSPECTOR SSOMA" },
                    { 34L, true, "CAO-TRASPALETIS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "TRASPALETISTA" },
                    { 35L, true, "CAO-EMPLEADO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "EMPLEADO" }
                });

            migrationBuilder.UpdateData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 1L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 2L,
                column: "activo",
                value: false);

            migrationBuilder.UpdateData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: false);

            migrationBuilder.CreateIndex(
                name: "ix_centro_planta_id",
                table: "centro",
                column: "planta_id");

            migrationBuilder.AddForeignKey(
                name: "fk_centro_planta_planta_id",
                table: "centro",
                column: "planta_id",
                principalTable: "planta",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_centro_planta_planta_id",
                table: "centro");

            migrationBuilder.DropIndex(
                name: "ix_centro_planta_id",
                table: "centro");

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 33L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 40L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 41L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 42L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 43L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 44L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 45L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 46L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 47L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 48L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 49L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 50L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 51L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 52L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 53L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 54L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 55L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 56L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 57L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 58L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 59L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 60L);

            migrationBuilder.DeleteData(
                table: "area",
                keyColumn: "id",
                keyValue: 61L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 107L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 108L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 109L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 110L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 111L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 112L);

            migrationBuilder.DeleteData(
                table: "centro_costo",
                keyColumn: "id",
                keyValue: 113L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 33L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 34L);

            migrationBuilder.DeleteData(
                table: "puesto",
                keyColumn: "id",
                keyValue: 35L);

            migrationBuilder.DropColumn(
                name: "planta_id",
                table: "centro");

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 1L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 2L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 10L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 11L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 12L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 13L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 14L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 15L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 16L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "area",
                keyColumn: "id",
                keyValue: 17L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "empresa",
                keyColumn: "id",
                keyValue: 2L,
                column: "razon_social",
                value: "CA Outsourcing");

            migrationBuilder.UpdateData(
                table: "planta",
                keyColumn: "id",
                keyValue: 1L,
                column: "nombre",
                value: "Planta Detergentes");

            migrationBuilder.UpdateData(
                table: "planta",
                keyColumn: "id",
                keyValue: 2L,
                column: "nombre",
                value: "Planta 20 mil");

            migrationBuilder.UpdateData(
                table: "planta",
                keyColumn: "id",
                keyValue: 3L,
                column: "nombre",
                value: "Planta 01");

            migrationBuilder.UpdateData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 1L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 2L,
                column: "activo",
                value: true);

            migrationBuilder.UpdateData(
                table: "seccion",
                keyColumn: "id",
                keyValue: 3L,
                column: "activo",
                value: true);
        }
    }
}

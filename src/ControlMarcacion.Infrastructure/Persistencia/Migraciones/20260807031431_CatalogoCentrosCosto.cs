using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CatalogoCentrosCosto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "centro_costo_id",
                table: "trabajador",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "centro",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    empresa_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_centro", x => x.id);
                    table.ForeignKey(
                        name: "fk_centro_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "centro_costo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    centro_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_centro_costo", x => x.id);
                    table.ForeignKey(
                        name: "fk_centro_costo_centro_centro_id",
                        column: x => x.centro_id,
                        principalTable: "centro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "centro",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por" },
                values: new object[,]
                {
                    { 1L, true, "066D", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null },
                    { 2L, true, "066E", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null },
                    { 3L, true, "066F", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null },
                    { 4L, true, "066G", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null },
                    { 5L, true, "066H", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null }
                });

            migrationBuilder.InsertData(
                table: "centro_costo",
                columns: new[] { "id", "activo", "centro_id", "codigo", "creado_en", "creado_por", "descripcion", "modificado_en", "modificado_por" },
                values: new object[,]
                {
                    { 1L, true, 1L, "PE21400700", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN PET RECICLADO", null, null },
                    { 2L, true, 1L, "PE21400701", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PASTILLAS WC", null, null },
                    { 3L, true, 1L, "PE21400702", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA WC LÍQUIDO GRANEL", null, null },
                    { 4L, true, 1L, "PE21400703", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA GRANEL HIPOCLORITO", null, null },
                    { 5L, true, 1L, "PE21400704", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "IMPRESIÓN BALDES", null, null },
                    { 6L, true, 1L, "PE21400705", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO WC LÍQUIDO", null, null },
                    { 7L, true, 1L, "PE21400706", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO HIPOCLORITO CISTERNA", null, null },
                    { 8L, true, 1L, "PE21420700", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS INDIRECTOS INTRACLORO", null, null },
                    { 9L, true, 1L, "PE21420701", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANTENIMIENTO INTRACLORO", null, null },
                    { 10L, true, 1L, "PE21420702", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD INTRACLORO", null, null },
                    { 11L, true, 1L, "PE21420703", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CALIDAD INTRACLORO", null, null },
                    { 12L, true, 1L, "PE21420704", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ALMACÉN EEIIREP INTRACLORO", null, null },
                    { 13L, true, 1L, "PE21430001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DE LA PRODUCCIÓN", null, null },
                    { 14L, true, 1L, "PE21430006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS COVID", null, null },
                    { 15L, true, 1L, "PE21430011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD INDUSTRIAL", null, null },
                    { 16L, true, 2L, "PE21300004", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ALMACÉN PT PLANTA 1", null, null },
                    { 17L, true, 2L, "PE21300005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CENTRO DE DISTRIBUCIÓN PRINCIPAL", null, null },
                    { 18L, true, 2L, "PE21400107", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA SANITARIOS ACUOSOS", null, null },
                    { 19L, true, 2L, "PE21400114", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO WC", null, null },
                    { 20L, true, 2L, "PE21400601", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN SOPLADO PET", null, null },
                    { 21L, true, 2L, "PE21400602", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN SOPLADO PE", null, null },
                    { 22L, true, 2L, "PE21400603", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN OFERTADO", null, null },
                    { 23L, true, 2L, "PE21400604", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN LÍNEA FLEXOGRAFIA", null, null },
                    { 24L, true, 2L, "PE21400605", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN LÍN BARNIZADO Y LITOGRHOJALAT", null, null },
                    { 25L, true, 2L, "PE21400606", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN INYECCIÓN", null, null },
                    { 26L, true, 2L, "PE21400607", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN ETIQUETADO/CODIFICADO", null, null },
                    { 27L, true, 2L, "PE21400608", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN ENVASES DE ALUMINIO", null, null },
                    { 28L, true, 2L, "PE21400609", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN ENSAMBLAJES", null, null },
                    { 29L, true, 2L, "PE21400610", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN CEPILLOS DENTALES", null, null },
                    { 30L, true, 2L, "PE21400611", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN BOLSAS PLÁSTICAS", null, null },
                    { 31L, true, 2L, "PE21400612", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MEZCLADO DE RESINAS", null, null },
                    { 32L, true, 2L, "PE21400613", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA GRANELES LEJÍA", null, null },
                    { 33L, true, 2L, "PE21400614", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA GRANEL LIMPIATOD-LAVALÍQ", null, null },
                    { 34L, true, 2L, "PE21400615", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA GRANAEROSOLES/INSECTICIDA", null, null },
                    { 35L, true, 2L, "PE21400616", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO DE LIMPIATODO Y LAVAVAJILLALÍQ", null, null },
                    { 36L, true, 2L, "PE21400617", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO DE LEJÍA", null, null },
                    { 37L, true, 2L, "PE21400618", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO DE AEROSOLES/INSECTICIDAS", null, null },
                    { 38L, true, 2L, "PE21400619", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENSAMBLE DE ENVASES DE HOJALATA", null, null },
                    { 39L, true, 2L, "PE21400707", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PROD TUB PASTA DENTAL", null, null },
                    { 40L, true, 2L, "PE21400712", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO SANITARIOS ACUOSOS", null, null },
                    { 41L, true, 2L, "PE21400715", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO CERAS", null, null },
                    { 42L, true, 2L, "PE21420600", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS INDIRECTOS LURIN", null, null },
                    { 43L, true, 2L, "PE21420601", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "TRATAMIENTO DE AGUA", null, null },
                    { 44L, true, 2L, "PE21420602", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANTENIMIENTO LURIN", null, null },
                    { 45L, true, 2L, "PE21420603", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD LURIN", null, null },
                    { 46L, true, 2L, "PE21420604", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CALIDAD LURIN", null, null },
                    { 47L, true, 2L, "PE21420605", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ALMACÉN EEIIREP LURÍN PRINCIPAL", null, null },
                    { 48L, true, 2L, "PE21430000", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DEL MEDIO AMBIENTE", null, null },
                    { 49L, true, 2L, "PE21430001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DE LA PRODUCCIÓN", null, null },
                    { 50L, true, 2L, "PE21430006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS COVID", null, null },
                    { 51L, true, 2L, "PE21430011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD INDUSTRIAL", null, null },
                    { 52L, true, 2L, "PE21430012", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DE MANTENIMIENTO", null, null },
                    { 53L, true, 2L, "PE21430015", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANTENIMIENTO ELÉCTRICO/INSTRUMENTACIÓN", null, null },
                    { 54L, true, 3L, "PE21400105", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "OFERTADO INTRALAB", null, null },
                    { 55L, true, 3L, "PE21400200", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN TUBOS PLÁSTICOS COSMÉTICOS", null, null },
                    { 56L, true, 3L, "PE21400201", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN SULFONACIÓN", null, null },
                    { 57L, true, 3L, "PE21400202", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO ÁCIDO SULFÓNICO", null, null },
                    { 58L, true, 3L, "PE21400701", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PASTILLA WC", null, null },
                    { 59L, true, 3L, "PE21400705", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO WC LIQUIDO", null, null },
                    { 60L, true, 3L, "PE21400708", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "OFERTADO PERSONAL CARE", null, null },
                    { 61L, true, 3L, "PE21400710", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "JABON LIQUIDO", null, null },
                    { 62L, true, 3L, "PE21400713", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PASTA DENTAL", null, null },
                    { 63L, true, 3L, "PE21420200", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS INDIRECTOS SULFONACIÓN / TUBEPACK", null, null },
                    { 64L, true, 3L, "PE21420201", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANTENIMIENTO  SULFONACIÓN/TUBEPACK", null, null },
                    { 65L, true, 3L, "PE21420202", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD SULFONACIÓN /TUBEPACK", null, null },
                    { 66L, true, 3L, "PE21420203", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CALIDAD SULFONACIÓN  /TUBEPACK", null, null },
                    { 67L, true, 3L, "PE21420204", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ALMACÉN EEIIREP SULFONACIÓN / TUBEPACK", null, null },
                    { 68L, true, 3L, "PE21430001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DE LA PRODUCCIÓN", null, null },
                    { 69L, true, 3L, "PE21430006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS COVID", null, null },
                    { 70L, true, 4L, "PE21300005", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CENTRO DE DISTRIBUCIÓN PRINCIPAL", null, null },
                    { 71L, true, 4L, "PE21400300", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN ENVASES PLÁSTICOS TERMOFORMA", null, null },
                    { 72L, true, 4L, "PE21400301", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA POLVO/PULIDOR", null, null },
                    { 73L, true, 4L, "PE21400302", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA LAVAVAJILLA POTE", null, null },
                    { 74L, true, 4L, "PE21400303", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA DETERGENTES LURÍN", null, null },
                    { 75L, true, 4L, "PE21400304", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "IMPRESIÓN POTES PLÁSTICOS LURÍN", null, null },
                    { 76L, true, 4L, "PE21400305", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO POLVO/PULIDOR", null, null },
                    { 77L, true, 4L, "PE21400306", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO LAVAVAJILLAS EN POTE", null, null },
                    { 78L, true, 4L, "PE21400307", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO DETERGENTES LURÍN", null, null },
                    { 79L, true, 4L, "PE21400506", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACABADOS FIBRAS, PAÑOS Y OTROS", null, null },
                    { 80L, true, 4L, "PE21420300", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS INDIR DETERGENTES / LAVAVAJILLA", null, null },
                    { 81L, true, 4L, "PE21420301", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANTENIMIENTO DETERGENTES", null, null },
                    { 82L, true, 4L, "PE21420302", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD DETERGENTES", null, null },
                    { 83L, true, 4L, "PE21420303", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CALIDAD DETERGENTES", null, null },
                    { 84L, true, 4L, "PE21420304", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ALMACÉN EEIIREP DETERGENTE", null, null },
                    { 85L, true, 4L, "PE21430000", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DEL MEDIO AMBIENTE", null, null },
                    { 86L, true, 4L, "PE21430001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DE LA PRODUCCIÓN", null, null },
                    { 87L, true, 4L, "PE21430006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS COVID", null, null },
                    { 88L, true, 4L, "PE21430011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD INDUSTRIAL", null, null },
                    { 89L, true, 5L, "PE21400105", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "OFERTADO INTRALAB", null, null },
                    { 90L, true, 5L, "PE21400500", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN ENVASES HOJALATA", null, null },
                    { 91L, true, 5L, "PE21400501", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN DE DOMOS Y FONDOS", null, null },
                    { 92L, true, 5L, "PE21400502", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN DE CORTE DE BOBINAS DE HOJAL", null, null },
                    { 93L, true, 5L, "PE21400503", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PRODUCCIÓN DE BARNIZADO Y LITOGRAFÍA", null, null },
                    { 94L, true, 5L, "PE21400504", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA PAÑO AMARILLO", null, null },
                    { 95L, true, 5L, "PE21400505", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANUFACTURA FIBRA", null, null },
                    { 96L, true, 5L, "PE21400506", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ACABADOS FIBRAS, PAÑOS Y OTROS", null, null },
                    { 97L, true, 5L, "PE21400705", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENVASADO WC LIQUIDO", null, null },
                    { 98L, true, 5L, "PE21400714", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ENV. COSMETIC. ACUOSOS", null, null },
                    { 99L, true, 5L, "PE21420500", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS INDIRECTOS INTRACAN", null, null },
                    { 100L, true, 5L, "PE21420502", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "MANTENIMIENTO INTRACAN", null, null },
                    { 101L, true, 5L, "PE21420503", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD INTRACAN", null, null },
                    { 102L, true, 5L, "PE21420504", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CALIDAD INTRACAN", null, null },
                    { 103L, true, 5L, "PE21420505", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "ALMACÉN EEIIREP INTRACAN", null, null },
                    { 104L, true, 5L, "PE21430001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GERENCIA DE LA PRODUCCIÓN", null, null },
                    { 105L, true, 5L, "PE21430006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "GASTOS COVID", null, null },
                    { 106L, true, 5L, "PE21430011", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "SEGURIDAD INDUSTRIAL", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_centro_empresa_id_codigo",
                table: "centro",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_centro_costo_centro_id_codigo",
                table: "centro_costo",
                columns: new[] { "centro_id", "codigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "centro_costo");

            migrationBuilder.DropTable(
                name: "centro");

            migrationBuilder.DropColumn(
                name: "centro_costo_id",
                table: "trabajador");
        }
    }
}

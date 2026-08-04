using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControlMarcacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cliente",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cliente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feriado",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    planta_id = table.Column<long>(type: "bigint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feriado", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "importacion_marcador",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_archivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    hash_contenido = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "datetime2", nullable: false),
                    cargado_por_usuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    filas_leidas = table.Column<int>(type: "int", nullable: false),
                    filas_insertadas = table.Column<int>(type: "int", nullable: false),
                    filas_duplicadas = table.Column<int>(type: "int", nullable: false),
                    filas_rechazadas = table.Column<int>(type: "int", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importacion_marcador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "periodo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    cerrado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cerrado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    reabierto_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reabierto_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    motivo_reapertura = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_periodo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "puesto",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_puesto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tareo_diario",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    turno_id = table.Column<long>(type: "bigint", nullable: true),
                    entrada_real = table.Column<DateTime>(type: "datetime2", nullable: true),
                    salida_real = table.Column<DateTime>(type: "datetime2", nullable: true),
                    minutos_trabajados = table.Column<int>(type: "int", nullable: false),
                    minutos_ordinarios = table.Column<int>(type: "int", nullable: false),
                    minutos_extra25 = table.Column<int>(type: "int", nullable: false),
                    minutos_extra35 = table.Column<int>(type: "int", nullable: false),
                    minutos_nocturnos = table.Column<int>(type: "int", nullable: false),
                    minutos_tardanza = table.Column<int>(type: "int", nullable: false),
                    minutos_salida_anticipada = table.Column<int>(type: "int", nullable: false),
                    es_dia_no_laborable = table.Column<bool>(type: "bit", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    justificacion_id = table.Column<long>(type: "bigint", nullable: true),
                    observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    calculado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tareo_diario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasa_sobretasa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    concepto = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    porcentaje = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    base_legal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    empresa_id = table.Column<long>(type: "bigint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasa_sobretasa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_ausencia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    comportamiento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    computa_como_asistencia = table.Column<bool>(type: "bit", nullable: false),
                    requiere_sustento = table.Column<bool>(type: "bit", nullable: false),
                    es_por_horas = table.Column<bool>(type: "bit", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_ausencia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_documento",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    longitud_exacta = table.Column<int>(type: "int", nullable: true),
                    solo_digitos = table.Column<bool>(type: "bit", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_documento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trabajador",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipo_documento_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_documento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    apellido_paterno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    apellido_materno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    nombres = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    sexo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    correo_electronico = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    tiene_hijos = table.Column<bool>(type: "bit", nullable: false),
                    empresa_id = table.Column<long>(type: "bigint", nullable: false),
                    unidad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    tipo_planilla = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    planta_id = table.Column<long>(type: "bigint", nullable: false),
                    area_id = table.Column<long>(type: "bigint", nullable: false),
                    seccion_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha_ingreso = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_cese = table.Column<DateOnly>(type: "date", nullable: true),
                    puesto_id = table.Column<long>(type: "bigint", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turno",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time", nullable: false),
                    minutos_refrigerio = table.Column<int>(type: "int", nullable: false),
                    minutos_tolerancia = table.Column<int>(type: "int", nullable: false),
                    minutos_jornada_ordinaria = table.Column<int>(type: "int", nullable: false),
                    dias_aplicables = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turno", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "empresa",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresa", x => x.id);
                    table.ForeignKey(
                        name: "fk_empresa_cliente_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "error_importacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    importacion_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_linea = table.Column<int>(type: "int", nullable: false),
                    contenido_linea = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_importacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_error_importacion_importacion_marcador_importacion_id",
                        column: x => x.importacion_id,
                        principalTable: "importacion_marcador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marcacion_cruda",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    id_marcador = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    importacion_id = table.Column<long>(type: "bigint", nullable: false),
                    modo_verificacion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    tipo_marcacion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    linea_original = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marcacion_cruda", x => x.id);
                    table.ForeignKey(
                        name: "fk_marcacion_cruda_importacion_marcador_importacion_id",
                        column: x => x.importacion_id,
                        principalTable: "importacion_marcador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitud_ausencia",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo_ausencia_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time", nullable: true),
                    hora_fin = table.Column<TimeOnly>(type: "time", nullable: true),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    sustento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ruta_documento_sustento = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    motivo_anulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitud_ausencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_solicitud_ausencia_tipo_ausencia_tipo_ausencia_id",
                        column: x => x.tipo_ausencia_id,
                        principalTable: "tipo_ausencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asignacion_marcador",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    id_marcador = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("pk_asignacion_marcador", x => x.id);
                    table.ForeignKey(
                        name: "fk_asignacion_marcador_trabajador_trabajador_id",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asignacion_turno",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    turno_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_asignacion_turno", x => x.id);
                    table.ForeignKey(
                        name: "fk_asignacion_turno_trabajador_trabajador_id",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asignacion_turno_turno_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planta",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    empresa_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planta", x => x.id);
                    table.ForeignKey(
                        name: "fk_planta_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "justificacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trabajador_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    solicitud_ausencia_id = table.Column<long>(type: "bigint", nullable: true),
                    tipo_ausencia_id = table.Column<long>(type: "bigint", nullable: true),
                    tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    minutos_justificados = table.Column<int>(type: "int", nullable: true),
                    estado_anterior = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ruta_documento_sustento = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    registrado_por_usuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    registrado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_justificacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_justificacion_solicitud_ausencia_solicitud_ausencia_id",
                        column: x => x.solicitud_ausencia_id,
                        principalTable: "solicitud_ausencia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "area",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    planta_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area", x => x.id);
                    table.ForeignKey(
                        name: "fk_area_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "turno_planta",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    turno_id = table.Column<long>(type: "bigint", nullable: false),
                    planta_id = table.Column<long>(type: "bigint", nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turno_planta", x => x.id);
                    table.ForeignKey(
                        name: "fk_turno_planta_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_turno_planta_turno_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seccion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    area_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "datetime2", nullable: false),
                    creado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seccion", x => x.id);
                    table.ForeignKey(
                        name: "fk_seccion_area_area_id",
                        column: x => x.area_id,
                        principalTable: "area",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "cliente",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "razon_social", "ruc" },
                values: new object[] { 1L, true, "INTRADEVCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Intradevco", null });

            migrationBuilder.InsertData(
                table: "tasa_sobretasa",
                columns: new[] { "id", "activo", "base_legal", "concepto", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por", "porcentaje", "vigente_desde", "vigente_hasta" },
                values: new object[,]
                {
                    { 1L, true, "D.S. 007-2002-TR, art. 10", "HoraExtraPrimerasDos", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, 25m, new DateOnly(2002, 8, 1), null },
                    { 2L, true, "D.S. 007-2002-TR, art. 10", "HoraExtraAdicionales", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, 35m, new DateOnly(2002, 8, 1), null },
                    { 3L, true, "D.S. 007-2002-TR, art. 8 (sobre la RMV)", "TrabajoNocturno", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, 35m, new DateOnly(2002, 8, 1), null },
                    { 4L, true, "D. Leg. 713, art. 3", "DescansoSemanalTrabajado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, 100m, new DateOnly(2002, 8, 1), null },
                    { 5L, true, "D. Leg. 713, art. 9", "FeriadoTrabajado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, 100m, new DateOnly(2002, 8, 1), null }
                });

            migrationBuilder.InsertData(
                table: "tipo_ausencia",
                columns: new[] { "id", "activo", "codigo", "comportamiento", "computa_como_asistencia", "creado_en", "creado_por", "es_por_horas", "modificado_en", "modificado_por", "nombre", "requiere_sustento" },
                values: new object[,]
                {
                    { 1L, true, "VAC", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Vacaciones", false },
                    { 2L, true, "DM", "Subsidiada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Descanso médico", true },
                    { 3L, true, "LCG", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Licencia con goce", false },
                    { 4L, true, "LSG", "JustificadaNoRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Licencia sin goce", false },
                    { 5L, true, "PER", "PermisoParcial", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, "Permiso", false },
                    { 6L, true, "LPAT", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Licencia paternal", true },
                    { 7L, true, "LMAT", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Licencia por maternidad", true },
                    { 8L, true, "SUBE", "Subsidiada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Subsidio por enfermedad", true },
                    { 9L, true, "SUBA", "Subsidiada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, "Subsidio por accidente", true }
                });

            migrationBuilder.InsertData(
                table: "tipo_documento",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "longitud_exacta", "modificado_en", "modificado_por", "nombre", "solo_digitos" },
                values: new object[,]
                {
                    { 1L, true, "DNI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 8, null, null, "Documento Nacional de Identidad", true },
                    { 2L, true, "CE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Carné de Extranjería", false },
                    { 3L, true, "PAS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Pasaporte", false },
                    { 4L, true, "PTP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Permiso Temporal de Permanencia", false }
                });

            migrationBuilder.InsertData(
                table: "turno",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "dias_aplicables", "hora_fin", "hora_inicio", "minutos_jornada_ordinaria", "minutos_refrigerio", "minutos_tolerancia", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, "DIU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "LunesASabado", new TimeOnly(19, 0, 0), new TimeOnly(7, 0, 0), 480, 60, 5, null, null, "Diurno" },
                    { 2L, true, "NOC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "LunesASabado", new TimeOnly(7, 0, 0), new TimeOnly(19, 0, 0), 480, 60, 5, null, null, "Nocturno" },
                    { 3L, true, "SAB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Sabado", new TimeOnly(15, 0, 0), new TimeOnly(7, 0, 0), 480, 45, 5, null, null, "Especial sábado" },
                    { 4L, true, "DOM1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Domingo", new TimeOnly(15, 0, 0), new TimeOnly(7, 0, 0), 480, 45, 5, null, null, "Dominical 1" },
                    { 5L, true, "DOM2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Domingo", new TimeOnly(19, 0, 0), new TimeOnly(7, 0, 0), 480, 60, 5, null, null, "Dominical 2" }
                });

            migrationBuilder.InsertData(
                table: "empresa",
                columns: new[] { "id", "activo", "cliente_id", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "razon_social", "ruc" },
                values: new object[,]
                {
                    { 1L, true, 1L, "CA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Consulting Advisers", "00000000001" },
                    { 2L, true, 1L, "CAO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "CA Outsourcing", "00000000002" }
                });

            migrationBuilder.InsertData(
                table: "planta",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, "DET", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Planta Detergentes" },
                    { 2L, true, "P20M", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Planta 20 mil" },
                    { 3L, true, "P01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "Planta 01" },
                    { 4L, true, "SULF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2L, null, null, "Planta Sulfonación" }
                });

            migrationBuilder.InsertData(
                table: "turno_planta",
                columns: new[] { "id", "activo", "creado_en", "creado_por", "modificado_en", "modificado_por", "planta_id", "turno_id" },
                values: new object[] { 1L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, 3L, 3L });

            migrationBuilder.CreateIndex(
                name: "ix_area_planta_id_codigo",
                table: "area",
                columns: new[] { "planta_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_marcador_id_marcador_fecha_inicio_fecha_fin",
                table: "asignacion_marcador",
                columns: new[] { "id_marcador", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_marcador_trabajador_id_fecha_inicio",
                table: "asignacion_marcador",
                columns: new[] { "trabajador_id", "fecha_inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_turno_trabajador_id_fecha_inicio_fecha_fin",
                table: "asignacion_turno",
                columns: new[] { "trabajador_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_turno_turno_id",
                table: "asignacion_turno",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "ix_cliente_codigo",
                table: "cliente",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empresa_cliente_id",
                table: "empresa",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_empresa_codigo",
                table: "empresa",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empresa_ruc",
                table: "empresa",
                column: "ruc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_error_importacion_importacion_id",
                table: "error_importacion",
                column: "importacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_feriado_fecha_planta_id",
                table: "feriado",
                columns: new[] { "fecha", "planta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_importacion_marcador_fecha_carga",
                table: "importacion_marcador",
                column: "fecha_carga");

            migrationBuilder.CreateIndex(
                name: "ix_importacion_marcador_hash_contenido",
                table: "importacion_marcador",
                column: "hash_contenido");

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_periodo_id",
                table: "justificacion",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_solicitud_ausencia_id",
                table: "justificacion",
                column: "solicitud_ausencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_justificacion_trabajador_id_fecha",
                table: "justificacion",
                columns: new[] { "trabajador_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_marcacion_cruda_fecha_hora",
                table: "marcacion_cruda",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "ix_marcacion_cruda_importacion_id",
                table: "marcacion_cruda",
                column: "importacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_marcacion_cruda_trabajador_id_fecha_hora",
                table: "marcacion_cruda",
                columns: new[] { "trabajador_id", "fecha_hora" });

            migrationBuilder.CreateIndex(
                name: "uq_marcacion_cruda_trabajador_fecha_marcador",
                table: "marcacion_cruda",
                columns: new[] { "trabajador_id", "fecha_hora", "id_marcador" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_periodo_codigo",
                table: "periodo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_periodo_fecha_inicio_fecha_fin",
                table: "periodo",
                columns: new[] { "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_planta_empresa_id_codigo",
                table: "planta",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_puesto_codigo",
                table: "puesto",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seccion_area_id_codigo",
                table: "seccion",
                columns: new[] { "area_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_ausencia_estado_fecha_inicio",
                table: "solicitud_ausencia",
                columns: new[] { "estado", "fecha_inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_ausencia_tipo_ausencia_id",
                table: "solicitud_ausencia",
                column: "tipo_ausencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_ausencia_trabajador_id_fecha_inicio_fecha_fin",
                table: "solicitud_ausencia",
                columns: new[] { "trabajador_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_tareo_diario_fecha",
                table: "tareo_diario",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_tareo_diario_periodo_id_estado",
                table: "tareo_diario",
                columns: new[] { "periodo_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "uq_tareo_diario_trabajador_fecha",
                table: "tareo_diario",
                columns: new[] { "trabajador_id", "fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasa_sobretasa_concepto_vigente_desde_vigente_hasta",
                table: "tasa_sobretasa",
                columns: new[] { "concepto", "vigente_desde", "vigente_hasta" });

            migrationBuilder.CreateIndex(
                name: "ix_tipo_ausencia_codigo",
                table: "tipo_ausencia",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tipo_documento_codigo",
                table: "tipo_documento",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trabajador_empresa_id_activo",
                table: "trabajador",
                columns: new[] { "empresa_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajador_planta_id_area_id_seccion_id",
                table: "trabajador",
                columns: new[] { "planta_id", "area_id", "seccion_id" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajador_tipo_documento_id_numero_documento",
                table: "trabajador",
                columns: new[] { "tipo_documento_id", "numero_documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turno_codigo",
                table: "turno",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turno_planta_planta_id",
                table: "turno_planta",
                column: "planta_id");

            migrationBuilder.CreateIndex(
                name: "ix_turno_planta_turno_id_planta_id",
                table: "turno_planta",
                columns: new[] { "turno_id", "planta_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignacion_marcador");

            migrationBuilder.DropTable(
                name: "asignacion_turno");

            migrationBuilder.DropTable(
                name: "error_importacion");

            migrationBuilder.DropTable(
                name: "feriado");

            migrationBuilder.DropTable(
                name: "justificacion");

            migrationBuilder.DropTable(
                name: "marcacion_cruda");

            migrationBuilder.DropTable(
                name: "periodo");

            migrationBuilder.DropTable(
                name: "puesto");

            migrationBuilder.DropTable(
                name: "seccion");

            migrationBuilder.DropTable(
                name: "tareo_diario");

            migrationBuilder.DropTable(
                name: "tasa_sobretasa");

            migrationBuilder.DropTable(
                name: "tipo_documento");

            migrationBuilder.DropTable(
                name: "turno_planta");

            migrationBuilder.DropTable(
                name: "trabajador");

            migrationBuilder.DropTable(
                name: "solicitud_ausencia");

            migrationBuilder.DropTable(
                name: "importacion_marcador");

            migrationBuilder.DropTable(
                name: "area");

            migrationBuilder.DropTable(
                name: "turno");

            migrationBuilder.DropTable(
                name: "tipo_ausencia");

            migrationBuilder.DropTable(
                name: "planta");

            migrationBuilder.DropTable(
                name: "empresa");

            migrationBuilder.DropTable(
                name: "cliente");
        }
    }
}

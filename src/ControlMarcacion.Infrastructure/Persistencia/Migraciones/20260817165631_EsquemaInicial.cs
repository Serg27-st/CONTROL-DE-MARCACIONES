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
                name: "asp_net_roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    nombre_completo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    apellido_paterno = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    apellido_materno = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    nombres = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    tipo_documento_id = table.Column<long>(type: "INTEGER", nullable: true),
                    numero_documento = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    security_stamp = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    phone_number = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    access_failed_count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cliente",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cliente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feriado",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    descripcion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feriado", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "importacion_marcador",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nombre_archivo = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    hash_contenido = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "TEXT", nullable: false),
                    cargado_por_usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    filas_leidas = table.Column<int>(type: "INTEGER", nullable: false),
                    filas_insertadas = table.Column<int>(type: "INTEGER", nullable: false),
                    filas_duplicadas = table.Column<int>(type: "INTEGER", nullable: false),
                    filas_rechazadas = table.Column<int>(type: "INTEGER", nullable: false),
                    anulada = table.Column<bool>(type: "INTEGER", nullable: false),
                    motivo_anulacion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    anulada_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    anulada_por_usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importacion_marcador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametro_legal",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tipo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    valor = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    base_legal = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametro_legal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "periodo",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    cerrado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    cerrado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    reabierto_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    reabierto_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    motivo_reapertura = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_periodo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "puesto",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_puesto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registro_auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tabla = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    clave_registro = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    operacion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    usuario = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    detalle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registro_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reintegro_tareo",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    periodo_id = table.Column<long>(type: "INTEGER", nullable: false),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    horas_normales = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_extra25 = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_extra35 = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_no_laborable = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    monto_movilidad = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_nocturnas = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_extra25_nocturnas = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_extra35_nocturnas = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    horas_no_laborable_nocturnas = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    viatico = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    dias_descanso_medico = table.Column<int>(type: "INTEGER", nullable: false),
                    dias_subsidio_maternidad = table.Column<int>(type: "INTEGER", nullable: false),
                    dias_subsidio_accidente = table.Column<int>(type: "INTEGER", nullable: false),
                    dias_licencia_paternidad = table.Column<int>(type: "INTEGER", nullable: false),
                    licencia_con_goce = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    reintegro_asignacion_familiar = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    reintegro_afecto = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    descuento_pago_indebido = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reintegro_tareo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tareo_diario",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    periodo_id = table.Column<long>(type: "INTEGER", nullable: false),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    turno_id = table.Column<long>(type: "INTEGER", nullable: true),
                    entrada_real = table.Column<DateTime>(type: "TEXT", nullable: true),
                    salida_real = table.Column<DateTime>(type: "TEXT", nullable: true),
                    minutos_trabajados = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_ordinarios = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_extra25 = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_extra35 = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_nocturnos = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_extra25_nocturnos = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_extra35_nocturnos = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_tardanza = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_salida_anticipada = table.Column<int>(type: "INTEGER", nullable: false),
                    tipo_dia_no_laborable = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    justificacion_id = table.Column<long>(type: "INTEGER", nullable: true),
                    observacion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    calculado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tareo_diario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasa_sobretasa",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    concepto = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    porcentaje = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    base_legal = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasa_sobretasa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_ausencia",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    comportamiento = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    computa_como_asistencia = table.Column<bool>(type: "INTEGER", nullable: false),
                    requiere_sustento = table.Column<bool>(type: "INTEGER", nullable: false),
                    es_por_horas = table.Column<bool>(type: "INTEGER", nullable: false),
                    maximo_dias_por_evento = table.Column<int>(type: "INTEGER", nullable: true),
                    maximo_dias_por_anio_calendario = table.Column<int>(type: "INTEGER", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_ausencia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_documento",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    longitud_exacta = table.Column<int>(type: "INTEGER", nullable: true),
                    longitud_maxima = table.Column<int>(type: "INTEGER", nullable: true),
                    solo_digitos = table.Column<bool>(type: "INTEGER", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_documento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trabajador",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tipo_documento_id = table.Column<long>(type: "INTEGER", nullable: false),
                    numero_documento = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    apellido_paterno = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    apellido_materno = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    nombres = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    sexo = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    telefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    direccion = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    correo_electronico = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    tiene_hijos = table.Column<bool>(type: "INTEGER", nullable: false),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: false),
                    unidad = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    tipo_planilla = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false),
                    area_id = table.Column<long>(type: "INTEGER", nullable: true),
                    seccion_id = table.Column<long>(type: "INTEGER", nullable: true),
                    fecha_ingreso = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_cese = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    motivo_cese = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    observacion_cese = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    puesto_id = table.Column<long>(type: "INTEGER", nullable: false),
                    centro_costo_id = table.Column<long>(type: "INTEGER", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turno",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    minutos_refrigerio = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_tolerancia = table.Column<int>(type: "INTEGER", nullable: false),
                    minutos_anticipacion_entrada = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 15),
                    minutos_anticipacion_salida = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    minutos_jornada_ordinaria = table.Column<int>(type: "INTEGER", nullable: false),
                    dias_aplicables = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turno", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    provider_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    provider_display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    user_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_roles",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    role_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_tokens",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    login_provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "empresa",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    cliente_id = table.Column<long>(type: "INTEGER", nullable: false),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    importacion_id = table.Column<long>(type: "INTEGER", nullable: false),
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    id_marcador = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    importacion_id = table.Column<long>(type: "INTEGER", nullable: false),
                    modo_verificacion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    tipo_marcacion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    linea_original = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    tipo_ausencia_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    hora_fin = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    subtipo_vacaciones = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    periodo_vacacional = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                    contingencia = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    diagnostico = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    centro_atencion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    sustento = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ruta_documento_sustento = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    observacion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    motivo_anulacion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    id_marcador = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "asignacion_sueldo",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    sueldo_basico_mensual = table.Column<decimal>(type: "TEXT", precision: 12, scale: 4, nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "asignacion_turno",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    turno_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "centro",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: false),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "periodo_laboral",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha_ingreso = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    fecha_cese = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    motivo_cese = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    observacion_cese = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "planta",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    empresa_id = table.Column<long>(type: "INTEGER", nullable: false),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    trabajador_id = table.Column<long>(type: "INTEGER", nullable: false),
                    periodo_id = table.Column<long>(type: "INTEGER", nullable: false),
                    fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    origen = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    solicitud_ausencia_id = table.Column<long>(type: "INTEGER", nullable: true),
                    tipo_ausencia_id = table.Column<long>(type: "INTEGER", nullable: true),
                    tipo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    minutos_justificados = table.Column<int>(type: "INTEGER", nullable: true),
                    estado_anterior = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    motivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ruta_documento_sustento = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    registrado_por_usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    registrado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "centro_costo",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    centro_id = table.Column<long>(type: "INTEGER", nullable: false),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "area",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "marcador",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("pk_marcador", x => x.id);
                    table.ForeignKey(
                        name: "fk_marcador_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "turno_planta",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    turno_id = table.Column<long>(type: "INTEGER", nullable: false),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "usuario_planta",
                columns: table => new
                {
                    usuario_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    planta_id = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_planta", x => new { x.usuario_id, x.planta_id });
                    table.ForeignKey(
                        name: "fk_usuario_planta_asp_net_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuario_planta_planta_planta_id",
                        column: x => x.planta_id,
                        principalTable: "planta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seccion",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    area_id = table.Column<long>(type: "INTEGER", nullable: false),
                    codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    creado_en = table.Column<DateTime>(type: "TEXT", nullable: false),
                    creado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    modificado_en = table.Column<DateTime>(type: "TEXT", nullable: true),
                    modificado_por = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    activo = table.Column<bool>(type: "INTEGER", nullable: false)
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
                table: "asp_net_roles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { "00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000001", "ADMINISTRADOR", "ADMINISTRADOR" },
                    { "00000000-0000-0000-0000-000000000005", "00000000-0000-0000-0000-000000000005", "COORDINADOR", "COORDINADOR" },
                    { "00000000-0000-0000-0000-000000000006", "00000000-0000-0000-0000-000000000006", "GERENCIA", "GERENCIA" },
                    { "00000000-0000-0000-0000-000000000007", "00000000-0000-0000-0000-000000000007", "TRABAJADOR_SOCIAL", "TRABAJADOR_SOCIAL" }
                });

            migrationBuilder.InsertData(
                table: "cliente",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "razon_social", "ruc" },
                values: new object[] { 1L, true, "INTRADEVCO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Intradevco", null });

            migrationBuilder.InsertData(
                table: "parametro_legal",
                columns: new[] { "id", "activo", "base_legal", "creado_en", "creado_por", "modificado_en", "modificado_por", "tipo", "valor", "vigente_desde", "vigente_hasta" },
                values: new object[,]
                {
                    { 1L, true, "D.S. 006-2024-TR", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "RemuneracionMinimaVital", 1130.00m, new DateOnly(2025, 1, 1), null },
                    { 2L, true, "Ley 25129", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PorcentajeAsignacionFamiliar", 10.00m, new DateOnly(2025, 1, 1), null },
                    { 3L, true, "Política interna GCA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "ValorMovilidadDiaria", 3.00m, new DateOnly(2025, 1, 1), null }
                });

            migrationBuilder.InsertData(
                table: "periodo",
                columns: new[] { "id", "activo", "cerrado_en", "cerrado_por", "codigo", "creado_en", "creado_por", "estado", "fecha_fin", "fecha_inicio", "modificado_en", "modificado_por", "motivo_reapertura", "nombre", "reabierto_en", "reabierto_por" },
                values: new object[] { 1L, true, null, null, "2026-07", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Abierto", new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 1), null, null, null, "Julio 2026", null, null });

            migrationBuilder.InsertData(
                table: "puesto",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "empresa_id", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, "OPER", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Operario" },
                    { 2L, true, "SUPV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Supervisor" },
                    { 3L, true, "AUX", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Auxiliar" },
                    { 4L, true, "ANAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, "Analista" },
                    { 10L, true, "26001", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operario de producción" },
                    { 11L, true, "26002", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operador transpalet" },
                    { 12L, true, "26006", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operario de máquina" },
                    { 13L, true, "26007", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Operario transpalet" },
                    { 14L, true, "26104", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Coordinador de producción" },
                    { 15L, true, "26148", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Inspector SSOMA" },
                    { 16L, true, "26216", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Supervisor" },
                    { 17L, true, "26223", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Supervisor de producción" },
                    { 18L, true, "26229", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1L, null, null, "Supervisor SSOMA" }
                });

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
                columns: new[] { "id", "activo", "codigo", "comportamiento", "computa_como_asistencia", "creado_en", "creado_por", "es_por_horas", "maximo_dias_por_anio_calendario", "maximo_dias_por_evento", "modificado_en", "modificado_por", "nombre", "requiere_sustento" },
                values: new object[,]
                {
                    { 1L, true, "VAC", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, null, null, "Vacaciones", false },
                    { 2L, true, "DM", "Subsidiada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, 20, null, null, null, "Descanso médico", true },
                    { 3L, true, "LCG", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, null, null, "Licencia con goce", false },
                    { 4L, true, "LSG", "JustificadaNoRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, null, null, "Licencia sin goce", false },
                    { 5L, true, "PER", "PermisoParcial", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, null, null, null, "Permiso", false },
                    { 6L, true, "LPAT", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, 10, null, null, "Licencia paternal", true },
                    { 7L, true, "LMAT", "JustificadaRemunerada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, 98, null, null, "Licencia por maternidad", true },
                    { 8L, true, "SUBE", "Subsidiada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, null, null, "Subsidio por enfermedad", true },
                    { 9L, true, "SUBA", "Subsidiada", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, null, null, null, null, "Subsidio por accidente", true }
                });

            migrationBuilder.InsertData(
                table: "tipo_documento",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "longitud_exacta", "longitud_maxima", "modificado_en", "modificado_por", "nombre", "solo_digitos" },
                values: new object[,]
                {
                    { 1L, true, "DNI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 8, null, null, null, "Documento Nacional de Identidad", true },
                    { 2L, true, "CE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 10, null, null, "Carné de Extranjería", false },
                    { 3L, true, "PAS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 10, null, null, "Pasaporte", false },
                    { 4L, true, "PTP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, 10, null, null, "Permiso Temporal de Permanencia", false }
                });

            migrationBuilder.InsertData(
                table: "turno",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "dias_aplicables", "hora_fin", "hora_inicio", "minutos_anticipacion_entrada", "minutos_anticipacion_salida", "minutos_jornada_ordinaria", "minutos_refrigerio", "minutos_tolerancia", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, "DIU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "LunesASabado", new TimeOnly(19, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 480, 0, 5, null, null, "Diurno" },
                    { 2L, true, "NOC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "LunesASabado", new TimeOnly(7, 0, 0), new TimeOnly(19, 0, 0), 15, 5, 480, 0, 5, null, null, "Nocturno" },
                    { 3L, true, "SAB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Sabado", new TimeOnly(15, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 480, 0, 5, null, null, "Especial sábado" },
                    { 4L, true, "DOM1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Domingo", new TimeOnly(15, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 480, 0, 5, null, null, "Dominical 1" },
                    { 5L, true, "DOM2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Domingo", new TimeOnly(19, 0, 0), new TimeOnly(7, 0, 0), 15, 5, 480, 0, 5, null, null, "Dominical 2" }
                });

            migrationBuilder.InsertData(
                table: "empresa",
                columns: new[] { "id", "activo", "cliente_id", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "razon_social", "ruc" },
                values: new object[,]
                {
                    { 1L, true, 1L, "CA", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "CONSULTING ADVISERS S.A.C", "00000000001" },
                    { 2L, true, 1L, "CAO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "CA Outsourcing", "00000000002" }
                });

            migrationBuilder.InsertData(
                table: "centro",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "descripcion", "empresa_id", "modificado_en", "modificado_por" },
                values: new object[,]
                {
                    { 1L, true, "066D", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PLANTA INTRACLORO", 2L, null, null },
                    { 2L, true, "066E", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "CENTRO LURÍN", 2L, null, null },
                    { 3L, true, "066F", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PLANTA SULFONACIÓN", 2L, null, null },
                    { 4L, true, "066G", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PLANTA DETERGENTES LURÍN", 1L, null, null },
                    { 5L, true, "066H", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "PLANTA INTRACAN/FEM", 1L, null, null }
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
                table: "area",
                columns: new[] { "id", "activo", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre", "planta_id" },
                values: new object[,]
                {
                    { 1L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 1L },
                    { 2L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 2L },
                    { 3L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 3L },
                    { 4L, true, "PROD", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "Producción", 4L },
                    { 10L, true, "ENV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "ENVASADO", 1L },
                    { 11L, true, "LAV", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "LAVAVAJILLAS", 1L },
                    { 12L, true, "PROC", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "PROCESOS", 1L },
                    { 13L, true, "SUPPRO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "SUP. PRODUCCION", 1L },
                    { 14L, true, "VOL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "VOLANTES", 1L },
                    { 15L, true, "INSSSO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "INSP. SSOMA", 1L },
                    { 16L, true, "CRRHH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "COORD. RRHH", 1L },
                    { 17L, true, "SUPSSO", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "SUP. SSOMA", 1L }
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

            migrationBuilder.InsertData(
                table: "turno_planta",
                columns: new[] { "id", "activo", "creado_en", "creado_por", "modificado_en", "modificado_por", "planta_id", "turno_id" },
                values: new object[] { 1L, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, 3L, 3L });

            migrationBuilder.InsertData(
                table: "seccion",
                columns: new[] { "id", "activo", "area_id", "codigo", "creado_en", "creado_por", "modificado_en", "modificado_por", "nombre" },
                values: new object[,]
                {
                    { 1L, true, 1L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" },
                    { 2L, true, 2L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" },
                    { 3L, true, 3L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" },
                    { 4L, true, 4L, "GEN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, "General" }
                });

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
                name: "uq_asignacion_marcador_abierta",
                table: "asignacion_marcador",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_sueldo_trabajador_id_fecha_inicio_fecha_fin",
                table: "asignacion_sueldo",
                columns: new[] { "trabajador_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_sueldo_abierta",
                table: "asignacion_sueldo",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_turno_trabajador_id_fecha_inicio_fecha_fin",
                table: "asignacion_turno",
                columns: new[] { "trabajador_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_turno_turno_id",
                table: "asignacion_turno",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "uq_asignacion_turno_abierta",
                table: "asignacion_turno",
                column: "trabajador_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "asp_net_role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "role_name_index",
                table: "asp_net_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "asp_net_user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "asp_net_user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "asp_net_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "email_index",
                table: "asp_net_users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "user_name_index",
                table: "asp_net_users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_centro_empresa_id_codigo",
                table: "centro",
                columns: new[] { "empresa_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_centro_costo_centro_id_codigo",
                table: "centro_costo",
                columns: new[] { "centro_id", "codigo" });

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
                name: "ix_importacion_marcador_anulada",
                table: "importacion_marcador",
                column: "anulada");

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
                name: "ix_marcacion_cruda_importacion_id_fecha_hora",
                table: "marcacion_cruda",
                columns: new[] { "importacion_id", "fecha_hora" });

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
                name: "ix_marcador_codigo",
                table: "marcador",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marcador_planta_id_activo",
                table: "marcador",
                columns: new[] { "planta_id", "activo" });

            migrationBuilder.CreateIndex(
                name: "ix_parametro_legal_tipo_vigente_desde_vigente_hasta",
                table: "parametro_legal",
                columns: new[] { "tipo", "vigente_desde", "vigente_hasta" });

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
                name: "ix_periodo_laboral_empresa_id",
                table: "periodo_laboral",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_periodo_laboral_trabajador_id_fecha_ingreso",
                table: "periodo_laboral",
                columns: new[] { "trabajador_id", "fecha_ingreso" });

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

            migrationBuilder.CreateIndex(
                name: "uq_reintegro_tareo_trabajador_periodo",
                table: "reintegro_tareo",
                columns: new[] { "trabajador_id", "periodo_id" },
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

            migrationBuilder.CreateIndex(
                name: "ix_usuario_planta_planta_id",
                table: "usuario_planta",
                column: "planta_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asignacion_marcador");

            migrationBuilder.DropTable(
                name: "asignacion_sueldo");

            migrationBuilder.DropTable(
                name: "asignacion_turno");

            migrationBuilder.DropTable(
                name: "asp_net_role_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_logins");

            migrationBuilder.DropTable(
                name: "asp_net_user_roles");

            migrationBuilder.DropTable(
                name: "asp_net_user_tokens");

            migrationBuilder.DropTable(
                name: "centro_costo");

            migrationBuilder.DropTable(
                name: "error_importacion");

            migrationBuilder.DropTable(
                name: "feriado");

            migrationBuilder.DropTable(
                name: "justificacion");

            migrationBuilder.DropTable(
                name: "marcacion_cruda");

            migrationBuilder.DropTable(
                name: "marcador");

            migrationBuilder.DropTable(
                name: "parametro_legal");

            migrationBuilder.DropTable(
                name: "periodo");

            migrationBuilder.DropTable(
                name: "periodo_laboral");

            migrationBuilder.DropTable(
                name: "puesto");

            migrationBuilder.DropTable(
                name: "registro_auditoria");

            migrationBuilder.DropTable(
                name: "reintegro_tareo");

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
                name: "usuario_planta");

            migrationBuilder.DropTable(
                name: "asp_net_roles");

            migrationBuilder.DropTable(
                name: "centro");

            migrationBuilder.DropTable(
                name: "solicitud_ausencia");

            migrationBuilder.DropTable(
                name: "importacion_marcador");

            migrationBuilder.DropTable(
                name: "trabajador");

            migrationBuilder.DropTable(
                name: "area");

            migrationBuilder.DropTable(
                name: "turno");

            migrationBuilder.DropTable(
                name: "asp_net_users");

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

# PHYSICAL_DATA_MODEL — modelo físico final

Fecha de corte: 2026-10-16  
Proveedor: SQLite / EF Core 10.0.10  
Alcance: modelo EF, configuraciones, 16 migraciones, snapshot y copia SQLite controlada.  
No se aplicó ninguna migración ni se modificó esquema.

## 1. Base auditada

| Dato | Resultado |
|---|---|
| Ruta temporal auditada | `C:\Users\stafur\AppData\Local\Temp\ControlMarcacion-ERD-20261016\ControlMarcacion.db` |
| Evidencia fuente | `C:\ProgramData\ControlMarcacion-evidencia-20261016-091047\data\ControlMarcacion.db` |
| Tamaño | 6,873,088 bytes |
| SHA-256 fuente/copia | `3448B2B2024F710BBCD0471D86F00F079F425E8CADC7C1FDE4105E7C93DC9B1C` |
| `PRAGMA integrity_check` | `ok` |
| `PRAGMA foreign_key_check` | 0 filas |

La copia temporal fue creada desde una evidencia local ya preservada; no se abrió ni escribió
una base productiva. Tras completar los PRAGMA y el inventario, la copia temporal fue eliminada
para no multiplicar datos sensibles; la evidencia fuente permanece preservada y verificable.

## 2. Inventario y convenciones

| Grupo | Tablas | Cantidad |
|---|---|---:|
| Dominio/aplicación central | organización, personal, turnos, marcaciones, tareo, solicitudes y parámetros | 37 |
| PMV | `prov_importacion`, `prov_marcacion`, `prov_error_importacion` | 3 |
| Auditoría | `registro_auditoria` | 1 |
| ASP.NET Core Identity | `asp_net_*` | 7 |
| EF internas | `__EFMigrationsHistory`, `__EFMigrationsLock` | 2 |
| **Total físico** |  | **50** |

Aplicación en sentido amplio: 41 tablas (37 centrales + 3 PMV + 1 auditoría). Total de
columnas: **578**. No existen vistas ni triggers en el modelo auditado.

Convenciones:

- nombres físicos `snake_case`;
- `INTEGER` para enteros y booleanos; `TEXT` para cadenas, fechas/horas, enums y decimales;
- PK de entidades de dominio: `id INTEGER` autoincremental;
- `BASE` significa exactamente: `creado_en TEXT NN`, `creado_por TEXT NULL`,
  `modificado_en TEXT NULL`, `modificado_por TEXT NULL`, `activo INTEGER NN`;
- `NN` = `NOT NULL`; `D=` = default físico; `FK→` = FK materializada;
- una columna `_id` sin `FK→` no tiene constraint referencial.

## 3. Tabla, entidad y propósito

| Tabla física | Entidad/modelo | Grupo | Propósito |
|---|---|---|---|
| `__EFMigrationsHistory` | EF Core | EF interna | Historial de migraciones aplicadas |
| `__EFMigrationsLock` | EF Core | EF interna | Lock técnico de migración SQLite |
| `cliente` | Cliente | Organización | Cliente contractual |
| `empresa` | Empresa | Organización | Empresa/razón social del cliente |
| `planta` | Planta | Organización | Planta de una empresa |
| `area` | Area | Organización | Área actual de planta |
| `seccion` | Seccion | Organización | Sección actual de área |
| `centro` | Centro | Organización | Centro organizativo/contable |
| `centro_costo` | CentroCosto | Organización | Centro de costo |
| `tipo_documento` | TipoDocumento | Catálogo | Tipo y reglas de documento |
| `puesto` | Puesto | Catálogo | Puesto laboral |
| `feriado` | Feriado | Calendario | Feriado nacional (`planta_id` null) o de planta |
| `equipo_marcador` | EquipoMarcador | Marcaciones | Aparato físico e identidad de origen |
| `marcador` | Marcador | Marcaciones | Catálogo actual de códigos por equipo |
| `turno` | Turno | Turnos | Identidad y proyección vigente del horario |
| `version_turno` | VersionTurno | Turnos | Horario histórico por vigencia |
| `turno_planta` | TurnoPlanta | Turnos | Habilitación N:M turno/planta |
| `cuadrilla` | Cuadrilla | Rotaciones | Cuadrilla de una planta |
| `programacion_cuadrilla` | ProgramacionCuadrilla | Rotaciones | Secuencia finita con fecha ancla |
| `tramo_rotacion` | TramoRotacion | Rotaciones | Tramo Trabajo/Descanso de la secuencia |
| `periodo` | Periodo | Tareo | Periodo operativo y cierre |
| `tipo_ausencia` | TipoAusencia | Solicitudes | Catálogo de ausencia/licencia |
| `trabajador` | Trabajador | Personal | Ficha y proyección actual |
| `periodo_laboral` | PeriodoLaboral | Personal | Autoridad histórica del vínculo |
| `asignacion_planta` | AsignacionPlanta | Personal | Autoridad histórica de planta |
| `asignacion_marcador` | AsignacionMarcador | Personal | Dueño temporal de equipo+código |
| `asignacion_area` | AsignacionArea | Personal | Área por vigencia |
| `asignacion_turno` | AsignacionTurno | Personal | Turno directo o cuadrilla por vigencia |
| `asignacion_sueldo` | AsignacionSueldo | Personal | Sueldo por vigencia |
| `importacion_marcador` | ImportacionMarcador | Marcaciones | Cabecera de importación oficial |
| `error_importacion` | ErrorImportacion | Marcaciones | Rechazo funcional de línea |
| `marcacion_cruda` | MarcacionCruda | Marcaciones | Marca física inmutable, sin trabajador autoritativo |
| `solicitud_ausencia` | SolicitudAusencia | Solicitudes | Permiso/licencia/ausencia por rango |
| `justificacion` | Justificacion | Solicitudes | Resolución diaria, incidencia o regularización |
| `tareo_diario` | TareoDiario | Tareo | Proyección diaria recalculable |
| `reintegro_tareo` | ReintegroTareo | Tareo | Ajuste manual por trabajador/periodo |
| `parametro_legal` | ParametroLegal | Parámetros | Parámetro normativo por vigencia |
| `tasa_sobretasa` | TasaSobretasa | Parámetros | Tasa económica por vigencia |
| `prov_importacion` | ImportacionProvisional | PMV | Cabecera provisional aislada |
| `prov_marcacion` | MarcacionProvisional | PMV | Marca provisional aislada |
| `prov_error_importacion` | ErrorImportacionProvisional | PMV | Error/rechazo provisional |
| `registro_auditoria` | RegistroAuditoria | Auditoría | Bitácora de cambios; distinta de logging |
| `asp_net_users` | ApplicationUser | Identity | Cuenta y campos propios de seguridad |
| `asp_net_roles` | IdentityRole | Identity | Rol |
| `asp_net_role_claims` | IdentityRoleClaim | Identity | Claim de rol |
| `asp_net_user_claims` | IdentityUserClaim | Identity | Claim de usuario |
| `asp_net_user_logins` | IdentityUserLogin | Identity | Login externo |
| `asp_net_user_roles` | IdentityUserRole | Identity | Puente usuario/rol |
| `asp_net_user_tokens` | IdentityUserToken | Identity | Token de usuario |
| `usuario_planta` | UsuarioPlanta | Seguridad | Alcance N:M usuario/planta |

## 4. Catálogo completo de columnas

Las listas siguientes contienen todas las columnas físicas. Los defaults se muestran solo
cuando existen en SQLite.

### EF internas e Identity

```text
__EFMigrationsHistory:
  MigrationId TEXT PK NN; ProductVersion TEXT NN

__EFMigrationsLock:
  Id INTEGER PK NN; Timestamp TEXT NN

asp_net_roles:
  id TEXT PK NN; name TEXT NULL; normalized_name TEXT NULL; concurrency_stamp TEXT NULL

asp_net_role_claims:
  id INTEGER PK NN; role_id TEXT NN FK→asp_net_roles.id;
  claim_type TEXT NULL; claim_value TEXT NULL

asp_net_users:
  id TEXT PK NN; nombre_completo TEXT NN; apellido_paterno TEXT NULL;
  apellido_materno TEXT NULL; nombres TEXT NULL; tipo_documento_id INTEGER NULL;
  numero_documento TEXT NULL; empresa_id INTEGER NULL; activo INTEGER NN;
  user_name TEXT NULL; normalized_user_name TEXT NULL; email TEXT NULL;
  normalized_email TEXT NULL; email_confirmed INTEGER NN; password_hash TEXT NULL;
  security_stamp TEXT NULL; concurrency_stamp TEXT NULL; phone_number TEXT NULL;
  phone_number_confirmed INTEGER NN; two_factor_enabled INTEGER NN;
  lockout_end TEXT NULL; lockout_enabled INTEGER NN; access_failed_count INTEGER NN

asp_net_user_claims:
  id INTEGER PK NN; user_id TEXT NN FK→asp_net_users.id;
  claim_type TEXT NULL; claim_value TEXT NULL

asp_net_user_logins:
  login_provider TEXT PK NN; provider_key TEXT PK NN; provider_display_name TEXT NULL;
  user_id TEXT NN FK→asp_net_users.id

asp_net_user_roles:
  user_id TEXT PK NN FK→asp_net_users.id; role_id TEXT PK NN FK→asp_net_roles.id

asp_net_user_tokens:
  user_id TEXT PK NN FK→asp_net_users.id; login_provider TEXT PK NN;
  name TEXT PK NN; value TEXT NULL

usuario_planta:
  usuario_id TEXT PK NN FK→asp_net_users.id; planta_id INTEGER PK NN FK→planta.id
```

### Organización y catálogos

```text
cliente:
  id INTEGER PK NN; codigo TEXT NN; razon_social TEXT NN; ruc TEXT NULL; +BASE

empresa:
  id INTEGER PK NN; cliente_id INTEGER NN FK→cliente.id; codigo TEXT NN;
  razon_social TEXT NN; ruc TEXT NN; +BASE

planta:
  id INTEGER PK NN; empresa_id INTEGER NN FK→empresa.id; codigo TEXT NN;
  nombre TEXT NN; +BASE

area:
  id INTEGER PK NN; planta_id INTEGER NN FK→planta.id; codigo TEXT NN;
  nombre TEXT NN; +BASE

seccion:
  id INTEGER PK NN; area_id INTEGER NN FK→area.id; codigo TEXT NN;
  nombre TEXT NN; +BASE

centro:
  id INTEGER PK NN; codigo TEXT NN; descripcion TEXT NULL;
  empresa_id INTEGER NN FK→empresa.id; planta_id INTEGER NULL FK→planta.id; +BASE

centro_costo:
  id INTEGER PK NN; centro_id INTEGER NN FK→centro.id; codigo TEXT NN;
  descripcion TEXT NN; +BASE

tipo_documento:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN; longitud_exacta INTEGER NULL;
  longitud_maxima INTEGER NULL; solo_digitos INTEGER NN; +BASE

puesto:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN; empresa_id INTEGER NULL sin FK; +BASE

feriado:
  id INTEGER PK NN; fecha TEXT NN; descripcion TEXT NN;
  planta_id INTEGER NULL sin FK; +BASE

equipo_marcador:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN; serie TEXT NULL;
  planta_id INTEGER NN FK→planta.id; descripcion TEXT NULL; +BASE

marcador:
  id INTEGER PK NN; codigo TEXT NN; descripcion TEXT NULL;
  equipo_id INTEGER NN FK→equipo_marcador.id; planta_id INTEGER NN FK→planta.id; +BASE

tipo_ausencia:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN; comportamiento TEXT NN;
  computa_como_asistencia INTEGER NN; requiere_sustento INTEGER NN;
  es_por_horas INTEGER NN; maximo_dias_por_evento INTEGER NULL;
  maximo_dias_por_anio_calendario INTEGER NULL; +BASE

parametro_legal:
  id INTEGER PK NN; tipo TEXT NN; valor TEXT NN; vigente_desde TEXT NN;
  vigente_hasta TEXT NULL; base_legal TEXT NULL; +BASE

tasa_sobretasa:
  id INTEGER PK NN; concepto TEXT NN; porcentaje TEXT NN; vigente_desde TEXT NN;
  vigente_hasta TEXT NULL; base_legal TEXT NULL; empresa_id INTEGER NULL sin FK; +BASE
```

### Personal e historia

```text
trabajador:
  id INTEGER PK NN; tipo_documento_id INTEGER NN sin FK; numero_documento TEXT NN;
  apellido_paterno TEXT NN; apellido_materno TEXT NN; nombres TEXT NN; sexo TEXT NN;
  telefono TEXT NULL; direccion TEXT NULL; correo_electronico TEXT NULL;
  tiene_hijos INTEGER NN; empresa_id INTEGER NN sin FK; unidad TEXT NULL;
  tipo_planilla TEXT NN; planta_id INTEGER NN sin FK; area_id INTEGER NULL sin FK;
  seccion_id INTEGER NULL sin FK; fecha_ingreso TEXT NN; fecha_cese TEXT NULL;
  motivo_cese TEXT NULL; observacion_cese TEXT NULL; puesto_id INTEGER NN sin FK;
  centro_costo_id INTEGER NULL sin FK; +BASE

periodo_laboral:
  id INTEGER PK NN; trabajador_id INTEGER NN FK→trabajador.id;
  empresa_id INTEGER NN FK→empresa.id; fecha_ingreso TEXT NN; fecha_cese TEXT NULL;
  motivo_cese TEXT NULL; observacion_cese TEXT NULL; +BASE

asignacion_planta:
  id INTEGER PK NN; periodo_laboral_id INTEGER NN FK→periodo_laboral.id;
  planta_id INTEGER NN FK→planta.id; fecha_inicio TEXT NN; fecha_fin TEXT NULL; +BASE

asignacion_area:
  id INTEGER PK NN; trabajador_id INTEGER NN FK→trabajador.id;
  area_id INTEGER NN FK→area.id; fecha_inicio TEXT NN; fecha_fin TEXT NULL; +BASE

asignacion_marcador:
  id INTEGER PK NN; trabajador_id INTEGER NN FK→trabajador.id;
  equipo_id INTEGER NN FK→equipo_marcador.id; id_marcador TEXT NN;
  fecha_inicio TEXT NN; fecha_fin TEXT NULL; +BASE

asignacion_turno:
  id INTEGER PK NN; trabajador_id INTEGER NN FK→trabajador.id;
  turno_id INTEGER NULL FK→turno.id; cuadrilla_id INTEGER NULL FK→cuadrilla.id;
  fecha_inicio TEXT NN; fecha_fin TEXT NULL; +BASE

asignacion_sueldo:
  id INTEGER PK NN; trabajador_id INTEGER NN FK→trabajador.id;
  sueldo_basico_mensual TEXT NN; fecha_inicio TEXT NN; fecha_fin TEXT NULL; +BASE
```

### Turnos, cuadrillas y rotaciones

```text
turno:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN; hora_inicio TEXT NN;
  hora_fin TEXT NN; minutos_refrigerio INTEGER NN; minutos_tolerancia INTEGER NN;
  minutos_anticipacion_entrada INTEGER NN D=15; minutos_anticipacion_salida INTEGER NN D=5;
  minutos_jornada_ordinaria INTEGER NN; dias_aplicables TEXT NN;
  minutos_gracia_salida INTEGER NN D=0; +BASE

version_turno:
  id INTEGER PK NN; turno_id INTEGER NN FK→turno.id; fecha_inicio TEXT NN;
  fecha_fin TEXT NULL; hora_inicio TEXT NN; hora_fin TEXT NN;
  minutos_refrigerio INTEGER NN; minutos_tolerancia INTEGER NN;
  minutos_anticipacion_entrada INTEGER NN; minutos_anticipacion_salida INTEGER NN;
  minutos_gracia_salida INTEGER NN; minutos_jornada_ordinaria INTEGER NN;
  dias_aplicables TEXT NN; +BASE

turno_planta:
  id INTEGER PK NN; turno_id INTEGER NN FK→turno.id;
  planta_id INTEGER NN FK→planta.id; +BASE

cuadrilla:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN;
  planta_id INTEGER NN FK→planta.id; +BASE

programacion_cuadrilla:
  id INTEGER PK NN; cuadrilla_id INTEGER NN FK→cuadrilla.id;
  fecha_inicio TEXT NN; fecha_fin TEXT NULL; fecha_ancla TEXT NN; +BASE

tramo_rotacion:
  id INTEGER PK NN; programacion_id INTEGER NN FK→programacion_cuadrilla.id;
  turno_id INTEGER NULL FK→turno.id; orden INTEGER NN; tipo TEXT NN; dias INTEGER NN; +BASE
```

No existe repetición implícita infinita. La programación materializa una secuencia finita; el
dominio rechaza fechas anteriores al ancla o posteriores a la suma de días de sus tramos.

### Marcaciones oficiales

```text
importacion_marcador:
  id INTEGER PK NN; equipo_id INTEGER NN FK→equipo_marcador.id;
  nombre_archivo TEXT NN; hash_contenido TEXT NN; serie_archivo TEXT NULL;
  origen_verificado TEXT NN D='NoVerificado'; fecha_carga TEXT NN;
  cargado_por_usuario TEXT NN; filas_leidas INTEGER NN; filas_insertadas INTEGER NN;
  filas_duplicadas INTEGER NN; filas_rechazadas INTEGER NN; anulada INTEGER NN;
  anulada_en TEXT NULL; anulada_por_usuario TEXT NULL; motivo_anulacion TEXT NULL; +BASE

error_importacion:
  id INTEGER PK NN; importacion_id INTEGER NN FK→importacion_marcador.id;
  numero_linea INTEGER NN; contenido_linea TEXT NULL; motivo TEXT NN; +BASE

marcacion_cruda:
  id INTEGER PK NN; equipo_id INTEGER NN FK→equipo_marcador.id;
  importacion_id INTEGER NN FK→importacion_marcador.id; id_marcador TEXT NN;
  fecha_hora TEXT NN; linea_original TEXT NULL; modo_verificacion TEXT NULL;
  tipo_marcacion TEXT NULL; +BASE
```

`marcacion_cruda` no contiene `trabajador_id`. Su dueño se resuelve temporalmente por equipo,
identificador y fecha contra `asignacion_marcador`.

### PMV

```text
prov_importacion:
  id INTEGER PK NN; nombre_archivo TEXT NN;
  equipo_id INTEGER NN FK→equipo_marcador.id; hash_contenido TEXT NN;
  fecha_carga TEXT NN; cargado_por_usuario TEXT NN; filas_leidas INTEGER NN;
  filas_insertadas INTEGER NN; filas_rechazadas INTEGER NN; +BASE

prov_marcacion:
  id INTEGER PK NN; equipo_id INTEGER NN FK→equipo_marcador.id;
  importacion_provisional_id INTEGER NN FK→prov_importacion.id;
  id_marcador TEXT NN; fecha_hora TEXT NN; modo_verificacion TEXT NULL;
  linea_original TEXT NULL; +BASE

prov_error_importacion:
  id INTEGER PK NN; importacion_provisional_id INTEGER NN FK→prov_importacion.id;
  numero_linea INTEGER NN; contenido_linea TEXT NULL; motivo TEXT NN; +BASE
```

PMV no alimenta `marcacion_cruda`, tareo oficial ni planilla. Su atribución a trabajador se
calcula en consulta y no se almacena.

### Solicitudes, justificaciones, tareo y auditoría

```text
solicitud_ausencia:
  id INTEGER PK NN; trabajador_id INTEGER NN sin FK;
  tipo_ausencia_id INTEGER NN FK→tipo_ausencia.id; fecha_inicio TEXT NN;
  fecha_fin TEXT NN; hora_inicio TEXT NULL; hora_fin TEXT NULL; estado TEXT NN;
  subtipo_vacaciones TEXT NULL; periodo_vacacional TEXT NULL; contingencia TEXT NULL;
  diagnostico TEXT NULL; centro_atencion TEXT NULL; sustento TEXT NULL;
  ruta_documento_sustento TEXT NULL; observacion TEXT NULL;
  motivo_anulacion TEXT NULL; +BASE

justificacion:
  id INTEGER PK NN; trabajador_id INTEGER NN sin FK; periodo_id INTEGER NN sin FK;
  fecha TEXT NN; origen TEXT NN; solicitud_ausencia_id INTEGER NULL FK→solicitud_ausencia.id;
  tipo_ausencia_id INTEGER NULL sin FK; tipo TEXT NN; minutos_justificados INTEGER NULL;
  estado_anterior TEXT NULL; motivo TEXT NN; ruta_documento_sustento TEXT NULL;
  registrado_por_usuario TEXT NN; registrado_en TEXT NN; entrada_regularizada TEXT NULL;
  salida_regularizada TEXT NULL; estado TEXT NN D='Aprobada'; motivo_resolucion TEXT NULL;
  resuelta_en TEXT NULL; resuelta_por_usuario TEXT NULL; tipo_incidencia TEXT NULL;
  version INTEGER NN D=0; +BASE

periodo:
  id INTEGER PK NN; codigo TEXT NN; nombre TEXT NN; fecha_inicio TEXT NN;
  fecha_fin TEXT NN; estado TEXT NN; cerrado_en TEXT NULL; cerrado_por TEXT NULL;
  reabierto_en TEXT NULL; reabierto_por TEXT NULL; motivo_reapertura TEXT NULL;
  version INTEGER NN D=0; +BASE

tareo_diario:
  id INTEGER PK NN; periodo_id INTEGER NN sin FK; trabajador_id INTEGER NN sin FK;
  fecha TEXT NN; turno_id INTEGER NULL sin FK; entrada_real TEXT NULL; salida_real TEXT NULL;
  minutos_trabajados INTEGER NN; minutos_ordinarios INTEGER NN; minutos_extra25 INTEGER NN;
  minutos_extra35 INTEGER NN; minutos_nocturnos INTEGER NN;
  minutos_extra25_nocturnos INTEGER NN; minutos_extra35_nocturnos INTEGER NN;
  minutos_tardanza INTEGER NN; minutos_salida_anticipada INTEGER NN;
  tipo_dia_no_laborable TEXT NN; estado TEXT NN; justificacion_id INTEGER NULL sin FK;
  observacion TEXT NULL; calculado_en TEXT NN; marcaciones_fuera_ventana INTEGER NN D=0;
  requiere_validacion INTEGER NN D=0; tipo_incidencia TEXT NULL;
  coincidencia_feriado_descanso TEXT NN D='Ninguna';
  es_descanso_programado INTEGER NN D=0; +BASE

reintegro_tareo:
  id INTEGER PK NN; periodo_id INTEGER NN sin FK; trabajador_id INTEGER NN sin FK;
  horas_normales TEXT NN; horas_extra25 TEXT NN; horas_extra35 TEXT NN;
  horas_no_laborable TEXT NN; monto_movilidad TEXT NN; horas_nocturnas TEXT NN;
  horas_extra25_nocturnas TEXT NN; horas_extra35_nocturnas TEXT NN;
  horas_no_laborable_nocturnas TEXT NN; viatico TEXT NN;
  dias_descanso_medico INTEGER NN; dias_subsidio_maternidad INTEGER NN;
  dias_subsidio_accidente INTEGER NN; dias_licencia_paternidad INTEGER NN;
  licencia_con_goce TEXT NN; reintegro_asignacion_familiar TEXT NN;
  reintegro_afecto TEXT NN; descuento_pago_indebido TEXT NN; +BASE

registro_auditoria:
  id INTEGER PK NN; tabla TEXT NN; clave_registro TEXT NN; operacion TEXT NN;
  usuario TEXT NN; fecha_hora TEXT NN; detalle TEXT NN
```

`registro_auditoria` es la bitácora estructurada de entidades. Los archivos de logging de la
aplicación son otra fuente y no forman parte del esquema relacional.

## 5. Índices físicos

SQLite informa **98 índices**: 91 creados por el modelo/migraciones (`origin=c`) y 7
autoíndices de PK (`origin=pk`). Hay **41 únicos** en total (34 declarados + 7 de PK) y **11
parciales**. `U` = unique; `P` = parcial. Los filtros parciales se copian literalmente.

```text
__EFMigrationsHistory:
  sqlite_autoindex___EFMigrationsHistory_1(MigrationId) [U, PK]
area:
  ix_area_planta_id_codigo(planta_id,codigo) [U]
asignacion_area:
  uq_asignacion_area_abierta(trabajador_id) [U,P: fecha_fin IS NULL]
  ix_asignacion_area_trabajador_id_fecha_inicio_fecha_fin(trabajador_id,fecha_inicio,fecha_fin)
  ix_asignacion_area_area_id(area_id)
asignacion_marcador:
  uq_asignacion_marcador_equipo_codigo_abierta(equipo_id,id_marcador) [U,P: fecha_fin IS NULL]
  uq_asignacion_marcador_abierta(trabajador_id) [U,P: fecha_fin IS NULL]
  ix_asignacion_marcador_trabajador_id_fecha_inicio(trabajador_id,fecha_inicio)
  ix_asignacion_marcador_equipo_id_id_marcador_fecha_inicio_fecha_fin(equipo_id,id_marcador,fecha_inicio,fecha_fin)
asignacion_planta:
  uq_asignacion_planta_abierta(periodo_laboral_id) [U,P: fecha_fin IS NULL]
  ix_asignacion_planta_planta_id(planta_id)
  ix_asignacion_planta_periodo_laboral_id_fecha_inicio_fecha_fin(periodo_laboral_id,fecha_inicio,fecha_fin)
asignacion_sueldo:
  uq_asignacion_sueldo_abierta(trabajador_id) [U,P: fecha_fin IS NULL]
  ix_asignacion_sueldo_trabajador_id_fecha_inicio_fecha_fin(trabajador_id,fecha_inicio,fecha_fin)
asignacion_turno:
  uq_asignacion_turno_abierta(trabajador_id) [U,P: fecha_fin IS NULL]
  ix_asignacion_turno_turno_id(turno_id)
  ix_asignacion_turno_trabajador_id_fecha_inicio_fecha_fin(trabajador_id,fecha_inicio,fecha_fin)
  ix_asignacion_turno_cuadrilla_id(cuadrilla_id)
asp_net_role_claims:
  ix_asp_net_role_claims_role_id(role_id)
asp_net_roles:
  role_name_index(normalized_name) [U]
  sqlite_autoindex_asp_net_roles_1(id) [U,PK]
asp_net_user_claims:
  ix_asp_net_user_claims_user_id(user_id)
asp_net_user_logins:
  ix_asp_net_user_logins_user_id(user_id)
  sqlite_autoindex_asp_net_user_logins_1(login_provider,provider_key) [U,PK]
asp_net_user_roles:
  ix_asp_net_user_roles_role_id(role_id)
  sqlite_autoindex_asp_net_user_roles_1(user_id,role_id) [U,PK]
asp_net_user_tokens:
  sqlite_autoindex_asp_net_user_tokens_1(user_id,login_provider,name) [U,PK]
asp_net_users:
  user_name_index(normalized_user_name) [U]
  email_index(normalized_email)
  sqlite_autoindex_asp_net_users_1(id) [U,PK]
centro:
  ix_centro_planta_id(planta_id)
  ix_centro_empresa_id_codigo(empresa_id,codigo) [U]
centro_costo:
  ix_centro_costo_centro_id_codigo(centro_id,codigo)
cliente:
  ix_cliente_codigo(codigo) [U]
cuadrilla:
  ix_cuadrilla_planta_id_codigo(planta_id,codigo) [U]
empresa:
  ix_empresa_ruc(ruc) [U]
  ix_empresa_codigo(codigo) [U]
  ix_empresa_cliente_id(cliente_id)
equipo_marcador:
  uq_equipo_marcador_serie(serie) [U,P: serie IS NOT NULL]
  ix_equipo_marcador_planta_id_activo(planta_id,activo)
  ix_equipo_marcador_codigo(codigo) [U]
error_importacion:
  ix_error_importacion_importacion_id(importacion_id)
feriado:
  ix_feriado_fecha_planta_id(fecha,planta_id)
importacion_marcador:
  ix_importacion_marcador_hash_contenido(hash_contenido)
  ix_importacion_marcador_fecha_carga(fecha_carga)
  ix_importacion_marcador_equipo_id_fecha_carga(equipo_id,fecha_carga)
  ix_importacion_marcador_anulada(anulada)
justificacion:
  ix_justificacion_trabajador_id_fecha(trabajador_id,fecha) [U,P: estado NOT IN ('Rechazada','Cancelada')]
  ix_justificacion_estado(estado)
  ix_justificacion_solicitud_ausencia_id(solicitud_ausencia_id)
  ix_justificacion_periodo_id(periodo_id)
marcacion_cruda:
  uq_marcacion_cruda_equipo_marcador_fecha(equipo_id,id_marcador,fecha_hora) [U]
  ix_marcacion_cruda_importacion_id_fecha_hora(importacion_id,fecha_hora)
  ix_marcacion_cruda_fecha_hora(fecha_hora)
marcador:
  uq_marcador_equipo_codigo(equipo_id,codigo) [U]
  ix_marcador_planta_id_activo(planta_id,activo)
parametro_legal:
  ix_parametro_legal_tipo_vigente_desde_vigente_hasta(tipo,vigente_desde,vigente_hasta)
periodo:
  ix_periodo_fecha_inicio_fecha_fin(fecha_inicio,fecha_fin)
  ix_periodo_codigo(codigo) [U]
periodo_laboral:
  uq_periodo_laboral_abierto(trabajador_id) [U,P: fecha_cese IS NULL]
  ix_periodo_laboral_trabajador_id_fecha_ingreso(trabajador_id,fecha_ingreso)
  ix_periodo_laboral_empresa_id(empresa_id)
planta:
  ix_planta_empresa_id_codigo(empresa_id,codigo) [U]
programacion_cuadrilla:
  ix_programacion_cuadrilla_cuadrilla_id_fecha_inicio_fecha_fin(cuadrilla_id,fecha_inicio,fecha_fin)
prov_error_importacion:
  ix_prov_error_importacion(importacion_provisional_id)
prov_importacion:
  ix_prov_importacion_hash(hash_contenido)
  ix_prov_importacion_equipo_fecha(equipo_id,fecha_carga)
prov_marcacion:
  uq_prov_marcacion_equipo_marcador_fecha(equipo_id,id_marcador,fecha_hora) [U]
  ix_prov_marcacion_importacion(importacion_provisional_id)
  ix_prov_marcacion_fecha(fecha_hora)
puesto:
  ix_puesto_codigo(codigo) [U]
registro_auditoria:
  ix_registro_auditoria_usuario_fecha_hora(usuario,fecha_hora)
  ix_registro_auditoria_tabla_clave_registro_fecha_hora(tabla,clave_registro,fecha_hora)
  ix_registro_auditoria_fecha_hora(fecha_hora DESC)
reintegro_tareo:
  uq_reintegro_tareo_trabajador_periodo(trabajador_id,periodo_id) [U]
seccion:
  ix_seccion_area_id_codigo(area_id,codigo) [U]
solicitud_ausencia:
  ix_solicitud_ausencia_trabajador_id_fecha_inicio_fecha_fin(trabajador_id,fecha_inicio,fecha_fin)
  ix_solicitud_ausencia_tipo_ausencia_id(tipo_ausencia_id)
  ix_solicitud_ausencia_estado_fecha_inicio(estado,fecha_inicio)
tareo_diario:
  ix_tareo_diario_pendientes_de_validacion(periodo_id,requiere_validacion) [P: requiere_validacion=1]
  uq_tareo_diario_trabajador_fecha(trabajador_id,fecha) [U]
  ix_tareo_diario_periodo_id_estado(periodo_id,estado)
  ix_tareo_diario_fecha(fecha)
tasa_sobretasa:
  ix_tasa_sobretasa_concepto_vigente_desde_vigente_hasta(concepto,vigente_desde,vigente_hasta)
tipo_ausencia:
  ix_tipo_ausencia_codigo(codigo) [U]
tipo_documento:
  ix_tipo_documento_codigo(codigo) [U]
trabajador:
  ix_trabajador_tipo_documento_id_numero_documento(tipo_documento_id,numero_documento) [U]
  ix_trabajador_planta_id_area_id_seccion_id(planta_id,area_id,seccion_id)
  ix_trabajador_empresa_id_activo(empresa_id,activo)
tramo_rotacion:
  ix_tramo_rotacion_turno_id(turno_id)
  ix_tramo_rotacion_programacion_id_orden(programacion_id,orden) [U]
turno:
  ix_turno_codigo(codigo) [U]
turno_planta:
  ix_turno_planta_turno_id_planta_id(turno_id,planta_id) [U]
  ix_turno_planta_planta_id(planta_id)
usuario_planta:
  ix_usuario_planta_planta_id(planta_id)
  sqlite_autoindex_usuario_planta_1(usuario_id,planta_id) [U,PK]
version_turno:
  uq_version_turno_abierta(turno_id) [U,P: fecha_fin IS NULL AND activo=1]
  ix_version_turno_turno_id_fecha_inicio(turno_id,fecha_inicio)
```

`__EFMigrationsLock` no tiene índice. Las tablas no citadas en este apartado tampoco tienen
índice físico adicional a su PK `INTEGER` incorporada en la propia tabla.

## 6. Check constraints e invariantes de vigencia

Se encontraron **6 checks** físicos:

| Tabla | Constraint | Expresión |
|---|---|---|
| asignacion_planta | `CK_asignacion_planta_fechas` | `fecha_fin IS NULL OR fecha_fin >= fecha_inicio` |
| asignacion_turno | `CK_asignacion_turno_turno_o_cuadrilla` | `(turno_id IS NULL) <> (cuadrilla_id IS NULL)` |
| tramo_rotacion | `CK_tramo_rotacion_descanso_un_dia` | `tipo <> 'Descanso' OR dias = 1` |
| tramo_rotacion | `CK_tramo_rotacion_dias_positivos` | `dias > 0` |
| tramo_rotacion | `CK_tramo_rotacion_orden_positivo` | `orden > 0` |
| tramo_rotacion | `CK_tramo_rotacion_tipo_turno` | Trabajo exige turno; Descanso exige turno nulo |

Matriz de defensa temporal:

| Invariante | DB | Dominio | Servicio/transacción |
|---|---|---|---|
| Un solo `PeriodoLaboral` abierto por trabajador | Índice parcial único | Validación del agregado | Escritura carga historia completa; concurrencia traducida |
| Un `AsignacionPlanta` abierto por periodo | Índice parcial único | Rango validado | Persistencia selectiva |
| `AsignacionPlanta.Desde <= Hasta` | Check | Sí | Sí |
| Un marcador abierto por trabajador | Índice parcial único | Validador de vigencias | Servicio |
| Un equipo+código abierto | Índice parcial único | Validador | Servicio |
| Un área/turno/sueldo abierto por trabajador | Índices parciales únicos | Validador | Servicio |
| Una versión de turno abierta y activa | Índice parcial único | Validador | ServicioTurnos |
| Solapamientos entre rangos cerrados | No | Sí | Consultas/servicios |
| `PeriodoLaboral.FechaIngreso <= FechaCese` | No | Sí | ServicioTrabajadores |
| Rangos de marcador/área/turno/sueldo | No check de fechas | Sí | Servicios |
| Programación de cuadrilla sin solapamiento | No | Sí | ServicioCuadrillas |

La base protege filas abiertas y unos pocos rangos, pero no reemplaza los validadores de
solapamiento del dominio/servicio.

## 7. Columnas legacy y proyecciones

No se identificaron tablas legacy ni tablas de aplicación sin consumidor. Las 48 tablas
mapeadas por EF se clasifican `ACTIVA`; las dos tablas EF son `TÉCNICA`. No hay candidatas a
retiro demostradas solo por el modelo físico.

Columnas legacy confirmadas: **4**.

| Tabla | Columna | Uso actual | Autoridad | Compatibilidad | Candidata a retiro |
|---|---|---|---|---|---|
| trabajador | fecha_ingreso | Inicio del vínculo actual/último proyectado | periodo_laboral.fecha_ingreso | Dual-write y UI actual | Sí, solo tras migrar consumidores y operación autorizada |
| trabajador | fecha_cese | Cese proyectado; null durante vínculo activo | periodo_laboral.fecha_cese | Dual-write y UI actual | Sí, bajo las mismas condiciones |
| trabajador | empresa_id | Empresa vigente proyectada | periodo_laboral.empresa_id | Filtros y formularios actuales | Sí, no inmediata |
| trabajador | planta_id | Planta vigente proyectada | asignacion_planta.planta_id | Alcance/UI actuales | Sí, no inmediata |

Los campos de horario de `turno` también son una proyección vigente de `version_turno`, pero
se mantienen como diseño de edición actual y no están clasificados como legacy/candidatos a
retiro. `marcador.planta_id` es una desnormalización controlada, no una historia autoritativa.

## 8. EF_SQLITE_DIVERGENCES

| Objeto | EF/snapshot | SQLite | Impacto | Clasificación |
|---|---|---|---|---|
| Tablas mapeadas | 48 `ToTable` | 48 tablas no técnicas | Ninguno | SIN_DIVERGENCIA |
| Columnas | 578 con nulabilidad/default | 578 observadas | Ninguno | SIN_DIVERGENCIA |
| Foreign keys | 47 | 47 | Ninguno | SIN_DIVERGENCIA |
| Índices declarados | 91 | 91 `origin=c` | Ninguno | SIN_DIVERGENCIA |
| Checks | 6 | 6 | Ninguno | SIN_DIVERGENCIA |
| Migraciones del repositorio | 16 | 16 en historial local | Ninguno | SIN_DIVERGENCIA |
| Tablas `__EF*` | No son entidades de dominio | 2 tablas técnicas | Esperado por EF/SQLite | ESPERADA |
| Autoíndices de PK | No se declaran como `HasIndex` | 7 `origin=pk` | Esperado | ESPERADA |
| Fechas/horas/decimal | Tipos CLR y precision lógica | Afinidad SQLite `TEXT` | Semántica del proveedor | ESPERADA |

Conteo: `SIN_DIVERGENCIA=6`, `ESPERADA=3`, `DEUDA=0`, `RIESGO=0`. Esto clasifica solo
divergencias EF↔SQLite; las relaciones lógicas sin FK y la cobertura parcial de invariantes son
decisiones/deuda del modelo, no discrepancias entre artefactos.

La comprobación adicional `dotnet ef migrations has-pending-model-changes --no-build` devolvió
`No changes have been made to the model since the last migration.`

## 9. Migraciones

Orden del repositorio y de la copia local:

1. `20260817165631_EsquemaInicial`
2. `20260910150246_EquipoMarcadorComoIdentidad`
3. `20260921143551_PadronesPlanta18Set`
4. `20260921162221_PuestosGenerales`
5. `20260921185600_AtribucionDeMarcacionesAlCalcular`
6. `20260922205054_GraciaDeSalidaPorTurno`
7. `20260922205415_OrigenVerificadoDeLaCarga`
8. `20260923140604_RotacionPorCuadrilla`
9. `20260924194824_JustificacionUnicaPorDiaYLicenciaSinGoce`
10. `20260924201125_IncidenciasYRegularizaciones`
11. `20260925142617_VersionDelPeriodo`
12. `20260925143248_VigenciaDelHorarioDeTurno`
13. `20260925150116_VigenciaDelAreaDelTrabajador`
14. `20261001212252_MarcacionesProvisionales`
15. `20261007033130_DescansoProgramadoEnRotaciones`
16. `20261012151429_PeriodoLaboralCanonicoYAsignacionPlanta`

El snapshot corresponde a la migración 16 y la copia local auditada contiene las 16. Según el
último inventario productivo autorizado, producción terminaba en
`20260923140604_RotacionPorCuadrilla`; por tanto hay **8 migraciones pendientes de producción**
(9–16). Esta fase no reabrió producción para revalidarlo y no aplicó ninguna. En particular,
`20261007033130_DescansoProgramadoEnRotaciones` está creada y probada en copias/local, pero no
aplicada productivamente; `20261012151429_PeriodoLaboralCanonicoYAsignacionPlanta` tampoco.

## 10. Conclusiones físicas

- `PeriodoLaboral` y `AsignacionPlanta` son las autoridades históricas; los cuatro campos de
  `trabajador` son proyección actual/compatibilidad.
- La atribución oficial y PMV es temporal; ninguna marca persiste trabajador.
- PMV tiene tres tablas aisladas y no alimenta tareo o planilla.
- La rotación real es una secuencia finita de `programacion_cuadrilla` y `tramo_rotacion`, con
  descanso explícito y checks físicos.
- No se hallaron tablas huérfanas ni divergencias materiales EF/snapshot/SQLite.
- Las ausencias de FK enumeradas en `DATA_RELATIONSHIPS.md` siguen siendo relaciones lógicas o
  proyecciones protegidas por aplicación.

## 11. Verificación final

```text
dotnet build -c Release: 0 errores, 0 warnings
Domain: 419/419
Infrastructure: 497/497
Web: 32/32
Total: 948/948, 0 fallos, 0 omitidos
```

Código productivo modificado: no. Migraciones nuevas: no. Producción y OP-03: no tocados.



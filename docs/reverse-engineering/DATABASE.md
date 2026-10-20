# Catálogo físico de base de datos

Fecha de corte: 2026-10-05  
Proveedor configurado: SQLite mediante Entity Framework Core 10.0.10  
Estado descrito: modelo EF/snapshot y validación física de la base canónica `src/ControlMarcacion.Web/ControlMarcacion.db` en modo de solo lectura.

## 1. Convenciones físicas

- MarcacionDbContext hereda de IdentityDbContext<ApplicationUser>; negocio e Identity comparten el mismo modelo.
- Tablas, columnas, claves, índices y restricciones se convierten globalmente a snake_case.
- Las entidades de negocio que heredan EntidadBase usan id INTEGER como PK y conservan creado_en, creado_por, modificado_en, modificado_por y activo.
- Las cadenas reciben longitud 200 por convención salvo configuración específica.
- Los decimal usan precisión lógica 12,4; SQLite los materializa conforme a su proveedor.
- Las enumeraciones relevantes se convierten a TEXT.
- La presencia de una columna llamada empresa_id, planta_id, trabajador_id o periodo_id no implica una FK. Solo se considera relación física cuando el modelo/snapshot declara HasForeignKey.
- El snapshot vigente contiene 47 tablas de aplicación y 45 relaciones FK. SQLite contiene además las dos tablas técnicas `__EFMigrationsHistory` y `__EFMigrationsLock`.

## 2. Catálogo de tablas

Abreviaturas: R = Restrict; C = Cascade; — = no existe FK física. Los índices automáticos de FK se omiten cuando no aportan información adicional.

| Tabla | Entidad | PK | FK físicas | Unique | Índices principales | Nullable relevantes | Delete behavior | Temporalidad |
|---|---|---|---|---|---|---|---|---|
| cliente | Cliente | id | — | codigo | codigo | ruc | — | Estado actual + auditoría base. |
| empresa | Empresa | id | cliente_id → cliente | codigo; ruc | cliente_id | Ninguna FK | R | Estado actual. |
| planta | Planta | id | empresa_id → empresa | empresa_id + codigo | empresa_id + codigo | Ninguna FK | R | Estado actual; sin historia. |
| area | Area | id | planta_id → planta | planta_id + codigo | planta_id + codigo | Ninguna FK | R | Catálogo actual; la asignación del trabajador sí se historiza. |
| seccion | Seccion | id | area_id → area | area_id + codigo | area_id + codigo | Ninguna FK | R | Estado actual; sin historia de pertenencia del trabajador. |
| centro | Centro | id | empresa_id → empresa; planta_id → planta | empresa_id + codigo | empresa_id + codigo; planta_id | planta_id, descripcion | R / R | Estado actual. |
| centro_costo | CentroCosto | id | centro_id → centro | — | centro_id + codigo, no único | — | R | Estado actual; sin historia del trabajador. |
| tipo_documento | TipoDocumento | id | — | codigo | codigo | longitud_exacta, longitud_maxima | — | Catálogo actual. |
| puesto | Puesto | id | — | codigo | codigo | empresa_id, pero sin FK | — | Estado actual; sin historia del trabajador. |
| feriado | Feriado | id | — | — | fecha + planta_id | planta_id, pero sin FK | — | Hecho por fecha y alcance opcional. |
| equipo_marcador | EquipoMarcador | id | planta_id → planta | codigo; serie cuando no es NULL | planta_id + activo; serie filtrado | serie, descripcion | R | Estado actual del aparato. |
| marcador | Marcador | id | equipo_id → equipo_marcador; planta_id → planta | equipo_id + codigo | planta_id + activo | descripcion | R / R | Catálogo actual; la propiedad del código se historiza aparte. |
| turno | Turno | id | — | codigo | codigo | — | — | Identidad y horario actual duplicado intencionalmente con versiones. |
| version_turno | VersionTurno | id | turno_id → turno | una abierta y activa por turno | turno_id + fecha_inicio; turno_id filtrado | fecha_fin | C | Línea temporal inclusiva. |
| turno_planta | TurnoPlanta | id | turno_id → turno; planta_id → planta | turno_id + planta_id | planta_id | — | C / R | Asociación vigente, no historizada. |
| cuadrilla | Cuadrilla | id | planta_id → planta | planta_id + codigo | planta_id + codigo | — | R | Estado actual. |
| programacion_cuadrilla | ProgramacionCuadrilla | id | cuadrilla_id → cuadrilla | — | cuadrilla_id + fecha_inicio + fecha_fin | fecha_fin | R | Línea temporal del ciclo. |
| tramo_rotacion | TramoRotacion | id | programacion_id → programacion_cuadrilla; turno_id → turno | programacion_id + orden | turno_id | — | R / R | Parte ordenada de una programación. |
| periodo | Periodo | id | — | codigo | fecha_inicio + fecha_fin | datos de cierre/reapertura | — | Rango operativo; version es token de concurrencia. |
| tipo_ausencia | TipoAusencia | id | — | codigo | codigo | máximos por evento/año | — | Catálogo actual. |
| trabajador | Trabajador | id | — | tipo_documento_id + numero_documento | empresa_id + activo; planta_id + area_id + seccion_id | área, sección, centro_costo, fecha/motivo de cese y contacto | — | Ficha actual; sus IDs organizativos son escalares sin FK. |
| periodo_laboral | PeriodoLaboral | id | trabajador_id → trabajador; empresa_id → empresa | — | trabajador_id + fecha_ingreso; empresa_id | fecha/motivo/observación de cese | C / R | Línea histórica sin unicidad de periodo abierto. |
| asignacion_marcador | AsignacionMarcador | id | trabajador_id → trabajador; equipo_id → equipo_marcador | una abierta por trabajador; una abierta por equipo+código | equipo+código+inicio+fin; trabajador+inicio | fecha_fin | C / R | Línea temporal. |
| asignacion_area | AsignacionArea | id | trabajador_id → trabajador; area_id → area | una abierta por trabajador | trabajador+inicio+fin; area_id | fecha_fin | C / R | Línea temporal. |
| asignacion_turno | AsignacionTurno | id | trabajador_id → trabajador; turno_id → turno; cuadrilla_id → cuadrilla | una abierta por trabajador | trabajador+inicio+fin; turno_id; cuadrilla_id | fecha_fin y exactamente una de turno_id/cuadrilla_id | C / R / R | Línea temporal con check XOR. |
| asignacion_sueldo | AsignacionSueldo | id | trabajador_id → trabajador | una abierta por trabajador | trabajador+inicio+fin | fecha_fin | C | Línea temporal. |
| importacion_marcador | ImportacionMarcador | id | equipo_id → equipo_marcador | — | hash_contenido; anulada; fecha_carga; equipo+fecha | serie, datos de anulación | R | Evento de carga; anulación lógica. Hash no único. |
| error_importacion | ErrorImportacion | id | importacion_id → importacion_marcador | — | importacion_id | contenido_linea | C | Detalle del evento de carga. |
| marcacion_cruda | MarcacionCruda | id | equipo_id → equipo_marcador; importacion_id → importacion_marcador | equipo_id + id_marcador + fecha_hora | fecha_hora; importacion_id + fecha_hora | metadatos de la línea | R / R | Hecho inmutable por instante. |
| prov_importacion | ImportacionProvisional | id | equipo_id → equipo_marcador | — | hash_contenido; equipo_id + fecha_carga | campos de modificación | R | Evento de carga PMV; hash indexado pero no único. |
| prov_marcacion | MarcacionProvisional | id | equipo_id → equipo_marcador; importacion_provisional_id → prov_importacion | equipo_id + id_marcador + fecha_hora | fecha_hora; importacion_provisional_id | modo, línea original y modificación | R / R | Hecho provisional; atribución a trabajador no persistida. |
| prov_error_importacion | ErrorImportacionProvisional | id | importacion_provisional_id → prov_importacion | — | importacion_provisional_id | contenido de línea y modificación | C | Rechazo o duplicado de carga PMV. |
| solicitud_ausencia | SolicitudAusencia | id | tipo_ausencia_id → tipo_ausencia | — | trabajador+rango; estado+inicio | horas, datos clínicos, documento, resolución | R | Flujo por rango; trabajador_id no es FK. |
| justificacion | Justificacion | id | solicitud_ausencia_id → solicitud_ausencia | trabajador_id + fecha para estados activos | periodo_id; estado; solicitud | solicitud, tipo ausencia, horas, resolución y documento | R | Hecho diario; version es token de concurrencia. Trabajador, periodo y tipo ausencia no tienen FK. |
| tareo_diario | TareoDiario | id | — | trabajador_id + fecha | periodo_id + estado; fecha; pendientes filtrado | turno_id, marcas reales, incidencia, justificación y observación | — | Proyección diaria recalculable. Sus IDs no tienen FK. |
| reintegro_tareo | ReintegroTareo | id | — | trabajador_id + periodo_id | trabajador_id + periodo_id | — | — | Ajuste por periodo; IDs sin FK. |
| parametro_legal | ParametroLegal | id | — | — | tipo + vigente_desde + vigente_hasta | vigente_hasta, base_legal | — | Línea temporal normativa sin exclusión física. |
| tasa_sobretasa | TasaSobretasa | id | — | — | concepto + vigente_desde + vigente_hasta | vigente_hasta, base_legal, empresa_id sin FK | — | Línea temporal normativa sin exclusión física. |
| registro_auditoria | RegistroAuditoria | id | — | — | fecha_hora descendente; tabla+clave+fecha; usuario+fecha | — | — | Bitácora append-only por comportamiento de aplicación. |
| asp_net_users | ApplicationUser | id TEXT | — | normalized_user_name | normalized_email; normalized_user_name | campos opcionales estándar y propios; tipo_documento_id sin FK | — | Estado de cuenta; concurrency_stamp. |
| asp_net_roles | IdentityRole | id TEXT | — | normalized_name | normalized_name | normalized_name | — | Catálogo Identity; concurrency_stamp. |
| asp_net_role_claims | IdentityRoleClaim | id INTEGER | role_id → asp_net_roles | — | role_id | claim_type, claim_value | C | Identity. |
| asp_net_user_claims | IdentityUserClaim | id INTEGER | user_id → asp_net_users | — | user_id | claim_type, claim_value | C | Identity. |
| asp_net_user_logins | IdentityUserLogin | login_provider + provider_key | user_id → asp_net_users | PK compuesta | user_id | provider_display_name | C | Identity. |
| asp_net_user_roles | IdentityUserRole | user_id + role_id | user_id → asp_net_users; role_id → asp_net_roles | PK compuesta | role_id | — | C / C | Asociación Identity. |
| asp_net_user_tokens | IdentityUserToken | user_id + login_provider + name | user_id → asp_net_users | PK compuesta | — | value | C | Identity. |
| usuario_planta | UsuarioPlanta | usuario_id + planta_id | usuario_id → asp_net_users; planta_id → planta | PK compuesta | planta_id | — | C / R | Asociación de alcance vigente. |

## 3. Restricciones transversales

### Unicidad temporal

SQLite refuerza únicamente la existencia de una fila abierta para asignaciones de marcador, área, turno, sueldo y versión de turno. No impide por sí mismo el solapamiento de dos filas cerradas.

AsignacionMarcador agrega una segunda defensa: equipo_id + id_marcador es único cuando fecha_fin es NULL. No impide que dos asignaciones históricas cerradas del mismo código se solapen.

ProgramacionCuadrilla y PeriodoLaboral no tienen índice único filtrado para la fila abierta.
R-P1-02 evita duplicados secuenciales al cargar el vínculo abierto en cada escritura, pero
la defensa concurrente de `PeriodoLaboral` sigue pendiente: primero deben sanearse los 85
grupos en `DATA-PERIODOLABORAL`, porque el índice no puede crearse mientras existan.

### Check constraints

Solo se observó un check de negocio explícito:

CK_asignacion_turno_turno_o_cuadrilla exige que exactamente una de turno_id y cuadrilla_id sea NULL.

No hay checks físicos para fecha_fin >= fecha_inicio, fecha_cese >= fecha_ingreso, días de tramo positivos, ancla o coherencia de campos de cese.

### Concurrencia

- periodo.version: token optimista.
- justificacion.version: token optimista.
- asp_net_users.concurrency_stamp y asp_net_roles.concurrency_stamp: tokens de Identity.
- TareoDiario, líneas temporales e importaciones no tienen token de concurrencia propio.

### Valores por defecto y conversiones destacadas

- turno.minutos_anticipacion_entrada: 15.
- turno.minutos_anticipacion_salida: 5.
- turno.minutos_gracia_salida: no nulo; la migración lo incorporó con 0 para filas existentes.
- Enums de turnos, periodos, tareo, solicitudes, justificaciones, parámetros y auditoría: TEXT.
- Estado activo y marcas de auditoría provienen de EntidadBase y SaveChangesAsync.

## 4. Relaciones ausentes aunque existan columnas ID

Las siguientes referencias son escalares y el snapshot no declara FK:

- trabajador → tipo_documento, empresa, planta, área, sección, puesto y centro_costo.
- feriado → planta.
- puesto → empresa.
- tasa_sobretasa → empresa.
- solicitud_ausencia → trabajador.
- justificacion → trabajador, periodo y tipo_ausencia.
- tareo_diario → periodo, trabajador, turno y justificacion.
- reintegro_tareo → periodo y trabajador.

Por ello, una escritura directa puede introducir identificadores inexistentes en esas columnas sin que SQLite lo rechace por FK. Los servicios suelen validarlos, pero esa protección no es estructural.

## 5. Tareo e importación

TareoDiario es único por trabajador y fecha, pero no tiene FK física ni a trabajador ni a periodo. Tampoco existe tabla de asociación con MarcacionCruda. EntradaReal, SalidaReal y JustificacionId son datos proyectados; no identifican el conjunto de marcas consumido.

MarcacionCruda sí tiene FKs restrictivas a equipo e importación y una clave natural física única por equipo, código e instante. El hash de ImportacionMarcador está indexado pero no es único: la aplicación puede detectar/rechazar ciertos reenvíos, mientras que la estructura admite varias cabeceras de carga con el mismo hash. La unicidad de marcas impide duplicar el mismo hecho exacto dentro del mismo equipo.

Anular una importación actualiza la cabecera; no elimina sus marcas. Los consumidores efectivos excluyen las importaciones anuladas por consulta.

Las marcaciones provisionales forman una rama separada. `prov_marcacion` replica la clave natural de la marca, pero no se relaciona con `marcacion_cruda`, `tareo_diario` ni trabajador. El hash provisional también es no único. A diferencia de la importación principal, la carga PMV contiene sus dos guardados en una transacción explícita.

## 6. Estado de modelo, snapshot, migraciones y base canónica

En Fase 3A, tras compilar el estado entonces vigente sin restaurar paquetes, `dotnet ef migrations has-pending-model-changes` informó que no existían cambios pendientes. En Fase 3B no se repitió el comando porque puede construir/inicializar el host; se compararon directamente snapshot, migraciones, `sqlite_master`, pragmas e historial físico.

Clasificación:

| Comparación | Estado | Evidencia |
|---|---|---|
| Modelo EF actual vs snapshot | CONSISTENTE | Configuraciones PMV presentes en el snapshot y sin nombres físicos ausentes. |
| Snapshot vs migraciones manuales presentes | CONSISTENTE | Secuencia de 14 migraciones, incluida `20261001212252_MarcacionesProvisionales`. |
| Snapshot vs base canónica | CONSISTENTE | 47/47 tablas de aplicación y 87/87 índices explícitos por nombre. |
| Migraciones vs base canónica | CONSISTENTE | Las 14 migraciones manuales aparecen en `__EFMigrationsHistory`. |
| Integridad física | CONSISTENTE | `integrity_check=ok`; `foreign_key_check` sin filas. |

Resumen físico de la base canónica:

- 49 tablas no internas: 47 de aplicación + 2 técnicas de EF;
- 45 FK, 87 índices explícitos, 32 explícitos únicos y 9 parciales;
- un check de negocio, sin triggers ni vistas;
- `PRAGMA user_version=0`, esperado porque EF usa su tabla de historial;
- 3 tablas PMV con 2 importaciones, 731 marcas y 761 errores/rechazos.

## 7. Hallazgos físicos que matizan el catálogo

- No se encontraron huérfanos ni índices esperados ausentes.
- Las líneas temporales pobladas no tienen rangos invertidos ni huecos detectados.
- `periodo_laboral` sí tiene 85 grupos abiertos duplicados en sus campos de negocio; 170 de sus 296 filas están involucradas. R-P1-02 no modificó esas filas: corrigió únicamente la escritura futura.
- Trabajador/área y Turno/VersionTurno coinciden en los estados abiertos observados.
- Cuadrillas, rotaciones, importación principal, marcaciones crudas y tareos no tienen datos representativos en esta base; su integridad dinámica sigue sin poder validarse con datos reales.
- El detalle completo, planes y método están en `PHASE_03B_DATABASE_VALIDATION.md`.

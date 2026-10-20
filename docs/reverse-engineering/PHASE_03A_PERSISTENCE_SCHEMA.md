# Fase 3A — Persistencia, esquema e integridad estructural

Fecha de corte: 2026-09-29  
Estado: completada  
Comprensión inicial: 62 %  
Comprensión final estimada: 76 %

Alcance: modelo EF Core, configuraciones, migraciones manuales, snapshot, servicios persistentes e inventario estático de consultas. No se abrió ni modificó ninguna base física, no se analizaron datos personales, no se ejecutaron migraciones y no se hicieron cambios funcionales.

## 1. Resumen ejecutivo

El modelo actual se materializa en 44 tablas SQLite: 37 tablas propias, incluida UsuarioPlanta, y siete tablas estándar de Identity. El snapshot declara 41 relaciones FK, 31 índices únicos, un check constraint de negocio y dos tokens de concurrencia propios del sistema.

La estructura es selectivamente relacional. Organización, asignaciones, equipos, importaciones, rotaciones e Identity tienen FKs explícitas. En contraste, varias columnas críticas son identificadores escalares sin FK: los catálogos organizativos del trabajador, las referencias de TareoDiario, los IDs de trabajador/periodo en solicitudes, justificaciones y reintegros, y varios alcances opcionales. Los servicios validan gran parte de estas referencias, pero una escritura directa a SQLite puede introducir huérfanos.

Las líneas temporales están protegidas contra más de una fila abierta en asignaciones y versiones mediante índices únicos filtrados. Ninguna restricción física evita el solapamiento de intervalos históricos cerrados. PeriodoLaboral y ProgramacionCuadrilla ni siquiera tienen unicidad física de fila abierta; allí la coherencia depende de los escritores de aplicación.

TareoDiario confirma el hallazgo de Fase 2: es único por trabajador y fecha, pero no tiene FKs a trabajador, periodo, turno o justificación y no existe relación persistida con las MarcacionCruda usadas. La importación sí tiene una clave natural fuerte para cada marca exacta: equipo, código e instante.

Después de recompilar el estado actual, EF Core confirmó que el modelo y MarcacionDbContextModelSnapshot no tienen cambios pendientes. Esto no permite afirmar qué migraciones están aplicadas en una instalación: el estado de una base desplegada queda expresamente reservado para Fase 3B.

## 2. Fuentes y método

Orden aplicado:

1. Entidades ya documentadas en Fases 1 y 2.
2. MarcacionDbContext y sus convenciones.
3. Configuraciones EF reabiertas solo para PK, FK, índices, nulabilidad, checks, defaults, conversiones, borrado y concurrencia.
4. Las 13 migraciones manuales, sin abrir Designer.cs.
5. Snapshot final para resolver el estado actual y enumerar relaciones físicas.
6. Servicios y consultas solo para CRUD, sincronización, integridad e inventario estático.

No se utilizó ControlMarcacion.db ni sus archivos WAL/SHM, aunque aparecen en el árbol de trabajo. Ninguna base fue declarada canónica.

El catálogo completo está en [DATABASE.md](DATABASE.md) y el ERD físico en [D06_DATABASE_ERD.md](diagrams/D06_DATABASE_ERD.md).

## 3. Modelo físico consolidado

| Métrica | Resultado confirmado |
|---|---:|
| Tablas en snapshot | 44 |
| Relaciones FK | 41 |
| Declaraciones de índice único | 31 |
| Check constraints de negocio | 1 |
| Tokens de concurrencia totales | 4 |
| Tokens de concurrencia propios del negocio | 2 |
| Líneas temporales críticas auditadas | 7 |

El único check explícito exige el XOR entre turno_id y cuadrilla_id en asignacion_turno.

Los tokens propios son periodo.version y justificacion.version. Los otros dos pertenecen a asp_net_users y asp_net_roles.

Las convenciones globales convierten todo a snake_case, asignan longitud 200 por defecto a strings y precisión 12,4 a decimal. Las enumeraciones relevantes se guardan como texto.

## 4. Matriz CRUD observada

Leyenda: Sí = operación física observada; Lógico = cambio de estado/anulación/cierre; Semilla = creación o cambio mediante datos de migración; — = no observada en servicios revisados.

| Módulo | Tabla | C | R | U | D | Observación |
|---|---|---:|---:|---:|---:|---|
| Organización | cliente | Semilla | Sí | Semilla | — | Catálogo raíz; no se observó mantenimiento operativo. |
| Organización | empresa | Semilla | Sí | Semilla | — | Consultada por personal y reportes. |
| Organización | planta | Sí | Sí | Sí | Lógico | ServicioCatalogos cambia Activo; no borra físicamente. |
| Organización | area | Sí | Sí | Sí | Lógico | ServicioCatalogos; además es referencia de AsignacionArea. |
| Organización | seccion | Sí | Sí | Sí | Lógico | ServicioCatalogos. |
| Organización | centro | Semilla | Sí | Semilla | — | Datos SAP; sin mantenimiento operativo observado. |
| Organización | centro_costo | Semilla | Sí | Semilla | — | Leído por personal/reportes. |
| Catálogos | tipo_documento | Sí | Sí | Sí | Lógico | ServicioCatalogos. |
| Catálogos | puesto | Sí | Sí | Sí | Lógico | ServicioCatalogos. |
| Catálogos | feriado | Sí | Sí | Sí | — | ServicioFeriados crea/importa y actualiza. |
| Personal | trabajador | Sí | Sí | Sí | — | El cese es U sobre la ficha, no borrado. |
| Personal | periodo_laboral | Sí | Sí | Sí | — | Se abre/sincroniza/cierra desde Trabajador y ServicioTrabajadores. |
| Personal | asignacion_marcador | Sí | Sí | Sí | — | Se corrige, parte o cierra; no se elimina. |
| Personal | asignacion_area | Sí | Sí | Sí | — | Se corrige o parte desde ServicioTrabajadores. |
| Personal | asignacion_turno | Sí | Sí | Sí | — | Se corrige, parte o cierra. |
| Personal | asignacion_sueldo | Sí | Sí | Sí | — | Se corrige, parte o cierra. |
| Equipos | equipo_marcador | Sí | Sí | Sí | Lógico | Cambio de planta bloqueado si ya tiene dependencias. |
| Equipos | marcador | Sí | Sí | Sí | Lógico | Planta se copia desde EquipoMarcador. |
| Horarios | turno | Sí | Sí | Sí | Lógico | Desactivación protegida cuando está en uso. |
| Horarios | version_turno | Sí | Sí | Sí | — | Se abre, corrige o cierra junto al horario actual. |
| Horarios | turno_planta | Sí | Sí | — | Sí | Asociación sincronizada con altas y remociones físicas. |
| Rotación | cuadrilla | Sí | Sí | Sí | Lógico | No se elimina. |
| Rotación | programacion_cuadrilla | Sí | Sí | Sí | Lógico | Cierra intervalos o desactiva programaciones reemplazadas. |
| Rotación | tramo_rotacion | Sí | Sí | — | — | Nace con la programación; FK Restrict impide cascada. |
| Periodos | periodo | Sí | Sí | Sí | — | Cierre/reapertura incrementa versión. |
| Ausencias | tipo_ausencia | Sí | Sí | Sí | Lógico | ServicioTiposAusencia. |
| Ausencias | solicitud_ausencia | Sí | Sí | Sí | Lógico | Aprobar, rechazar y anular cambian estado. |
| Ausencias | justificacion | Sí | Sí | Sí | Sí | Puede eliminarse físicamente en flujos concretos y removerse al anular solicitudes. |
| Importación | importacion_marcador | Sí | Sí | Lógico | — | Anulación/reactivación conserva cabecera y marcas. |
| Importación | error_importacion | Sí | Sí | — | — | Se crea como detalle; cascade solo si se borrara la importación. |
| Importación | marcacion_cruda | Sí | Sí | — | — | Tratada como hecho inmutable. |
| Asistencia | tareo_diario | Sí | Sí | Sí | — | Upsert lógico por trabajador+fecha durante recálculo. |
| Asistencia | reintegro_tareo | Sí | Sí | Sí | — | Registro único por trabajador+periodo. |
| Normativa | parametro_legal | Semilla | Sí | Semilla | — | No se observó mantenimiento operativo. |
| Normativa | tasa_sobretasa | Semilla | Sí | Semilla | — | Lectura por vigencia. |
| Auditoría | registro_auditoria | Sí | Sí | — | — | Append-only por interceptor. |
| Seguridad | asp_net_users | Sí | Sí | Sí | — | UserManager crea y activa/desactiva. |
| Seguridad | asp_net_roles | Semilla | Sí | — | — | Roles predefinidos. |
| Seguridad | tablas auxiliares Identity | Sí | Sí | Sí | Sí | Operadas por Identity según claims, logins, roles y tokens. |
| Seguridad | usuario_planta | Sí | Sí | — | — | Creada con el usuario; no se observó editor posterior en el servicio actual. |

## 5. Auditoría de integridad temporal

| Línea histórica | A. Qué impide solapamiento | B. Capas | C. Refuerzo físico | D. Anomalías posibles por escritura directa | Clasificación |
|---|---|---|---|---|---|
| AsignacionMarcador | ValidadorVigencias tras la reasignación; el servicio parte/cierra intervalos | Domain + Service + Database | Una abierta por trabajador; una abierta por equipo+código; FKs y dos índices temporales | Intervalos cerrados solapados, fecha_fin anterior a inicio, doble propietario histórico del mismo código | PROTECCIÓN PARCIAL |
| AsignacionArea | SincronizarAreaVigente corrige o cierra/abre la fila vigente | Service + Database | Una abierta por trabajador; FKs a trabajador y área; índice temporal | Solapamientos cerrados, rango invertido, AreaId actual distinto del historial | PROTECCIÓN PARCIAL |
| AsignacionTurno | ReasignarTurno + ValidadorVigencias | Domain + Service + Database | Una abierta por trabajador; FK; check XOR turno/cuadrilla | Solapamientos cerrados, rango invertido, referencias válidas pero planta incompatible | PROTECCIÓN PARCIAL |
| AsignacionSueldo | Reasignación genérica + ValidadorVigencias | Domain + Service + Database | Una abierta por trabajador; FK; índice temporal | Solapamientos cerrados, rango invertido, importes fuera de reglas de aplicación | PROTECCIÓN PARCIAL |
| PeriodoLaboral | AbrirVinculo, SincronizarVinculoAbierto y DarDeBaja trabajan sobre el vínculo abierto | Domain + Service | Solo FKs e índice trabajador+fecha; no hay unique abierto | Varias filas abiertas, solapamientos, cese anterior al ingreso, divergencia con la ficha | SOLO APLICACIÓN |
| VersionTurno | ServicioTurnos corrige la vigente o la cierra el día anterior y abre otra | Service + Database | Una versión abierta y activa por turno; FK cascade; índice turno+inicio | Versiones cerradas solapadas, rango invertido, horario actual diferente de la versión abierta | PROTECCIÓN PARCIAL |
| ProgramacionCuadrilla | ReemplazarDesdeAsync cierra o desactiva programaciones y crea una nueva | Service | FKs e índice de búsqueda, pero sin unique abierto ni check temporal | Varias activas abiertas, solapamientos, rangos invertidos, ancla/tramos incoherentes | SOLO APLICACIÓN |

No se clasifica ninguna línea como PROTECCIÓN FUERTE porque SQLite no tiene una restricción que excluya cualquier solapamiento de intervalos, incluidos los cerrados.

## 6. Fuentes duplicadas

### Trabajador vs PeriodoLaboral

Restricciones físicas:

- PeriodoLaboral tiene FK cascade a Trabajador y FK Restrict a Empresa.
- No hay unicidad de vínculo abierto ni check de fechas.
- Trabajador.EmpresaId es obligatorio pero no tiene FK física; FechaIngreso/FechaCese tampoco se relacionan estructuralmente con PeriodoLaboral.

Sincronización observada:

- CrearAsync llena la ficha y llama Trabajador.AbrirVinculo.
- ActualizarAsync modifica la ficha y SincronizarVinculoAbierto ajusta fecha de ingreso y empresa de la fila abierta.
- DarDeBaja actualiza ficha y primer vínculo abierto.
- Una ficha creada ya cesada abre el vínculo con sus datos, pero CerrarAsignacionesSiNaceCesado solo cierra marcador, turno y sueldo; el vínculo ya nace con FechaCese.
- La carga masiva usa ServicioTrabajadores, por lo que comparte estos escritores.

Inconsistencias estructuralmente posibles:

- Ficha activa con ningún vínculo abierto, o con varios.
- Empresa/fechas actuales distintas del vínculo abierto.
- PeriodosLaborales cerrados solapados.
- EmpresaId inexistente en Trabajador, aunque el historial sí exige empresa válida.
- Cambio directo de campos planos sin actualizar el historial.

### Trabajador.AreaId vs AsignacionArea

ServicioTrabajadores escribe AreaId y llama SincronizarAreaVigente cuando recibe un área. En alta, la primera asignación rige desde FechaIngreso; en edición normal rige desde la fecha actual, salvo corrección en el mismo inicio.

Trabajador.AreaId no tiene FK y puede ser NULL. AsignacionArea sí tiene FKs a Trabajador y Area y una sola fila abierta por trabajador. La base no obliga a que AreaId coincida con esa fila, ni a que exista asignación cuando el campo actual tiene valor.

### Turno vs VersionTurno

ServicioTurnos escribe primero el horario actual de Turno y AjustarVigenciaDelHorarioAsync conserva la versión: corrige en sitio si la fecha efectiva no supera el inicio, o cierra la vigente y crea otra. Crear un turno genera una versión desde la vigencia original.

VersionTurno tiene FK cascade y unique filtrado para una versión abierta y activa. No hay constraint que compare sus campos horarios con Turno, por lo que una escritura directa puede hacerlos divergir.

### Marcador.PlantaId vs EquipoMarcador.PlantaId

La duplicación existe físicamente: Marcador tiene dos FKs independientes, una a EquipoMarcador y otra a Planta. ServicioMarcadores copia marcador.PlantaId desde el equipo al crear/editar. ServicioEquipos impide mover un equipo de planta cuando ya tiene marcadores o importaciones.

No existe FK compuesta ni check que exija marcador.planta_id = equipo_marcador.planta_id. Una escritura directa puede producir divergencia aun con ambas FKs válidas.

## 7. Historia organizativa permitida por el esquema

| Dimensión | Campo actual | Historia física | Integridad |
|---|---|---|---|
| Empresa | trabajador.empresa_id, sin FK | periodo_laboral.empresa_id con FK | Historia parcial de vínculo; no gobierna uniformemente cálculo/reportes. |
| Planta | trabajador.planta_id, sin FK | No existe asignación temporal | Solo estado actual. |
| Área | trabajador.area_id, sin FK | asignacion_area con FKs | Historia explícita, pero sincronización no reforzada entre ambas representaciones. |
| Sección | trabajador.seccion_id, sin FK | No existe | Solo estado actual. |
| Centro de costo | trabajador.centro_costo_id, sin FK | No existe | Solo estado actual. |
| Centro | Se deriva del centro de costo actual | No existe | No hay historia del trabajador. |

El esquema permite conservar empresa por vínculo y área por fecha. No puede reconstruir físicamente planta, sección o centro de costo históricos a partir de una línea temporal propia.

## 8. Integridad de TareoDiario

Hallazgos confirmados:

- PK id.
- Unique físico trabajador_id + fecha.
- Índices periodo_id + estado, fecha y uno filtrado para pendientes de validación.
- PeriodoId y TrabajadorId son obligatorios a nivel de nulabilidad, pero no tienen FK.
- TurnoId y JustificacionId son opcionales y tampoco tienen FK.
- EntradaReal, SalidaReal, TipoIncidencia y Observacion son opcionales.
- Estado y TipoDiaNoLaborable se almacenan como texto requerido, sin check de valores permitidos en SQLite.
- No existe token de concurrencia en TareoDiario.
- No hay check de minutos no negativos ni de que Fecha pertenezca al PeriodoId indicado.
- La protección de periodo cerrado se ejerce en servicios mediante GuardiaDePeriodo y periodo.version.
- No existe FK, tabla puente ni colección persistida que conecte TareoDiario con MarcacionCruda.

Consecuencia estructural: la base asegura idempotencia de fila por trabajador/fecha, pero no asegura por sí sola la existencia de trabajador, periodo, turno o justificación, ni la trazabilidad exacta de las marcas consumidas.

## 9. Integridad de importación

| Elemento | Protección estructural | Protección de aplicación | Posibilidad residual |
|---|---|---|---|
| Equipo físico | codigo único; serie única cuando no es NULL; FK a planta | Valida planta, estado y serie | Serie NULL en varios equipos es válida. |
| Marcador | equipo_id + codigo único; FKs separadas a equipo y planta | Copia planta del equipo y bloquea cambios peligrosos | Equipo y planta pueden divergir por SQL directo. |
| AsignacionMarcador | FKs a trabajador/equipo; únicas abiertas | Valida catálogo, equipo, planta y vigencias | No hay FK al registro Marcador por equipo+código; cerradas pueden solaparse. |
| ImportacionMarcador | FK a equipo; hash indexado | Compara hash, serie y coincidencias con otros equipos; anula lógicamente | Hash no único: pueden existir varias cabeceras con el mismo contenido. |
| MarcacionCruda | unique equipo+código+instante; FKs restrictivas a equipo/importación | Colapsa doble fichada, consulta existentes antes de insertar | Marcas cercanas pero no idénticas son estructuralmente distintas; la ventana es solo regla de aplicación. |
| Anulación | Cabecera conserva bool y metadatos | Consultas excluyen IDs anulados | Las marcas permanecen físicamente y contarían si una consulta omite el filtro. |

La importación guarda la cabecera en un SaveChanges y luego las marcas en otro. No se observó una transacción externa envolviendo ambos pasos en el método revisado. Si la segunda escritura falla por una carrera o error, es posible que quede una cabecera ya persistida sin todas las marcas previstas. Se registra como candidato; confirmar frecuencia e impacto requiere pruebas y datos.

La clave natural física de la marca es fuerte para duplicados exactos. El hash sirve para detección, no para unicidad.

## 10. Cronología resumida de migraciones

| Orden | Migración | Cambio conceptual | Módulo afectado | Riesgo |
|---:|---|---|---|---|
| 1 | 20260817165631_EsquemaInicial | Crea esquema de negocio, Identity, semillas, índices y FKs iniciales | Todo el sistema | Alto: línea base extensa. |
| 2 | 20260910150246_EquipoMarcadorComoIdentidad | Introduce equipo físico, migra identidad de códigos y cambia unicidad/atribución por equipo | Importación y personal | Alto: backfill SQL basado en plantas, códigos y filas existentes. |
| 3 | 20260921143551_PadronesPlanta18Set | Actualiza padrones sembrados y agrega vínculo opcional Centro→Planta | Organización | Medio: cambio intensivo de datos maestros. |
| 4 | 20260921162221_PuestosGenerales | Normaliza y amplía catálogo de puestos | Organización/personal | Medio: semántica de datos sembrados. |
| 5 | 20260921185600_AtribucionDeMarcacionesAlCalcular | Elimina trabajador_id de MarcacionCruda | Importación/tareo | Alto: la atribución pasa a depender enteramente de vigencias. |
| 6 | 20260922205054_GraciaDeSalidaPorTurno | Agrega minutos_gracia_salida con 0 inicial | Turnos | Bajo: columna no nula con default de migración. |
| 7 | 20260922205415_OrigenVerificadoDeLaCarga | Agrega origen y serie declarada del archivo | Importación | Medio: filas antiguas quedan NoVerificado. |
| 8 | 20260923140604_RotacionPorCuadrilla | Agrega cuadrillas, programaciones, tramos, FK opcional y XOR en asignación | Turnos/personal | Alto: nueva cadena temporal. |
| 9 | 20260924194824_JustificacionUnicaPorDiaYLicenciaSinGoce | Normaliza datos y hace única la justificación diaria | Ausencias | Alto: requiere resolver datos previos antes de imponer unicidad. |
| 10 | 20260924201125_IncidenciasYRegularizaciones | Agrega incidencia/validación al tareo y workflow/concurrencia a justificación | Tareo/justificaciones | Alto: amplía estado persistido y filtros. |
| 11 | 20260925142617_VersionDelPeriodo | Agrega periodo.version y crea VersionTurno | Periodos/turnos | Alto: concurrencia y nueva historia. |
| 12 | 20260925143248_VigenciaDelHorarioDeTurno | Siembra/backfill de horarios históricos | Turnos | Alto: fija una interpretación inicial del pasado. |
| 13 | 20260925150116_VigenciaDelAreaDelTrabajador | Crea AsignacionArea y la llena desde el área actual | Personal/organización | Alto: convierte un valor actual en historia inicial. |

No se identificó una columna residual en el modelo actual por el solo hecho de ser antigua. La evolución explica varias duplicaciones actuales: Turno/VersionTurno y Trabajador.AreaId/AsignacionArea fueron introducidas como pares actual-histórico.

## 11. Drift modelo ↔ snapshot ↔ migraciones

| Comparación | Clasificación | Resultado |
|---|---|---|
| Entidades/configuraciones actuales vs snapshot | CONSISTENTE | EF Core informó que no hay cambios pendientes después de compilar el estado actual. |
| Snapshot vs secuencia de migraciones manuales | CONSISTENTE | Las estructuras finales tienen una operación de creación/adición/backfill en la cronología. |
| Binarios previos vs snapshot | No aplica al estado final | Una comprobación antes de recompilar usó ensamblados obsoletos; la recompilación eliminó el falso positivo. |
| Repositorio vs base de producción | NO DETERMINABLE SIN BD | Falta copia canónica y __EFMigrationsHistory. |

No hay DRIFT CONFIRMADO entre las fuentes de código actuales. Tampoco se infiere que una base existente esté actualizada.

## 12. Inventario estático de consultas críticas

Las cifras son ocurrencias estáticas orientativas, no cantidad de viajes SQL ejecutados.

| Componente | Patrón observado | Evaluación | Motivo |
|---|---|---|---|
| ServicioCalculoTareo | Múltiples listas AsNoTracking para trabajadores, asignaciones, turnos, versiones, marcas, justificaciones y tasas; cálculo posterior en memoria | REVISAR | El diseño evita consultar por cada día, pero usa muchas consultas secuenciales y materializaciones por periodo. Requiere medición para juzgarlo. |
| ServicioReportes | Proyecciones AsNoTracking separadas para tareos y catálogos; cruces mediante diccionarios/lookups; un bucle de cálculo nocturno en memoria | REVISAR | Agregador multi-fuente. No se observó query dentro de ese bucle. |
| ServicioImportacion | Consultas acotadas por equipo, IDs y rango; prelectura de existentes; loops de transformación y AddAsync por marca | REVISAR | No hay consulta por cada marca, pero la cantidad de entidades rastreadas y dos guardados merecen medición con archivos grandes. |
| ServicioTrabajadores | Asignación masiva recorre IDs y dentro del loop obtiene cada trabajador y valida acceso | CANDIDATO A PROBLEMA | Existe una secuencia de consultas proporcional a la cantidad de trabajadores seleccionados. No se etiqueta como N+1 sin traza SQL. |
| ConsultaTrabajadores | Count paginado y una proyección con varias subconsultas correlacionadas FirstOrDefault/Any | REVISAR | EF genera una consulta compuesta; hace falta revisar SQL/plan antes de juzgar. |
| ServicioSolicitudes | Precarga periodos y justificaciones del rango antes de recorrer fechas | NORMAL | El loop diario usa colecciones ya materializadas y agrega entidades; no consulta por día. |
| ServicioJustificaciones | Consultas puntuales por PK/IDs, comprobación de alcance y tokens de concurrencia | NORMAL | Secuencia pequeña ligada a una operación individual; loop observado solo valida dos horas en memoria. |

No se declara N+1, problema de Include ni degradación de rendimiento. Las clasificaciones indican dónde medir después.

## 13. Relación entre accesos e índices

| Consulta/uso | Predicados principales | Índice existente | Cobertura aparente | Revisar |
|---|---|---|---|---|
| Atribuir marca | equipo_id, id_marcador, fecha dentro de vigencia | equipo+id+inicio+fin | Alta para prefijo equipo+id | Solapamientos históricos y plan SQLite. |
| Leer marcas para cálculo/importación | equipo_id, id_marcador IN, fecha_hora por rango | unique equipo+id+fecha_hora | Alta | Cardinalidad de listas IN. |
| Excluir cargas anuladas | importacion_id contra subconsulta por anulada | índice importacion.anulada; índice raw importacion+fecha | Parcial | Plan de NOT IN/subconsulta y proporción anulada. |
| Detectar archivo previo | hash_contenido | índice hash no único | Alta para búsqueda | Cardinalidad de hashes repetidos. |
| Cobertura por carga | importacion_id, MIN/MAX fecha_hora | importacion_id+fecha_hora | Alta | Confirmar plan. |
| Turno del trabajador | trabajador_id y fecha entre inicio/fin | trabajador+inicio+fin | Alta por trabajador | Selectividad de fecha_fin. |
| Versión del turno | turno_id, inicio/fin, activo | turno+fecha_inicio; unique abierta | Parcial para intervalos históricos | Falta fecha_fin en índice compuesto. Medir antes de cambiar. |
| Programación de cuadrilla | cuadrilla_id y rango | cuadrilla+inicio+fin | Alta por cuadrilla | Ausencia de unique abierto es integridad, no cobertura. |
| Justificación diaria | trabajador_id + fecha + estado | unique filtrado trabajador+fecha; estado | Alta para activas | Cómo usa SQLite el filtro de estados. |
| Upsert de tareo | trabajador_id + fecha | unique trabajador+fecha | Completa | — |
| Reporte por periodo/estado | periodo_id + estado | periodo+estado | Completa | Proyecciones y volumen real. |
| Bandeja de incidencias | periodo_id + requiere_validacion=1 | índice filtrado | Alta | Confirmar que el SQL coincide con el filtro. |
| Reintegro | trabajador_id + periodo_id | unique compuesto | Completa | — |
| Trabajadores por empresa/activo | empresa_id + activo | compuesto | Alta | — |
| Trabajadores por planta/área/sección | planta_id + area_id + seccion_id | compuesto | Parcial cuando solo filtra por centro/puesto/fechas | Plan y cardinalidades. |
| Búsqueda textual de trabajador | UPPER(campos) CONTAINS | No hay índice funcional/FTS | Baja aparente | Requiere medición y plan; no se declara problema. |
| Reporte histórico de área | trabajador_id y rango | trabajador+inicio+fin | Alta por trabajador | Solapamientos reales. |
| Tasas/parámetros vigentes | concepto/tipo + rango | concepto/tipo+desde+hasta | Alta | Solapamientos normativos. |

No se propone ningún índice en esta fase.

## 14. Candidatos de deuda técnica con evidencia

| ID | Hallazgo | Evidencia | Impacto potencial | Estado |
|---|---|---|---|---|
| P3A-01 | IDs organizativos de Trabajador sin FK | Snapshot no declara relaciones para siete campos de catálogo | Huérfanos y filtros/reportes incoherentes por escritura directa | CONFIRMADO |
| P3A-02 | TareoDiario y ReintegroTareo sin FKs de negocio | Solo tienen IDs escalares y uniques | Filas con trabajador/periodo/turno inexistentes | CONFIRMADO |
| P3A-03 | Solicitud y Justificacion tienen referencias parciales | Solicitud solo tiene FK a tipo; Justificacion solo a solicitud | Huérfanos de trabajador, periodo o tipo | CONFIRMADO |
| P3A-04 | Índices abiertos no excluyen solapamientos cerrados | Configuraciones temporales y snapshot | Ambigüedad histórica si se escribe fuera de servicios | CONFIRMADO |
| P3A-05 | PeriodoLaboral y ProgramacionCuadrilla sin unique abierto | Solo índices no únicos | Varias líneas vigentes simultáneas | CONFIRMADO |
| P3A-06 | Pares actual/histórico sin constraint de igualdad | Trabajador/PeriodoLaboral, Area/AsignacionArea, Turno/VersionTurno | Divergencia si un escritor elude sincronización | CANDIDATO |
| P3A-07 | Planta duplicada en Marcador y Equipo sin coherencia física | Dos FKs independientes | Filtrado por planta distinto al origen físico | CONFIRMADO |
| P3A-08 | Cabecera y marcas se guardan en dos transacciones implícitas | Dos SaveChanges separados en ServicioImportacion | Cabecera parcial si falla la segunda escritura | CANDIDATO |
| P3A-09 | Hash de importación no es único | Índice simple y lógica de servicio | Cabeceras repetidas; las marcas exactas siguen protegidas | CONFIRMADO |
| P3A-10 | Estado real de migraciones y anomalías existentes desconocido | No se abrió BD canónica | Riesgo no cuantificable | REQUIERE DATOS |

No se propone refactor ni corrección en esta fase.

## 15. REQUIERE BD CANÓNICA

Los siguientes puntos no pueden confirmarse desde código y migraciones:

- Migraciones realmente aplicadas en __EFMigrationsHistory.
- Estado de PRAGMA foreign_keys, journal_mode, integrity_check e índices.
- Existencia de filas huérfanas en IDs sin FK.
- Cantidad de trabajadores cuya ficha difiere de PeriodoLaboral.
- Divergencias entre AreaId y AsignacionArea, Turno y VersionTurno, o planta de marcador/equipo.
- Múltiples vínculos/programaciones abiertas.
- Solapamientos cerrados en las siete líneas auditadas.
- Justificaciones activas duplicadas anteriores a la restricción.
- Importaciones con hash repetido o cabeceras sin marcas.
- Duplicados o inconsistencias anteriores a las claves naturales actuales.
- Cardinalidades, distribución por fechas/equipos/plantas y crecimiento.
- Planes SQLite para consultas críticas.
- Duración, memoria y viajes SQL reales.
- Bases que hayan fallado a mitad de una migración o backfill.

Estas verificaciones requieren una copia canónica anonimizada o autorización explícita para trabajar con una copia controlada. No deben ejecutarse sobre una base productiva arbitraria.

## 16. Requerimiento de rotación confirmado

La regla de negocio queda fijada para diseño posterior:

14 días DIURNO → 1 día de DESCANSO MÓVIL → 7 días NOCTURNO → repetir.

El día de descanso pertenece al ciclo móvil de 22 días y no es un domingo fijo. El esquema actual obliga a que cada TramoRotacion tenga TurnoId, por lo que conserva el gap conceptual confirmado en Fase 2. No se diseñó ni implementó solución.

## 17. Cierre

La comprensión estimada sube de 62 % a 76 %. Ya se conoce cómo EF materializa el dominio, cuáles relaciones son físicas, qué invariantes viven solo en aplicación, cómo evolucionó el esquema y qué consultas deben medirse.

Siguiente fase recomendada: Fase 3B — validación contra una copia canónica controlada, integridad de datos, migraciones aplicadas, PRAGMA, planes SQLite y mediciones. Debe iniciarse únicamente cuando el usuario identifique o proporcione esa copia.

La Fase 3B no se ejecutó.

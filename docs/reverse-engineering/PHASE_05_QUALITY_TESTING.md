# Fase 5 — Especificación verificable, testing y calidad operativa

Fecha: 2026-10-05  
Comprensión consolidada: **96 %**  
Estado: fase completada con defectos reproducidos; no se implementaron correcciones.

## 1. Resultado ejecutivo

La línea base heredada estaba verde: **677/677** pruebas. Se agregaron 17 casos de aceptación y 7 mediciones sintéticas. El corte final contiene **701 pruebas: 687 pasan, 14 fallan, 0 omitidas**. Los fallos son evidencia reproducible de horas extra, tardanza, NH-01, NH-02 y NH-03; no se cambiaron expectativas ni producción para hacerlos pasar.

Bloqueos para considerar el sistema apto para liquidación sin control manual:

- **P0**: cálculo de horas extra contrario a la regla confirmada.
- **P0**: la tolerancia configurada de 5 minutos perdona tardanza que planilla exige contar desde 07:01.
- **P0**: PMV permite acceso cruzado entre plantas a un Administrador.
- **P1**: liquidación duplica bases nocturnas y de descanso trabajado.
- **P1**: cada edición puede crear otro `PeriodoLaboral` y el cese no cierra el existente.
- **P1**: una justificación parcial puede ocultar un día incompleto de la bandeja.
- **P1**: la vigencia puede declarar “al día” tras borrados o recálculos parciales.

## 2. Ejecución de pruebas

### Línea base antes de agregar aceptación

| Ejecución | Total | Pasan | Fallan | Omitidas | Duración reportada / pared |
|---|---:|---:|---:|---:|---|
| Domain | 289 | 289 | 0 | 0 | 577 ms / 8.47 s |
| Infrastructure | 388 | 388 | 0 | 0 | 18 s / 26.29 s |
| Solución global | 677 | 677 | 0 | 0 | Domain 461 ms + Infrastructure 11 s / 23.07 s |

### Corte final con contratos de Fase 5

| Ejecución | Total | Pasan | Fallan | Omitidas | Duración reportada / pared |
|---|---:|---:|---:|---:|---|
| Domain | 300 | 290 | 10 | 0 | 458 ms / 5.75 s |
| Infrastructure | 401 | 397 | 4 | 0 | 18 s / 23.19 s |
| Solución global | 701 | 687 | 14 | 0 | Domain 624 ms + Infrastructure 18 s / 28.10 s |

Comandos: `dotnet test <proyecto> --no-restore` para cada proyecto y `dotnet test ControlMarcacion.sln --no-restore` global.

## 3. Evidencia de defectos

| Hallazgo | Evidencia ejecutada | Observado | Clasificación |
|---|---|---|---|
| NH-07 | HE-01..05 | 480 ordinarios donde se esperan 622 o 0 según día | Bug/gap funcional confirmado · P0 |
| Tardanza | TAR-01..03 | 07:00=0 pasa; 07:01 y 07:05 devuelven 0 en vez de 1 y 5 | Bug/gap funcional confirmado · P0 |
| NH-01 | LIQ-03/04 | 18.80 vs 10.80; 24.00 vs 16.00 horas equivalentes | Bug confirmado · P1 |
| NH-02 | PL-01/02, limpiando tracking entre peticiones | 3 vínculos tras dos ediciones; cese nulo en vínculo | Bug confirmado · P1 |
| NH-03 | INC-01..03 | Permiso parcial, tardanza justificada y regularización parcial: `Incompleto`, `TipoIncidencia=null`, `RequiereValidacion=false` | Bug confirmado · P1 |
| NH-04 | lectura de algoritmo y borrado físico | `MAX` sobre filas sobrevivientes no observa eliminación | Bug confirmado por código · P1 |
| PMV-RN-012 | controlador/servicio | rol sí; alcance por planta ausente en listar/consultar/exportar/importar | Autorización rota · P0 |

## 4. Seguridad acción por acción

| Controller | Action | Autenticado | Rol | Planta | Estado |
|---|---|---|---|---|---|
| MarcacionesProvisionales | Index | Sí | Coordinador/Administrador | No filtra selector | CONTROL INSUFICIENTE |
| MarcacionesProvisionales | Importar | Sí | Coordinador/Administrador | No valida alcance del equipo | CONTROL INSUFICIENTE |
| MarcacionesProvisionales | Consulta | Sí | Coordinador/Administrador | No valida alcance del equipo | CONTROL INSUFICIENTE |
| MarcacionesProvisionales | Exportar | Sí | Coordinador/Administrador | No valida alcance del equipo | CONTROL INSUFICIENTE |
| Trabajadores | Index/Editar/Guardar/Historial/Baja | Sí | Coordinador/Administrador | Consultas/servicio acotados | CORRECTO; mantener prueba por ID directo |
| Marcaciones | Cargar/Resultado | Sí | Coordinador/Administrador | Servicio valida equipo/planta | REVISAR cobertura Web |
| Marcaciones | Anular/Reactivar | Sí | Solo Coordinador | Global por privilegio | CORRECTO |
| Asistencia | Index/Recalcular | Sí | Coordinador/Administrador | Proveedor de alcance | CORRECTO |
| Reportes | Consultar/Exportar | Sí | Coordinador/Gerencia | Política propia, no Administrador | REVISAR definición de alcance de Gerencia |
| Solicitudes | CRUD/Aprobar/Rechazar/Anular | Sí | Coordinador/Administrador/TrabajadorSocial | Servicio acota trabajador | REVISAR aprobación parcial |
| Reintegros | Editar/Guardar | Sí | Coordinador/Administrador | Servicio acota trabajador | CORRECTO |
| Configuración | Catálogos/Turnos/Cuadrillas/Equipos/Marcadores/Periodos | Sí | Coordinador/Administrador | Variable por servicio | REVISAR pruebas por ID directo |
| Auditoría/Usuarios/Feriados | Todas | Sí | Solo Coordinador | Global intencional | CORRECTO |
| Cuenta/Inicio | login/logout/inicio/error | Excepción intencional | Anónimo/autenticación | N/A | CORRECTO |

No existe proyecto de tests Web; por ello la autorización declarativa y los filtros de acción no tienen cobertura end-to-end.

## 5. Transacciones y concurrencia

- Importación principal: cabecera/errores y marcas se guardan en dos `SaveChanges` sin transacción envolvente. Riesgo de cabecera lógica incompleta.
- PMV: importación dentro de transacción explícita; hash duplicado se protege en aplicación, sin `UNIQUE`, por lo que queda carrera concurrente.
- Recálculo: upsert por `(trabajador, fecha)`, guardado de lote y guardia de versión del periodo; `CierreDePeriodoConcurrenteTests` cubre la carrera de cierre.
- Justificaciones: rutas críticas usan transacción explícita y versión optimista.
- Solicitudes: aprobación puede quedar parcial por días omitidos; el estado cabecera no expresa ese resultado.
- `PeriodoLaboral`: el problema no es una carrera sino una carga incompleta del agregado entre peticiones.

| Operación | Clasificación de concurrencia | Evidencia |
|---|---|---|
| Cierre vs recálculo | PROTEGIDO | versión del periodo + test concurrente |
| Resolver regularización | PROTEGIDO | versión optimista y transacción |
| Hash de importación principal | PARCIAL | comprobación de aplicación; hash no unique |
| Hash PMV | PARCIAL | transacción de escritura, pero hash no unique |
| Programación/cuadrilla | PARCIAL | validaciones de vigencia; sin token explícito observado |
| Edición de trabajador | NO PROTEGIDO frente a last-write-wins | sin token de versión; además NH-02 |
| Lecturas/reportes | NO NECESARIO para consistencia fuerte, pero pueden quedar obsoletos | alerta de vigencia parcial |

## 6. Observabilidad, hardcodes y mantenibilidad

| Tema | Estado |
|---|---|
| Logging | Recálculo tiene log agregado; faltan métricas uniformes de duración/filas/resultado en importación y exportación. |
| Auditoría | Interceptor excluye tareo derivado; borrados quedan auditados, pero la alerta de vigencia no consume esa auditoría. |
| 480 | Semillas, comentarios y pruebas; contradice corte fijo 17:22. |
| 07:00 / 17:22 | 07:00 existe en turnos; 17:22 no es fuente del motor. Deben ser horario teórico/versionado, no literales duplicados. |
| 360 / 6 h | Margen fijo posterior al turno en `ServicioCalculoTareo`; sin configuración ni test de frontera directo. |
| 22:00 / 06:00 | Franja nocturna fija y confirmada; mantener centralizada aunque no necesariamente configurable. |
| 5 min | Semilla operacional; el campo es configurable por turno. |
| 20 MB | Límite de archivo en capa Web; necesita prueba de frontera/413. |
| 30/40 días | PMV advierte >30 y bloquea exportación >40; faltan fronteras exactas y coherencia consulta/exportación. |
| Duplicación | PMV reutiliza parser, pero duplica rama de persistencia/consulta/exportación por diseño temporal. |
| Código muerto | CONFIRMADO SIN USO: ninguno demostrado. POSIBLEMENTE SIN USO: rutas históricas/documentales, requieren análisis de referencias. EN USO: detector de turno. PMV es temporal pero activo. |
| Duplicación | REAL: políticas de rango PMV repartidas entre consulta/controlador/vista. REPRESENTACIÓN NECESARIA: validación en backend y ocultamiento del botón en frontend. |

### Logging y manejo de errores

| Flujo | Logs/excepciones observados | Evaluación |
|---|---|---|
| Importación principal | Errores por fila se persisten; excepciones de negocio llegan a UI; sin métrica uniforme de duración | PARCIAL |
| Recálculo/tareo | `ILogger` y resumen agregado; errores de periodo se hacen visibles | MEJOR CUBIERTO |
| Reportes/exportación | Excepciones explícitas ante parámetros faltantes; sin telemetría de tamaño/duración | PARCIAL |
| PMV | Errores se persisten y transacción revierte; servicio no registra métricas ni intentos cruzados de planta | INSUFICIENTE |
| Solicitudes/justificaciones | Excepciones de negocio explícitas; concurrencia controlada en resoluciones | PARCIAL |
| `catch` vacío/excepción tragada | No se confirmó ninguno en los flujos dirigidos | SIN HALLAZGO CONFIRMADO |

## 7. Baseline sintético de rendimiento

El entorno no contiene un dataset representativo del flujo principal y la base canónica no se modificó. Se ejecutó un harness SQLite temporal para PMV. Son cifras **SINTÉTICAS**, una sola corrida y no constituyen SLA.

| Escenario requerido | Estado | Métricas disponibles | Decisión |
|---|---|---|---|
| Importación PMV 1k | SINTÉTICO | 295.56 ms; delta memoria 6,334,744 B; 1,000 filas | Sin conteo SQL instrumentado |
| Importación PMV 20k | SINTÉTICO | 5,187.13 ms; delta memoria 65,656,336 B; 20,000 filas | Sin conteo SQL instrumentado |
| Tareo 1 día / 5 días / mes | PENDIENTE | Tests funcionales, no benchmark | Medir con 1, 100 y padrón esperado; capturar SQL y asignaciones |
| Exportación principal | PENDIENTE | Cobertura funcional existente | Medir bytes, filas, memoria pico y duración |
| PMV consulta 1 día | SINTÉTICO frío | 127.17 ms; 831,488 B; 1 jornada; JSON 585 B | Incluye calentamiento inicial |
| PMV consulta 30 días | SINTÉTICO | 1.65 ms; 139,784 B; 30 jornadas; JSON 14,709 B | 60 marcas; sin advertencia |
| PMV consulta 31 días | SINTÉTICO | 6.72 ms; 139,784 B; 31 jornadas; JSON 15,195 B | advertencia activa |
| PMV consulta 40 días | SINTÉTICO | 2.00 ms; 156,232 B; 40 jornadas; JSON 19,578 B | exportación aún permitida |
| PMV consulta 41 días | SINTÉTICO | 4.57 ms; 156,232 B; 41 jornadas; JSON 20,064 B | flag de bloqueo de exportación activo |

Protocolo propuesto: proceso frío y caliente, 3 repeticiones, mediana/p95, `GC.GetTotalMemory`, contador EF de comandos, filas de entrada/salida y tamaño de XLSX. Los números deben etiquetarse “SINTÉTICO” y no usarse como SLA de producción.

## 8. DOCUMENTATION_DRIFT

- `CLAUDE.md` y varios comentarios sostienen 480 minutos/horas extra desde la duración; la regla confirmada fija 17:22 y sábado completo como extra.
- `CLAUDE.md`, semillas y tests antiguos toleran 5 minutos; negocio confirma tolerancia de planilla 0.
- `TareoDiario` dice “hasta 480”; no representa la regla vigente.
- Documentación antigua conserva tres hojas y AO–BF; el exportador vigente usa cuatro hojas y A–BE.
- `CLAUDE.md` afirma ausencia general de alcance por planta, pero el alcance sí existe en módulos principales; PMV es la excepción real.
- Las fases anteriores se conservan como registro histórico. Esta fase no las reescribe; la reconciliación vigente queda en estos artefactos.

## 9. Cobertura consolidada

El conteo correcto de Fase 4 era 83/14/11 al leer la columna Cobertura, no al buscar palabras en toda la fila. Tras reclasificar RN-055/RN-088 y agregar RN-090: **82 DIRECTO, 16 PARCIAL, 10 NINGUNO**. La matriz y los nombres de método están en `ACCEPTANCE_RULES.md`.

## 10. Rotación y tardanza

Rotación:

- Comportamiento actual: ciclos de tramos configurables, cada tramo exige un turno.
- Capacidad: duraciones, secuencias y cambios futuros variables; resolución modular respecto del ancla.
- Gap: no representa naturalmente un descanso móvil como tramo sin turno.
- Supuesto descartado: 14+1+7 no se impone como repetición universal; es un patrón observado.

Tardanza:

- Regla de negocio: tolerancia de planilla 0; 07:01=1 minuto y 07:05=5.
- Comportamiento actual: usa `Turno.MinutosTolerancia`; semilla 5; al excederla cuenta desde la hora teórica.
- Diferencia: TAR-02/03 fallan porque la tolerancia operativa se aplica al descuento de planilla.

## 11. Preguntas abiertas que bloquean diseño

1. ¿Los 622 minutos de 07:00–17:22 incluyen refrigerio pagado y aplican igual a todos los turnos diurnos?
2. ¿El domingo debe etiquetarse contablemente como descanso semanal o feriado, aunque ambos tengan hoy 100 %?
3. ¿La prima nocturna se calcula por minuto, por día o contra una base salarial externa?
4. ¿La alerta de vigencia debe bloquear exportación o solo advertir?
5. ¿Qué fila sobrevive en cada grupo duplicado de `PeriodoLaboral` y cómo se documenta la reparación?
6. ¿Los periodos cerrados deben ser snapshots inmutables de organización o reemisiones con maestro actual?
7. ¿Se permiten múltiples decisiones/justificaciones por día o una composición explícita dentro de una sola entidad?

## 12. Recomendación

No usar `LiquidarPeriodoAsync`, el reparto actual de horas extra ni la tardanza con tolerancia configurada como fuente automática de pago hasta cerrar los P0. Restringir PMV a Coordinador o deshabilitarlo para Administrador hasta implementar alcance por planta. Corregir después `PeriodoLaboral`, incidencias y vigencia antes de ampliar la automatización de cierres y exportaciones.

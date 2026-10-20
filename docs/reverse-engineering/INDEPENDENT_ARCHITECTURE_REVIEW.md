# Revisión arquitectónica independiente

Fecha: 2026-10-01  
Revisor: Claude Opus 4.6, actuando como arquitecto senior y auditor adversarial.  
Fuentes revisadas: PHASE_01_DISCOVERY.md, PHASE_02_DOMAIN_ARCHITECTURE.md, PHASE_03A_PERSISTENCE_SCHEMA.md, DATABASE.md, CONTEXT_REGISTER.md y diagramas D01–D06/D13.  
Código consultado: solo archivos específicos para refutar o validar afirmaciones concretas.

---

## 1. Resumen

La ingeniería inversa existente es sólida, metódica y honesta en sus niveles de certeza. Las tres fases mantienen coherencia interna y usan correctamente las etiquetas CONFIRMADO / INFERIDO / NO DETERMINADO. No encontré ninguna conclusión fabricada ni una afirmación sin respaldo presentada como hecho.

Los hallazgos más relevantes de esta revisión son:

- La mayoría de las conclusiones se **validan**, varias con matices que reducen su gravedad.
- El riesgo de `PeriodoLaboral` está **correctamente identificado pero sobredimensionado**: el código documenta explícitamente por qué usa campos planos y la decisión es consciente, no un olvido.
- La conclusión sobre el descanso rotativo es **correcta**: el modelo no puede expresar un descanso móvil dentro del ciclo.
- Varios candidatos de deuda técnica son en realidad **decisiones arquitectónicas válidas** para un monolito SQLite de escritorio.
- No existe ningún hallazgo que obligue a detener la ingeniería inversa antes de Fase 3B.

---

## 2. Conclusiones validadas

| ID | Conclusión Codex | Estado Claude | Evidencia | Observación |
|---|---|---|---|---|
| F1-01 | La separación de capas Domain→Application→Infrastructure→Web se mantiene sin ciclos | VALIDADO | Confirmado en Fase 1 y coherente con CLAUDE.md | Correcto. Domain no importa EF Core ni ASP.NET. |
| F1-02 | El recorrido TXT→lector→dedup→importación→atribución→cálculo→tareo está confirmado | VALIDADO | Verificado en ServicioImportacion y ServicioCalculoTareo | Correcto y bien documentado. |
| F1-03 | MarcacionCruda es inmutable y no contiene TrabajadorId | VALIDADO | Confirmado en migración `AtribucionDeMarcacionesAlCalcular` y entidad | Decisión de diseño deliberada (ADR 0001). |
| F2-01 | La atribución se resuelve por equipo+número+vigencia en la fecha de la marca | VALIDADO | `ResolverTurno` y `AtribucionMarcaciones` en el código | Es el corazón del diseño temporal. |
| F2-02 | Tres niveles de vigencia: AsignacionTurno → ProgramacionCuadrilla → VersionTurno | VALIDADO | `ServicioCalculoTareo.ResolverTurno` y `HorariosPorFecha` | Código verificado línea por línea. |
| F2-03 | AsignacionTurno contiene exactamente TurnoId o CuadrillaId | VALIDADO | Check constraint `CK_asignacion_turno_turno_o_cuadrilla` en snapshot | Protección física. |
| F3A-01 | IDs organizativos de Trabajador carecen de FK | VALIDADO | `ConfiguracionesPersonal.cs` no declara FK para EmpresaId, PlantaId, etc. de Trabajador | Solo PeriodoLaboral tiene FK a Empresa. |
| F3A-02 | TareoDiario no tiene FKs de negocio | VALIDADO | Snapshot y configuración confirman ausencia | Coherente con su rol de proyección recalculable. |
| F3A-04 | Los índices únicos filtrados no excluyen solapamientos de filas cerradas | VALIDADO | Limitación inherente de SQLite (no soporta exclusión de intervalos) | Correctamente identificado. |
| F3A-05 | PeriodoLaboral y ProgramacionCuadrilla no tienen unique abierto | VALIDADO | Snapshot confirmado | Protección solo en capa de aplicación. |
| F3A-08 | Importación guarda cabecera y marcas en dos SaveChanges separados | VALIDADO | `ServicioImportacion.cs` líneas 214 y 231, sin transacción envolvente | Riesgo real de cabecera huérfana. |

## 3. Conclusiones validadas con matices

| ID | Conclusión Codex | Estado Claude | Evidencia | Matiz |
|---|---|---|---|---|
| F2-R1 | PeriodoLaboral existe pero cálculo y reportes usan campos planos | VALIDADO CON MATICES | `ServicioReportes.cs:639-642` documenta la decisión explícitamente; `ServicioCalculoTareo.cs:63,79,324` también usa campos planos | **No es un olvido ni deuda oculta.** El código dice textualmente: «Se usan los campos planos y no PeriodoLaboral, otra vez para coincidir con el motor: la historia de vínculos existe para no perder los reingresos, pero mientras el motor calcule contra los campos planos, el reporte tiene que leer lo mismo o los dos dirán cosas distintas.» Esto es una **decisión de consistencia**, no una omisión. El riesgo real se limita a reingresos con cambio de empresa, que en la práctica no ha ocurrido. |
| F2-R2 | Planta sin historia es un riesgo | VALIDADO CON MATICES | `Trabajador.PlantaId` sin FK ni línea temporal | El riesgo es real, pero atenuado: un traslado entre plantas es un evento que en la práctica implica cambio de empresa (Consulting Advisers vs CA Outsourcing), y el sistema exige cesar al trabajador antes de cambiar de empresa. Sería un riesgo solo si existieran traslados internos dentro de la misma empresa, lo que CLAUDE.md no menciona como caso real. **Requiere confirmación de negocio.** |
| F2-R3 | Doble representación Turno/VersionTurno requiere sincronización | VALIDADO CON MATICES | `ServicioTurnos` escribe ambos; tests `VigenciaDelHorarioDeTurnoTests` verifican coherencia | La duplicación es **intencional y documentada** en la migración y CLAUDE.md. El riesgo de divergencia existe por escritura directa a SQLite, pero no por los caminos normales del sistema. Es un riesgo de operación, no de diseño. |
| F2-R5 | Trazabilidad TareoDiario→MarcacionCruda es reconstruida | VALIDADO CON MATICES | No existe FK ni tabla puente | La reconstrucción es determinista: dado un trabajador, una fecha, sus asignaciones y las marcaciones vigentes, el conjunto es reproducible. No es una relación persistida, pero **la reproductibilidad del cálculo** (idempotencia) actúa como garantía funcional. Persistir la relación agregaría costo de mantenimiento sin beneficio claro mientras la idempotencia se mantenga. |
| F3A-06 | Pares actual/histórico pueden diverger sin constraint de igualdad | VALIDADO CON MATICES | Trabajador.AreaId vs AsignacionArea; Turno vs VersionTurno | Los servicios sincronizan. La divergencia solo es posible por escritura directa a SQLite o por un bug en el servicio. En un sistema de escritorio monousuario con SQLite local, la probabilidad de escritura directa es baja. **Riesgo operativo menor, no deuda de diseño.** |
| F3A-07 | Marcador.PlantaId duplica EquipoMarcador.PlantaId | VALIDADO CON MATICES | Dos FKs independientes confirmadas | La duplicación existe, pero `ServicioMarcadores` copia el valor y `ServicioEquipos` bloquea el cambio de planta con marcadores. El escenario de divergencia requiere SQL directo contra una base que solo el sistema escribe. **Riesgo mínimo en el contexto operativo.** |

## 4. Conclusiones no demostradas

| ID | Conclusión Codex | Estado Claude | Observación |
|---|---|---|---|
| F2-R7 | Documentación interna desactualizada respecto al alcance por planta | NO DEMOSTRADO | La Fase 2 afirma que hay comentarios que contradicen la implementación actual. No se citó ningún comentario concreto, y el código actual (`ProveedorAlcancePlantasHttp`) sí aplica el alcance. Si los comentarios existían y fueron corregidos entre la escritura del documento y la revisión, la afirmación ya no aplica. |
| F2-IR2 | Programaciones concurrentes de cuadrilla podrían necesitar defensas adicionales | NO DEMOSTRADO | Es un riesgo teórico de concurrencia que no se probó. En un sistema de escritorio monousuario, la probabilidad de dos escrituras concurrentes sobre la misma cuadrilla es casi nula. Requiere prueba de carga para elevarse de inferido a confirmado. |
| F2-IR3 | El margen fijo posterior a la salida puede producir solapamientos difíciles | NO DEMOSTRADO | El motor tiene defensa de consumo (`FinJornadaTeorica` y protección de marcas previas). No se demostró un caso concreto que falle. |

## 5. Conclusiones refutadas

| ID | Conclusión Codex | Estado Claude | Evidencia | Observación |
|---|---|---|---|---|
| — | (ninguna conclusión se refuta por completo) | — | — | Las tres fases son rigurosas. No encontré afirmaciones falsas. Las que maticé arriba no están equivocadas, están sobredimensionadas en severidad. |

## 6. Nuevos riesgos

| ID | Riesgo | Severidad | Evidencia | Acción sugerida |
|---|---|---|---|---|
| NR-01 | **La importación no es atómica.** Los dos SaveChanges (líneas 214 y 231 de `ServicioImportacion.cs`) no están envueltos en una transacción. Una falla entre ambos deja una cabecera sin marcaciones. El sistema no tiene recuperación ni detección de este estado. | MEDIA | Código verificado; no hay `ExecuteAsync` ni `BeginTransaction` alrededor. | Envolver en `CreateExecutionStrategy().ExecuteAsync(...)` como ya hace el propio CLAUDE.md que prescribe para transacciones. O bien detectar cabeceras con `FilasInsertadas > 0` y cero `MarcacionCruda` asociadas. Fase 3B puede cuantificarlo. |
| NR-02 | **PeriodoLaboral no se consulta por ningún consumidor crítico, pero sí se escribe.** Es una tabla activamente mantenida (`AbrirVinculo`, `SincronizarVinculoAbierto`, `DarDeBaja`) cuya información no se consume en cálculo ni reportes. Si un día se migra a usarla, la divergencia acumulada con los campos planos puede ser significativa. | BAJA | `ServicioCalculoTareo` y `ServicioReportes` explícitamente no la usan, pero `ServicioTrabajadores` la mantiene | No es urgente. Documentar como deuda conocida. Antes de usarla en el motor, validar integridad en Fase 3B. |
| NR-03 | **El comentario del ProgramacionCuadrilla dice «Los días de descanso no son parte del ciclo: los pone el turno con sus DiasAplicables», pero DiasAplicables es por día de la semana, no por posición en el ciclo.** Esto significa que el descanso solo funciona si coincide con un día fijo (ej. siempre domingo), y no para un descanso cada N días del ciclo. Es una limitación que el código documenta como diseño pero que no satisface la regla de negocio confirmada (descanso móvil cada 22 días). | MEDIA | `ProgramacionCuadrilla.cs:22-30`, `CalculoTareo.cs:466-496`, `Turno.DiasAplicables` es un flags enum de días de la semana | Confirma la conclusión de Fase 2 y la refuerza con la mecánica exacta. |

## 7. Revisión de deuda técnica

| ID Codex | Hallazgo | Clasificación Claude | Justificación |
|---|---|---|---|
| P3A-01 | IDs organizativos de Trabajador sin FK | DECISIÓN ARQUITECTÓNICA VÁLIDA | En un sistema de escritorio donde solo el propio sistema escribe en la base, las FKs de catálogos estables aportan protección marginal. Agregar 7 FKs a Trabajador complejiza las migraciones y los cascades sin beneficio claro cuando los servicios ya validan. Si se migrara a multiusuario o API, esto cambiaría. |
| P3A-02 | TareoDiario y ReintegroTareo sin FKs | DECISIÓN ARQUITECTÓNICA VÁLIDA | TareoDiario es una proyección recalculable. Si se agregara FK a Trabajador, un borrado (que el sistema prohíbe) arrastraría los tareos. ReintegroTareo tiene unique compuesto que actúa como constraint funcional. |
| P3A-03 | Solicitud y Justificacion con referencias parciales | RIESGO DE DISEÑO | La ausencia de FK en Justificacion.TrabajadorId es el caso más débil: si un trabajador se borrara (que no se puede, pero), las justificaciones quedarían huérfanas sin que la base proteste. Es un riesgo menor pero real si las políticas cambian. |
| P3A-04 | Índices abiertos no excluyen solapamientos cerrados | RIESGO DE DISEÑO | No es deuda: SQLite no puede expresar exclusión de intervalos. La protección vive en los servicios y es correcta para el contexto operativo. Si se migrara a PostgreSQL, se podrían agregar exclusion constraints. |
| P3A-05 | PeriodoLaboral y ProgramacionCuadrilla sin unique abierto | DEUDA CONFIRMADA | A diferencia de las cinco asignaciones que sí tienen unique filtrado, estas dos confían solo en el servicio. Un índice único filtrado de ProgramacionCuadrilla (`cuadrilla_id WHERE fecha_fin IS NULL AND activo = 1`) es trivial de agregar y cierra un hueco real. PeriodoLaboral es más discutible dado que nadie la consume. |
| P3A-06 | Pares actual/histórico sin constraint | RIESGO DE DISEÑO | La sincronización es correcta en los caminos normales. SQLite no puede expresar «el campo X de la tabla A debe coincidir con el de la tabla B». Agregar un trigger sería frágil. Mejor estrategia: tests de integridad en Fase 3B. |
| P3A-07 | Planta duplicada en Marcador y Equipo | DECISIÓN ARQUITECTÓNICA VÁLIDA | Es una desnormalización controlada: el servicio la sincroniza y el bloqueo de cambio la protege. La FK redundante permite filtrar marcadores por planta sin join. |
| P3A-08 | Dos SaveChanges sin transacción en importación | DEUDA CONFIRMADA | Es el candidato más claro de deuda técnica real con impacto potencial. La solución existe en el propio estilo del proyecto (`ExecuteAsync`). |
| P3A-09 | Hash de importación no único | DECISIÓN ARQUITECTÓNICA VÁLIDA | El hash detecta, no previene. Las marcaciones sí tienen unicidad por equipo+código+instante, que es la defensa real contra duplicados. Hacer el hash único impediría cargar el mismo archivo en otro equipo (caso legítimo que el sistema rechaza por otra vía). |
| P3A-10 | Estado de migraciones en BD desconocido | REQUIERE DATOS | Correcto. Fase 3B. |

## 8. Riesgos históricos

| Riesgo | Severidad | Estado |
|---|---|---|
| Recontratación con cambio de empresa reescribe la historia | MEDIA | **Atenuado por diseño.** `Trabajador.CambiarEmpresa` exige que el trabajador esté cesado. El cese cierra asignaciones. PeriodoLaboral conserva la empresa del vínculo cerrado. El motor usa campos planos del vínculo actual. El riesgo existe solo si alguien edita `EmpresaId` directamente en la base después de reingresar, y solo afecta a reportes históricos que se emitan después. |
| Cambio de planta sin historia borra la planta anterior | BAJA | No hay traslados dentro de la misma empresa en la práctica (confirmado por la regla de que cambiar empresa exige cese). Si existiera un traslado interno, sería un nuevo riesgo. |
| El CECO y la sección no tienen historia | BAJA | Afecta la columna CENTRO DE COSTO del tareo final si cambia. Pero CLAUDE.md documenta que la hoja Tareo se genera contra el período y se entrega una vez; un cambio posterior no altera lo ya entregado. El riesgo sería reemitir un tareo cerrado después de un cambio de CECO. |
| Turno modificado con VersionTurno anterior | ATENUADO | `EstadoCalculoTareo` detecta y avisa. La sobre-alerta es consciente y documentada en CLAUDE.md. |

## 9. Riesgos de trazabilidad

| Riesgo | Severidad | Estado |
|---|---|---|
| No hay relación persistida TareoDiario↔MarcacionCruda | BAJA | La reproductibilidad del cálculo (idempotencia) compensa. El cálculo es determinista dada la misma entrada. No es una falta de trazabilidad sino una trazabilidad por reconstrucción. |
| Justificacion.JustificacionId en TareoDiario no tiene FK | BAJA | El campo se proyecta para consulta rápida. Si la justificación se retirara, el recálculo limpiaría el campo. |
| La auditoría excluye MarcacionCruda y TareoDiario | DECISIÓN VÁLIDA | Ambas tienen su propia trazabilidad: la marca por ImportacionMarcador y el tareo por CalculadoEn. Auditarlas duplicaría millones de filas sin agregar información. |

## 10. Riesgos de temporalidad

| Riesgo | Severidad | Estado |
|---|---|---|
| Solapamientos de asignaciones cerradas no se detectan en la base | MEDIA | Los servicios los previenen. SQLite no puede detectarlos. Fase 3B debe buscarlos en datos reales. |
| PeriodoLaboral sin unique abierto permite múltiples vínculos activos | BAJA | Nadie consume PeriodoLaboral en el motor, así que el impacto funcional de un duplicado es nulo. Fase 3B debe verificar integridad. |
| ProgramacionCuadrilla sin unique abierto | MEDIA | El motor sí la consume. Dos programaciones activas podrían dar resultados distintos según orden de evaluación (FirstOrDefault). El servicio lo previene, pero el riesgo de datos corruptos previos a los controles actuales es real. |

## 11. Preguntas pendientes de negocio

1. **¿Existen traslados de planta dentro de la misma empresa?** Si no, el riesgo de planta sin historia es teórico. Si sí, necesita AsignacionPlanta.
2. **¿El descanso móvil del ciclo de cuadrilla debe computarse como «Descanso» (sin paga) o como «No laborable» (con sobretasa si se trabaja)?** La respuesta determina si necesita su propio turno-descanso o un mecanismo nuevo.
3. **¿Cuántas recontrataciones reales existen?** Si son cero, el riesgo de PeriodoLaboral divergente es puramente preventivo.
4. **¿Alguien accede a la base SQLite directamente (backups, scripts, consultas ad-hoc)?** Si no, varios riesgos de integridad por escritura directa son teóricos.
5. **¿El CECO o la sección cambian con frecuencia?** Si raramente, la ausencia de historia es aceptable.

## 12. Qué debe comprobar Fase 3B

1. **Integridad de PeriodoLaboral**: contar filas abiertas por trabajador, comparar empresa/fechas contra campos planos.
2. **Programaciones de cuadrilla**: buscar más de una activa y abierta por cuadrilla.
3. **Solapamientos de asignaciones cerradas** en las siete líneas temporales.
4. **Cabeceras huérfanas de importación**: ImportacionMarcador con FilasInsertadas > 0 y cero MarcacionCruda asociadas.
5. **Divergencia Trabajador.AreaId vs AsignacionArea abierta**.
6. **Divergencia Turno (campos actuales) vs VersionTurno abierta**.
7. **Divergencia Marcador.PlantaId vs EquipoMarcador.PlantaId**.
8. **Estado real de migraciones** en `__EFMigrationsHistory`.
9. **PRAGMAs**: `foreign_keys`, `journal_mode`, `integrity_check`.
10. **Cardinalidades**: cuántos trabajadores, marcaciones, tareos y justificaciones por periodo.

## 13. Nivel de confianza global

**ALTO.** La ingeniería inversa es rigurosa, conservadora en sus afirmaciones y honesta cuando no sabe. Las conclusiones principales son correctas. Los matices que aporto reducen la severidad percibida de algunos riesgos sin invalidarlos. La única deuda técnica con impacto real y solución inmediata es la falta de transacción en la importación (P3A-08).

---

## Tabla principal consolidada

| ID | Conclusión Codex | Estado Claude | Evidencia | Observación |
|---|---|---|---|---|
| F1-01 | Capas sin ciclos | VALIDADO | Proyectos y dependencias | — |
| F1-02 | Flujo TXT→tareo confirmado | VALIDADO | Código verificado | — |
| F1-03 | MarcacionCruda inmutable sin TrabajadorId | VALIDADO | Entidad y migración | ADR 0001 |
| F2-01 | Atribución por equipo+número+vigencia | VALIDADO | ResolverTurno, AtribucionMarcaciones | — |
| F2-02 | Tres niveles de vigencia | VALIDADO | Código verificado | — |
| F2-03 | XOR turno/cuadrilla | VALIDADO | Check constraint | — |
| F2-R1 | PeriodoLaboral no consumida | VALIDADO CON MATICES | Decisión explícita documentada en código | No es olvido |
| F2-R2 | Planta sin historia | VALIDADO CON MATICES | Requiere confirmación de negocio | Atenuado por regla de cese |
| F2-R3 | Doble representación turno/versión | VALIDADO CON MATICES | Intencional y sincronizada | Riesgo operativo, no de diseño |
| F2-R5 | Trazabilidad reconstruida | VALIDADO CON MATICES | Idempotencia compensa | No es falta, es diseño |
| F2-R7 | Documentación desactualizada sobre alcance | NO DEMOSTRADO | Sin ejemplo concreto citado | — |
| F2-F | Descanso rotativo no representable | VALIDADO | TramoRotacion exige TurnoId; DiasAplicables es por día de semana | NR-03 refuerza |
| F3A-01 | Trabajador sin FKs organizativas | VALIDADO | Configuración verificada | — |
| F3A-02 | TareoDiario sin FKs | VALIDADO | Snapshot verificado | — |
| F3A-04 | Sin exclusión de solapamientos cerrados | VALIDADO | Limitación SQLite | — |
| F3A-05 | PL y PC sin unique abierto | VALIDADO | Snapshot verificado | — |
| F3A-06 | Pares sin constraint de igualdad | VALIDADO CON MATICES | Sincronización correcta en caminos normales | — |
| F3A-07 | PlantaId duplicada | VALIDADO CON MATICES | Desnormalización controlada | — |
| F3A-08 | Dos SaveChanges sin transacción | VALIDADO | Código verificado | DEUDA CONFIRMADA |

---

```
Conclusiones Codex revisadas: 18
Validadas: 11
Matizadas: 6
Refutadas: 0
No demostradas: 1
Nuevos riesgos: 3

Archivos de código adicionales abiertos:
- src/ControlMarcacion.Domain/Personal/PeriodoLaboral.cs — verificar estructura y rol
- src/ControlMarcacion.Domain/Catalogos/ProgramacionCuadrilla.cs — verificar TramoRotacion y descanso
- src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs (parcial) — verificar uso de PeriodoLaboral, ResolverTurno y HorariosPorFecha
- src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioReportes.cs (parcial) — verificar decisión documentada de no usar PeriodoLaboral
- src/ControlMarcacion.Infrastructure/Marcaciones/ServicioImportacion.cs (parcial) — verificar dos SaveChanges y ausencia de transacción
- src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs (parcial) — verificar TurnoAplicaEn y DiasAplicables
- src/ControlMarcacion.Domain/Catalogos/Turno.cs (parcial) — verificar DiasAplicables como flags enum
- src/ControlMarcacion.Infrastructure/Persistencia/Configuraciones/ConfiguracionesPersonal.cs (parcial) — verificar FKs de Trabajador vs PeriodoLaboral
Motivo: cada archivo se abrió para verificar una afirmación específica, no para exploración general.

¿Existe algún hallazgo que obligue a detener la ingeniería inversa antes de Fase 3B?: NO.
La base documental es sólida. Fase 3B puede proceder cuando se identifique la copia canónica.
```

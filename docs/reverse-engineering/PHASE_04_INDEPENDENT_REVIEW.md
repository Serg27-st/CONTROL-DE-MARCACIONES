# Revisión independiente — Fase 4 (reglas de negocio y linaje)

Fecha: 2026-10-05  
Revisor: Claude Opus 5.5, en rol de arquitecto, analista funcional y auditor adversarial.  
Fuentes leídas: `PHASE_04_FLOWS_RULES_LINEAGE.md`, `BUSINESS_RULES.md`, `PHASE_03B_DATABASE_VALIDATION.md`, `INDEPENDENT_ARCHITECTURE_REVIEW.md`, `modules/PROVISIONAL_MARKINGS.md`, `CONTEXT_REGISTER.md` (cierres 3B y 4).  
Protocolo: verificación selectiva de clases/métodos/tests concretos (registro en el Anexo A). No se modificó código, no se abrió la base SQLite, no se ejecutaron pruebas, no se inició Fase 5.

Convención de evidencia: **[CÓDIGO]** leído en esta revisión; **[DOC]** tomado de documentos previos sin reverificar; **[INFERIDO]** razonamiento sobre lo leído; **[NEGOCIO-USUARIO]** regla declarada como confirmada en el encargo de esta revisión y **no** escrita en ningún `.md` del repositorio.

---

## 1. Resumen ejecutivo

La Fase 4 es en su mayor parte correcta: el flujo extremo a extremo, la aritmética de `CalculadoraTareo`, el linaje de `TareoDiario` y del Excel, y la frontera de PMV coinciden con el código revisado. Pero tiene tres tipos de defecto:

1. **Severidades mal calibradas.** Uno de los tres «críticos» (tasas por empresa) es latente y no tiene ruta de escritura en el sistema: baja a P3. Otro (PeriodoLaboral) estaba bien detectado por el síntoma equivocado: el problema no es que el motor no lo consuma —eso es una decisión documentada—, sino que **el sistema lo corrompe al editar cualquier ficha**.
2. **Causas no encontradas.** Las 85 duplicaciones de `periodo_laboral` tienen una causa raíz localizable en código: `TrabajadorRepository.ObtenerPorIdAsync` no carga `PeriodosLaborales`, de modo que cada edición abre un vínculo nuevo y cada baja deja el vínculo abierto (NH-02).
3. **Hallazgos ausentes.** La revisión encontró 12 hallazgos nuevos. Los más graves:
   - **NH-07** — la regla de horas extra confirmada (ordinario lunes a viernes hasta 17:22; sábado entero como hora extra) **no coincide con lo implementado** (480 min de duración desde la entrada; sábado laborable ordinario) y **contradice** las «Reglas confirmadas por GCA» de `CLAUDE.md` (07:00–19:00 = 480 ordinarios + 240 extra).
   - **NH-01** — la «Liquidación del periodo» de `/Asistencia` **cuenta dos veces** los minutos nocturnos y los de descanso/feriado trabajados.
   - **NH-03** — cualquier justificación que no resuelva el día (tardanza, permiso parcial, regularización incompleta) **borra la incidencia** de un día `Incompleto` o `PendienteValidacion`, y además ocupa el único cupo diario de justificación.
   - **NH-04** — la alerta de vigencia **no ve los borrados físicos** de justificaciones.

Ninguno obliga a detener la ingeniería inversa. **Sí obligan a no diseñar cambios del motor** hasta reconciliar la regla de horas extra con `CLAUDE.md`.

---

## 2. Reglas validadas

Validadas contra código en esta revisión (o en la revisión anterior, cuando se indica):

| Regla | Afirmación de Fase 4 | Evidencia de la revisión |
|---|---|---|
| RN-012 | Importación principal: dos `SaveChanges` sin transacción | [CÓDIGO] revisión anterior; reconfirmado por 3B |
| RN-024 | Rotación por `(fecha − ancla) mod ciclo` con resto positivo | [CÓDIGO] `ProgramacionCuadrilla.TurnoEn` (revisión anterior) |
| RN-025 | No hay tramo de descanso; el descanso sale de `DiasAplicables` | [CÓDIGO] `TramoRotacion.TurnoId` obligatorio; `ServicioTurnos.cs:323` rechaza `DiasSemana.Ninguno` |
| RN-026 / RN-027 | Versión de turno por fecha; *fallback* al turno actual | [CÓDIGO] `HorariosPorFecha` (revisión anterior) |
| RN-029 | Jornada imputada al día de inicio; fin al día siguiente si cruza medianoche | [CÓDIGO] `CalculoTareo.FinTurno` L971 |
| RN-037 / RN-038 | Sin archivo → `PendienteValidacion`; con archivo y sin marcas → `Falta` | [CÓDIGO] `CalculoTareo.cs` L498-513 |
| RN-041 / RN-042 | Descanso/feriado; el feriado prevalece | [CÓDIGO] L470-496 |
| RN-043 | Llegar antes no paga: el cómputo empieza en `max(entrada, inicio)` | [CÓDIGO] L895 |
| RN-044 | Gracia de salida por umbral | [CÓDIGO] L903-907 |
| RN-046 / RN-047 | Ordinario `min(trabajados, MinutosJornadaOrdinaria)`; 120 min al 25 % y el resto al 35 % | [CÓDIGO] L922-925; constante L294 |
| RN-048 / RN-049 | La tardanza y la salida anticipada cuentan completas al superar el umbral | [CÓDIGO] L985-1003 |
| RN-050 / RN-054 | La salida anticipada no cambia el estado; en día no laborable no hay tardanza | [CÓDIGO] L942-944 |
| RN-051 / RN-053 | Franja 22:00–06:00 fija; los extras son la cola de la jornada | [CÓDIGO] L288-291, L931-939, L1013-1039 |
| RN-059 / RN-060 | La ausencia paga la jornada ordinaria solo si el tipo paga; nunca extras | [CÓDIGO] `Aplicar` L554-571 |
| RN-062 | El permiso parcial cruza intervalos y no acredita minutos | [CÓDIGO] L719-784 |
| RN-063 | La regularización completa, no sustituye; conserva `EntradaReal`/`SalidaReal` | [CÓDIGO] L630-675 |
| RN-073 | Anular una solicitud borra sus justificaciones solo si todos sus periodos están abiertos | [CÓDIGO] `ServicioSolicitudes.AnularAsync` L399-449 |
| RN-082 | El bono nocturno entra por cualquier día con `MinutosNocturnos > 0` | [CÓDIGO] `ServicioReportes.cs` L86-87 |
| RN-088 | Exactamente siete fuentes y `MAX(CalculadoEn)` | [CÓDIGO] `ServicioReportes.cs` L488-593 |
| RN-089 | La liquidación toma las tasas al inicio del periodo; el bono nocturno, día por día | [CÓDIGO] `ServicioCalculoTareo.cs` L796-804; `ServicioReportes.cs` L960-963 |
| RN-093 | El Administrador recalcula y consulta solo sus plantas | [CÓDIGO] `ServicioCalculoTareo.cs` L71-76, L522, L693, L762 |
| PMV-RN-001 | PMV es un módulo aislado | [DOC] 3B; nada de lo leído lo contradice |
| PMV-RN-010 | Más de 40 días bloquea la exportación, no la consulta | [CÓDIGO] `MarcacionesProvisionalesController.cs` L114-139 |
| PMV-RN-012 | PMV no aplica alcance por planta | [CÓDIGO] controlador L13, L27, L97, L143: `EquiposActivosAsync` sin filtro y sin proveedor de alcance |

Total de reglas validadas: **32** (las filas agrupadas cuentan por regla).

---

## 3. Reglas con matices

| Regla | Fase 4 | Matiz de la revisión | Clase |
|---|---|---|---|
| RN-010 | Marca cruda inmutable; las correcciones viven en justificaciones | Correcto para la marca. Pero las **justificaciones** sí se borran físicamente por dos vías (`ServicioSolicitudes.cs:442` y `ServicioJustificaciones.cs:612`), contra el invariante 7 de `CLAUDE.md`. Atenuante: el interceptor audita `EntityState.Deleted` (`InterceptorAuditoria.cs:234`). Fase 4 solo citó la vía de la solicitud. | VALIDADA CON MATICES |
| RN-019 | La población ignora `periodo_laboral` (riesgo alto) | Que el motor use los campos planos es una **decisión consciente y documentada** (`ServicioReportes.cs:639-642`). El riesgo alto real es NH-02: el mantenimiento de `PeriodoLaboral` está roto. | VALIDADA CON MATICES |
| RN-045 / RN-052 | El refrigerio no se descuenta de la nocturnidad (doble base) | Comportamiento confirmado, pero **latente**: los cinco turnos sembrados tienen `MinutosRefrigerio = 0` (`DatosSemilla.cs:433-470`). Se vuelve real si GCA fija un refrigerio no pagado; ver §8.6 y NH-07. | VALIDADA CON MATICES |
| RN-055 | Una incidencia por día, por prioridad | La prioridad es correcta y está documentada como diseño. Fase 4 no vio que **cualquier `JustificacionId` suprime toda incidencia**, aunque la justificación no resuelva el día (NH-03). | VALIDADA CON MATICES |
| RN-079 | La nocturnidad es una dimensión superpuesta | Correcto, y además AL (nocturno en día no laborable) es **subconjunto** de AI (nocturno de todos los días). Si planilla suma columnas, paga dos veces. Depende de la pregunta 6. | VALIDADA CON MATICES |
| RN-088 | Lista de fuentes incompleta | Confirmado y **ampliado**: además de las omisiones citadas, no detecta borrados físicos (NH-04) y queda «al día» en falso tras un recálculo parcial por planta (§8.1). Algunas omisiones sí se cubren indirectamente: la baja de un trabajador cierra sus asignaciones de marcador y de turno, y esa modificación sí entra en la alerta. | VALIDADA CON MATICES |
| RN-090 | La liquidación agrega minutos y tasas, sin importes; riesgo medio | Además **suma mal**: NH-01. | VALIDADA CON MATICES |
| RN-094 | Excel mixto: histórico y actual | Ampliado con efectos concretos: OBS «CESE» retroactivo, filtros por planta o empresa actuales que mueven trabajadores entre reportes históricos, y reintegros que **no** cambian (§8.2). | VALIDADA CON MATICES |
| RN-096 | El reporte consume la proyección aunque esté obsoleta | Correcto. El riesgo depende de que la alerta sea fiable, y hoy no lo es. | VALIDADA CON MATICES |
| PMV-RN-006 | Las marcas sin dueño se conservan | Correcto. Fase 4 no distingue causas que sí son separables: el ID `1` del *master* queda sin dueño por diseño (`CLAUDE.md`), y el respaldo binario pierde los ceros a la izquierda, así que «0014» del padrón ≠ «14» del archivo. Ver NH-12. | VALIDADA CON MATICES |
| PMV-RN-009 | Organización actual; la cuadrilla se muestra como etiqueta | Correcto. Impacto actual nulo: la base canónica tiene 0 cuadrillas y 0 asignaciones de turno [DOC 3B §11.2]. | VALIDADA CON MATICES |
| PMV-RN-003 | El hash no es único | Correcto, pero la ventana de carrera exige dos cargas simultáneas del mismo archivo en una instalación de una sola PC. Severidad baja. | VALIDADA CON MATICES |

Total con matices: **12**.

### 3.1 Rotación y descanso — gap funcional, no deuda técnica

Regla confirmada: `14 DIURNO → 1 DESCANSO MÓVIL → 7 NOCTURNO → repetir` (ciclo de 22 días).

- **VALIDADA** como **GAP FUNCIONAL / MODELADO**. La revisión añade dos evidencias:
  1. No existe ningún atajo dentro del modelo: un turno «DESCANSO» con cero días aplicables lo rechaza `ServicioTurnos.cs:323` («Debe indicar al menos un día…»).
  2. El modelo no solo **no puede expresar** el descanso móvil: **impone uno equivocado**. Con los turnos sembrados (`DIU`/`NOC` de lunes a sábado), cada domingo dentro del ciclo de 22 días sale `DescansoSemanal`, y trabajarlo va al tramo 100 %. Usar cuadrillas con esos turnos produciría tareos incorrectos, no tareos incompletos. Si en cambio se configuran los turnos con los siete días, el día de descanso móvil saldría **Falta**.
- **Mezcla de categorías detectada:** 3B lo listó en «Deuda confirmada #3» junto a atomicidad e índices. No es deuda técnica: el código hace lo que su diseño declara (`ProgramacionCuadrilla.cs:22-30`). Es una **regla de negocio que el modelo no admite**. Fase 4 (RN-025, «decisión de modelado») lo clasificó bien.
- Impacto actual: nulo, porque no hay cuadrillas en la base. Es bloqueante para poner la rotación en producción.

### 3.2 Nocturnidad

| Aspecto | Comportamiento confirmado [CÓDIGO] | Observación |
|---|---|---|
| Inicio / fin | 22:00 / 06:00, constantes estáticas (L288-291) | No es configurable. Es un valor legal codificado: contradice el espíritu del invariante 4, que pide los parámetros legales como datos con vigencia. La tasa sí es dato. |
| Cruce de medianoche | El bucle empieza el día anterior (L1024) y cubre varias franjas | Correcto, incluida una jornada de más de 24 h. |
| Base | Intervalo **computable** `[max(entrada, inicio), fin o salida]` | Una llegada anticipada antes de las 22:00 no suma nocturnidad. Coherente con RN-043. |
| Acumulación | Suma total del día, ordinaria + extra | Persistida en `MinutosNocturnos`. |
| Interacción con extras | Los extras son la cola: `Extra25Nocturnos` y `Extra35Nocturnos` por solape | Subconjuntos del total nocturno. |
| Interacción con feriado | No se separa en el motor; el reporte reparte AI (todos los días) y AL (día no laborable) | AL ⊂ AI: riesgo de doble lectura, ligado a la pregunta 6. |
| Refrigerio | No se descuenta de la franja | Latente mientras sea 0 (§8.6). |
| Parámetros | La franja no usa ninguno; el bono nocturno usa RMV, `TasaSobretasa.TrabajoNocturno` y sueldo vigentes por día | La sobretasa nocturna **no** se aplica por hora en el tareo final; sí en la Liquidación (NH-01). |

Separación entre ley e implementación: el proyecto documenta «22:00–06:00, D.S. 007-2002-TR art. 8» en `CLAUDE.md` como regla confirmada por GCA. Esta revisión no verificó el texto legal; lo único verificado es que el código implementa esa franja.

### 3.3 Correcciones manuales: qué existe realmente

| Necesidad operativa | Qué existe [CÓDIGO] | Brecha |
|---|---|---|
| Ingreso faltante | Regularización con `EntradaRegularizada` (el Administrador la solicita y el Coordinador aprueba); `IncidenciasController.Regularizar` / `Resolver` | **Imposible si el día ya tiene otra justificación** (un permiso parcial, por ejemplo): un solo cupo por día. Ver NH-03. |
| Salida faltante | Igual, con `SalidaRegularizada` | Igual. |
| Modificar una marca | **No existe, por diseño** (invariante 1). Salidas posibles: regularizar o anular la importación. | Ninguna: es la decisión correcta. |
| Validar una incidencia sin corregir nada | **No existe.** La bandeja solo tiene `Index`, `Regularizar` y `Resolver` (aprobar o rechazar una regularización). | Un `TurnoDiscrepante` o `FueraDeTurno` revisado y correcto **no puede marcarse como revisado**: reaparece en cada recálculo. Es un GAP menor que llena la bandeja de ruido, el mismo problema que el código dice querer evitar. |
| Corregir el turno | «Turno rige desde» en la ficha (`ReasignarTurno`), «Correr la rotación» en la cuadrilla, y recalcular | Correcto y auditado. No se puede corregir el turno de **un solo día**: toda corrección es «desde esta fecha». |

---

## 4. Reglas no demostradas

| Regla / afirmación | Por qué no queda demostrada |
|---|---|
| RN-003 / RN-004 (comportamiento del hash ante una cabecera huérfana) | La recuperación descrita es razonamiento sobre el código: no hay prueba ni dato (0 importaciones principales en la base). |
| RN-067 / RN-068 (solapamiento y edición de solicitudes) | No se reverificaron; Fase 4 ya las marca como cobertura parcial. |
| RN-095 («el recálculo deja log agregado») | No se verificó el `LoggerMessage`. Además, el comentario de `ServicioJustificaciones.cs:608-610` afirma que quién retira una justificación «queda en el log de aplicación», pero el método descarta `usuario` (`_ = usuario`) y no registra nada. Lo que queda es la auditoría del interceptor. |
| «La alerta usa principalmente el máximo» como causa de falsos «al día» | **Promovida a confirmada por código** (§8.1); ya no es una inferencia. |

Total no demostradas: **5 reglas** en 3 filas (RN-003, RN-004, RN-067, RN-068, RN-095). La cuarta fila se resolvió.

---

## 5. Reglas o conclusiones refutadas

| Ref. | Afirmación de Fase 4 / 3B | Resultado | Evidencia |
|---|---|---|---|
| R-01 | «79 directas, 18 parciales, 11 sin prueba» | **REFUTADA** (aritmética del documento) | La propia tabla de `BUSINESS_RULES.md` da **83 TESTEADA, 14 PARCIAL, 11 SIN TEST**. |
| R-02 | Tasas por empresa = riesgo **crítico**, «resultado no determinista o tasa de otra razón social» | **REFUTADA la severidad** (el mecanismo descrito es cierto) | No existe ninguna ruta de escritura de `TasaSobretasa` fuera de la semilla, que siembra 5 filas con `EmpresaId` nulo. Ningún controlador ni servicio crea o edita tasas. Solo es reproducible con SQL directo. Ver §7.2. |
| R-03 | Alto #2: el Excel de un periodo cerrado cambia por «reintegros» actuales | **REFUTADA para reintegros** | RN-066: escribir reintegros exige el periodo abierto. En un periodo cerrado los reintegros son estables. Padrón y CECO sí cambian. |
| R-04 | 3B: «el origen operativo de las 85 duplicaciones no puede distinguirse» | **REFUTADA** | Causa raíz probable localizada en código (NH-02); explica exactamente el patrón observado de «máximo dos filas, todas iguales». |

Total refutadas: **4 conclusiones**. Ninguna regla del catálogo describe mal el código.

---

## 6. Nuevos hallazgos

| ID | Hallazgo | Evidencia | Tipo | Severidad |
|---|---|---|---|---|
| **NH-01** | **Doble conteo en «Liquidación del periodo».** `LiquidarPeriodoAsync` suma ordinarios, extra 25 y extra 35 de **todos** los días, incluidos descanso y feriado, y además suma esos mismos minutos como concepto Descanso o Feriado. `TotalHorasEquivalentes` suma `minutos × (1 + %)` por concepto encima de las ordinarias, y el nocturno, que ya está dentro de ordinarias y extras, se suma otra vez × 1,35. Ejemplo: un domingo DIU de 12 h da 8 + 2,5 + 2,7 + 24 = **37,2 h equivalentes**, contra 24 h si todo el día va al 100 %. Una noche NOC laborable con 8 h nocturnas da 24,0 h, contra 16,0 h si la nocturnidad fuera solo el 35 % adicional. La pantalla dice que entrega la base «para que planilla multiplique». | [CÓDIGO] `ServicioCalculoTareo.cs:772-831`; `SobretasaDtos.cs:26-62`; `Asistencia/Index.cshtml:271-293`. Sin prueba (RN-090 SIN TEST). | BUG CONFIRMADO (por lectura, no ejecutado) | Alta |
| **NH-02** | **El mantenimiento de `PeriodoLaboral` está roto.** `TrabajadorRepository.ObtenerPorIdAsync` incluye las asignaciones de marcador, turno, sueldo y área, **no `PeriodosLaborales`**, y no hay carga diferida. Por eso: (a) `ActualizarAsync` → `SincronizarVinculoAbierto` ve la colección vacía y llama a `AbrirVinculo`, con lo que **cada edición duplica el vínculo abierto**; (b) `DarDeBajaAsync` → `Trabajador.DarDeBaja` no encuentra el vínculo y **no lo cierra**. Encaja con 3B: 126 trabajadores con una fila (nunca editados) y 85 con exactamente dos, idénticas en todos los campos de negocio (editados una vez). Las pruebas de dominio (`BajaTrabajadorTests`) arman la colección en memoria y no ejercitan el repositorio. | [CÓDIGO] `TrabajadorRepository.cs:8-14`; `ServicioTrabajadores.cs:274, 333, 364-374, 701-721`; `Trabajador.cs:169-177, 235-244`. [DOC] 3B §12.1. | BUG CONFIRMADO por código, consistente con los datos; no reproducido | Alta en integridad; nula hoy en el tareo |
| **NH-03** | **Una justificación que no resuelve el día suprime la incidencia.** `ConIncidencia` sale sin incidencia si existe `JustificacionId` (L370-373), pero `Tardanza`, `SalidaAnticipada`, `PermisoParcial` y la regularización incompleta **conservan** el estado `Incompleto`, `Falta` o `PendienteValidacion`. Ejemplo: alguien con un permiso parcial aprobado que olvida marcar la salida queda `Incompleto`, con 0 horas, `RequiereValidacion = false` y fuera de la bandeja. Además la única justificación del día ya está ocupada, así que **no se puede registrar la regularización** sin anular la solicitud entera. La prueba existente (`Un_dia_ya_justificado_no_genera_incidencia`) solo cubre `MarcacionIncompleta`, que sí cambia el estado. | [CÓDIGO] `CalculoTareo.cs:370-373, 573-594, 644-651, 771-783`; `IncidenciasDelDiaTests.cs:286-304`. | RIESGO FUNCIONAL (BUG POTENCIAL) | Alta |
| **NH-04** | **La alerta de vigencia no ve los borrados.** Calcula `MAX(ModificadoEn ?? CreadoEn)` sobre las justificaciones que **siguen existiendo**. Retirar una justificación manual o anular una solicitud aprobada las borra físicamente, y el tareo sigue aplicándolas (con su `JustificacionId` apuntando a una fila inexistente) mientras la alerta dice «al día». | [CÓDIGO] `ServicioReportes.cs:525-529`; `ServicioJustificaciones.cs:612`; `ServicioSolicitudes.cs:442`. | BUG CONFIRMADO por código | Alta |
| **NH-05** | **OBS «CESE» retroactivo.** El tareo final escribe `OBS = "CESE"` y `FECHA CESE` siempre que la ficha tenga cese, sin mirar si cae en el periodo. Reemitir julio después de registrar un cese en setiembre lo muestra como cesado en julio: justo el error que el comentario de `ResolverTrabajadoresAsync` dice haber corregido, solo que por otra vía. | [CÓDIGO] `ServicioReportes.cs:1182-1184`; comentario en L627-637. | BUG CONFIRMADO por código | Media |
| **NH-06** | **Los filtros históricos usan la organización actual.** El padrón del reporte filtra por `t.EmpresaId`, `t.PlantaId` y `t.CentroCostoId` de hoy, y el alcance del Administrador por la planta actual. Un traslado de planta en agosto **saca** a la persona del reporte de julio de su planta anterior y la **pone** en el de la nueva, y el Administrador de la planta anterior deja de ver su julio. Es más que una etiqueta cambiada: cambia la población. | [CÓDIGO] `ServicioReportes.cs:650-701`. | RIESGO FUNCIONAL / REQUIERE NEGOCIO | Media (alta si hay traslados) |
| **NH-07** | **Las horas extra implementadas no coinciden con la regla confirmada, y la regla confirmada contradice `CLAUDE.md`.** Detalle en §6.1. | [CÓDIGO] `CalculoTareo.cs:895-925`; `DatosSemilla.cs:430-474`; [NEGOCIO-USUARIO]; `CLAUDE.md` «Reglas confirmadas por GCA». | GAP FUNCIONAL + CONTRADICCIÓN DE FUENTES | **Crítica** |
| **NH-08** | **Con DOM1/DOM2 el domingo es laborable**, así que un domingo trabajado con esos turnos **no** va al 100 % y se reparte en ordinario y extras. Contradice «domingo trabajado = feriado». | [CÓDIGO] `DatosSemilla.cs:457-474` (`DiasAplicables = Domingo`); `CalculoTareo.cs:466-470`. | GAP FUNCIONAL / REQUIERE NEGOCIO | Media |
| **NH-09** | **`CLAUDE.md` está desactualizado sobre el alcance por planta.** Dice que «no existe todavía» el filtrado por las plantas de un Administrador, pero el código lo aplica en Personal, Justificaciones, Tareo, Liquidación y Reportes. Consecuencia: la ausencia en PMV es una **omisión frente a un invariante del sistema**, no una política uniforme. | [CÓDIGO] `ProveedorAlcancePlantasHttp.cs:13-61`; usos en `ServicioCalculoTareo` y `ServicioReportes`. | DOCUMENTACIÓN DESACTUALIZADA | Media (eleva PMV-RN-012) |
| **NH-10** | **El resumen de cobertura de pruebas de Fase 4 no cuadra con su propia tabla** (R-01). | [DOC] recuento de `BUSINESS_RULES.md`. | DOCUMENTACIÓN | Baja |
| **NH-11** | **El modelo de turnos impone descansos semanales dentro de una rotación** (ver §3.1, punto 2) y no admite un turno de descanso. | [CÓDIGO] `ServicioTurnos.cs:323`; semilla `LunesASabado`. | GAP FUNCIONAL | Alta (al activar la rotación) |
| **NH-12** | **Las marcas PMV sin dueño tienen causas separables con datos existentes.** (a) El ID del *master* (`1`) queda sin dueño a propósito. (b) Una carga desde `BKtransaction.dat` pierde los ceros a la izquierda, así que el padrón `0014` no cruza. (c) Si la pareja `(equipo, ID)` tiene alguna asignación en otra fecha, el problema es de vigencia. (d) Si no tiene ninguna, el padrón está incompleto o el ID es ajeno. | [DOC] `CLAUDE.md` (master y binario); 3B §8. | REQUIERE DATOS (consulta de solo lectura) | Baja |

Total de nuevos hallazgos: **12**.

### 6.1 Horas extra: lo implementado frente a la regla confirmada

Fuente de la regla: [NEGOCIO-USUARIO]. Lunes a viernes, ordinario hasta las 17:22 y horas extra desde la salida ordinaria; sábado, todo el día es hora extra; primeras 2 h al 25 % y el resto al 35 %; domingo trabajado tratado como feriado. **Esta regla no está escrita en ningún `.md` del repositorio**, y `CLAUDE.md` afirma otra como confirmada por GCA.

| Aspecto | Implementado [CÓDIGO] | Regla confirmada | Diferencia exacta |
|---|---|---|---|
| Frontera ordinario / extra (L–V) | **Duración**: `min(trabajados, MinutosJornadaOrdinaria)`, con 480 en los cinco turnos, contados desde `max(entrada, inicio)` | **Hora de reloj**: 17:22 | En DIU 07:00–19:00 puntual el sistema cierra el ordinario a las **15:00**: 142 min antes. |
| DIU 07:00–19:00, día laborable completo | 480 ordinario / **120 al 25 % / 120 al 35 %** | 622 ordinario (07:00–17:22) / **98 al 25 % / 0 al 35 %** | El sistema da 142 min más de extra y 120 min al 35 % que la regla no contempla. |
| Llegada tarde (p. ej. 07:30) | La frontera **se corre**: ordinario hasta 15:30 | No especificado; una frontera de reloj sugiere que **no se corre** | **REQUIERE NEGOCIO**. |
| Sábado | `DIU`/`NOC` aplican de lunes a sábado: el sábado es laborable, con 480 ordinario y extras después; `SAB` (07–15) da 480 ordinario y 0 extra | **Todo es hora extra** | Sábado de 12 h: el sistema da 480 / 120 / 120; la regla, 0 / 120 / 600. Sábado de 8 h: el sistema da 480 / 0 / 0; la regla, 0 / 120 / 360. |
| Reparto 25 / 35 | Por día, los primeros 120 min (constante L294) | Primeras 2 h, el resto | **Coincide.** |
| Domingo trabajado (DIU/NOC) | `DescansoSemanal`; el tareo final lo lleva entero a AG (100 %) | Como feriado | **Coincide en el tareo final.** El `TareoDiario` sigue partiéndolo en ordinario y extras, y la Liquidación lo cuenta dos veces (NH-01). |
| Domingo trabajado (DOM1/DOM2) | Laborable: ordinario y extras, sin 100 % | Como feriado | **No coincide** (NH-08). |
| Turno nocturno | 480 ordinario y extras en cola | No declarado | **REQUIERE NEGOCIO**. |
| Contradicción documental | `CLAUDE.md`: «una jornada de 12 h da 720 efectivos, 480 ordinarios y 240 de sobretiempo» | Ordinario hasta 17:22 | Hay **dos reglas «confirmadas» incompatibles**. |

**[INFERIDO, no confirmado]** 07:00 → 17:22 son 622 min = **576 (48 h ÷ 5 días) + 46**. Es aritméticamente coherente con 48 h semanales repartidas de lunes a viernes **más ~46 min de refrigerio no computable**. Si fuera así, también contradiría la regla de `CLAUDE.md` «el refrigerio no se descuenta, es tiempo pagado», y activaría la brecha latente de §8.6. Es una hipótesis para confirmar con GCA, no una conclusión.

---

## 7. Revisión de los riesgos críticos

### 7.1 PMV sin alcance de planta para el Administrador — VALIDADO

- Verificado: `[Authorize(Roles = COORDINADOR, ADMINISTRADOR)]` a nivel de clase. `Index`, `Consulta` y `Exportar` cargan `EquiposActivosAsync` sin filtro, y ningún servicio PMV consume `IProveedorAlcancePlantas`.
- **Matiz que agrava:** no hace falta manipular la URL. El **propio desplegable** ofrece los equipos de todas las plantas.
- Datos expuestos: DNI, apellidos y nombres de trabajadores de **la otra razón social** (Consulting Advisers ↔ CA Outsourcing), en pantalla y en Excel. Es relevante para la Ley 29733: son dos responsables de datos distintos.
- Escritura indebida: limitada a las tablas `prov_*`; no toca el tareo.
- Clasificación: **problema de autorización y aislamiento de datos**, severidad **ALTA**. No es crítica de integridad, pero es la **única** violación confirmada del invariante de alcance que el resto del sistema sí cumple (NH-09). **P0**: el riesgo es de datos personales y la corrección es acotada.

### 7.2 Tasas por empresa — VALIDADO el mecanismo, REFUTADA la severidad

| Pregunta | Respuesta [CÓDIGO] |
|---|---|
| ¿Qué entidad tiene `EmpresaId`? | `TasaSobretasa.EmpresaId` (`long?`, «nulo si aplica a todas»). Sin FK [DOC DATABASE.md]. `ParametroLegal` no tiene empresa. |
| ¿Qué tasas están afectadas? | Los cinco conceptos: extra 25, extra 35, nocturno, descanso semanal y feriado. |
| ¿Quién las consume? | `ServicioCalculoTareo.LiquidarPeriodoAsync` (L798-808: `Where` sin empresa y `FirstOrDefault` por concepto, sin orden) y `ServicioReportes.ObtenerTasaSobretasaAsync` (L885-892, solo el concepto nocturno, usado en el bono nocturno con `FirstOrDefault` vigente). |
| ¿Se elige una tasa de otra empresa? | **Hoy no puede ocurrir**: solo existen las 5 filas sembradas con `EmpresaId = null` y **no hay ninguna ruta de escritura** (búsqueda de `new TasaSobretasa` / `TasasSobretasa.Add`: solo la semilla). Ninguna vista de `Web` edita tasas. |
| ¿Potencial o reproducible? | **Potencial.** Solo con un `INSERT` directo: dos filas vigentes del mismo concepto darían una elección **no determinista**, porque falta `ORDER BY`. La liquidación ni siquiera recibe la empresa del trabajador; agrega el periodo entero. |
| Severidad | **BAJA** (campo latente o muerto). **P3**: decidir si se elimina el campo o se implementa la precedencia «empresa > global» cuando exista una pantalla de tasas. |

### 7.3 PeriodoLaboral — VALIDADO CON MATICES, reclasificado

| Dimensión | Clasificación | Fundamento |
|---|---|---|
| Que el motor y los reportes usen `FechaIngreso`/`FechaCese` planos | **DECISIÓN ACTUAL CONSCIENTE** | Comentario explícito en `ServicioReportes.cs:639-642`; mismo filtro en motor y reporte. Correcto mientras haya un solo vínculo por persona. |
| Reingresos | **LIMITACIÓN FUNCIONAL** (latente) | Con varios vínculos, los campos planos no pueden expresar el hueco entre un cese y el reingreso. Sigue latente porque no existe la acción «reingresar» (`CLAUDE.md`). |
| Las 85 duplicaciones | **BUG CONFIRMADO por código** (NH-02), no deuda ni decisión | Cada edición de la ficha abre un vínculo; cada baja lo deja abierto. Efecto visible hoy: `/Trabajadores/Historial` muestra vínculos duplicados y abiertos después de un cese. |
| Impacto en el tareo hoy | **Nulo** | El motor no lee la tabla. |
| Impacto futuro | **Alto** | Cualquier intento de usar `PeriodoLaboral` heredaría datos corruptos. La reparación de datos exige decidir qué fila sobrevive: es una decisión con autor. |

Prioridad real: **P1**. La corrupción crece con cada edición.

---

## 8. Revisión de los riesgos altos

### 8.1 Alerta de tareo desactualizado — VALIDADO y ampliado

**Fuentes que sí considera** [CÓDIGO `ServicioReportes.cs:488-593`]: marcaciones (`CreadoEn`) del rango; `ModificadoEn` de las importaciones con marcas en el rango (anular o reactivar); justificaciones del rango (`ModificadoEn ?? CreadoEn`); asignaciones de marcador que solapan el rango; asignaciones de turno que solapan el rango (incluye integrantes de cuadrilla); `Turno.ModificadoEn` de los turnos en uso en el rango (asignación directa o tramo); programaciones de cuadrilla que solapan el rango (incluye las dadas de baja).

| Insumo | ¿Lo considera? | Efecto de omitirlo |
|---|---|---|
| Marcaciones | Sí | — |
| Anular / reactivar una carga | Sí | — |
| Justificación creada o modificada | Sí | — |
| **Justificación borrada** (retiro manual o anulación de solicitud) | **No** (NH-04) | Falso «al día» con una ausencia ya retirada todavía pagada. |
| Asignación de marcador | Sí | — |
| Asignación de turno / cuadrilla | Sí | — |
| Turno / versión | Sí, vía `Turno.ModificadoEn` | Sobreavisa, y es deliberado (`CLAUDE.md`). |
| Programación de cuadrilla | Sí | — |
| `TramoRotacion` editado sin tocar la programación | Indirecto | Las programaciones se reemplazan, no se editan; riesgo bajo. |
| **Feriados** (alta, baja o sincronización) | **No** | Un feriado de planta registrado tarde deja el tareo con falta o sin el 100 % y la alerta «al día». |
| **Tipo de ausencia** (`Comportamiento`, `ComputaComoAsistencia`) | **No** | Cambia qué días se pagan en el siguiente recálculo. |
| **Ficha: `FechaIngreso` / `FechaCese`** | **Parcial** | La baja cierra las asignaciones y eso sí se detecta; **corregir la fecha de ingreso** no. |
| **Equipo nuevo o dado de baja** (cambia la cobertura) | **No** | Días que deberían pasar a `PendienteValidacion` siguen como `Falta`, o al revés. |
| `TurnoPlanta` (candidatos del detector) | No | Solo afecta a `TurnoDiscrepante`; bajo. |
| Parámetros, sueldos, reintegros, CECO, hijos | No, **y no deben** | No entran al `TareoDiario`: se leen en vivo al generar el reporte. No dejan el tareo obsoleto; hacen que el **Excel** no sea una foto fija (§8.2). |
| **Recálculo parcial por planta** | **No contemplado** | `MAX(CalculadoEn)` del rango **sin filtro por planta** (deliberado, L482-486), mientras el Administrador **recalcula solo sus plantas** (`ServicioCalculoTareo.cs:71-76`). Secuencia reproducible por código: cambio en la planta B a las 10:00 → el Administrador de A recalcula a las 11:00 → el máximo es 11:00, mayor que 10:00 → el Coordinador ve «al día» con la planta B sin recalcular. |

**Respuesta:** **sí**. La interfaz y el Excel pueden afirmar que el tareo está actualizado cuando alguno de sus insumos cambió después. Hay al menos tres vías confirmadas por código (recálculo parcial, borrados, feriados o equipos o tipo de ausencia) y ninguna prueba las cubre: `AlcanceYVigenciaReportesTests` prueba marcación, justificación nueva, turno, edición de turno y programación, pero no anulación, borrado, recálculo parcial ni feriados. Severidad **ALTA**, **P1**.

### 8.2 Histórico en reportes

| Campo del reporte | Histórico | Actual | Riesgo real de alterar un reporte histórico |
|---|---|---|---|
| Empresa (filtro y columna) | — | `t.EmpresaId` | **Bajo hoy**: cambiar de empresa exige cese y no hay reingreso. Latente con reingresos. |
| Planta (columna D, **filtro y alcance**) | — | `t.PlantaId` | **Alto si hay traslados**: cambia la población del reporte, no solo la etiqueta (NH-06). |
| Área (H) | `AsignacionArea` por solapamiento | Respaldo: la ficha | Bajo. |
| Sección | — | Ficha | Bajo: solo respaldo de CENTRO DE COSTO en el resumen. |
| Centro / CECO (E, F, G) | — | Ficha | **Alto**: sin vigencia; cambiar el CECO reescribe la columna que cruza con SAP en todo periodo reemitido. |
| Turno (Excepciones) | `TareoDiario.TurnoId` | Código del turno | Bajo: el código casi nunca cambia. No muestra la versión de horario aplicada. |
| ID de marcador / aparato | Asignaciones por solapamiento | — | Ninguno. |
| DNI / nombre | — | Ficha | Bajo y deseable: corrige erratas. |
| Fecha de ingreso / cese, OBS | — | Campos planos | **Medio**: «CESE» retroactivo (NH-05). |
| Asignación familiar (AM) | RMV y % por vigencia | `TieneHijos` actual | **Medio**: cambiar el indicador reescribe meses cerrados. |
| Movilidad, bono nocturno | Tarifa, RMV, tasa y sueldo por vigencia | — | Bajo, salvo edición retroactiva de un parámetro (no verificado si se permite). |
| Reintegros | Fila por periodo | — | **Ninguno en periodo cerrado** (RN-066). R-03 refuta a Fase 4. |
| Cifras del tareo | `TareoDiario` congelado en periodo cerrado | — | Ninguno: no se puede recalcular un periodo cerrado. |

Conclusión: el Excel de un periodo cerrado **sí puede cambiar**, por planta, CECO, `TieneHijos` y cese, y **no** por reintegros ni por cifras de tareo. Qué datos deben congelarse es la pregunta 11: **bloquea el diseño**.

### 8.3 Importación principal no atómica — VALIDADA; prioridad real P2

| Dimensión | Evaluación |
|---|---|
| Impacto real | Una cabecera vigente con `FilasInsertadas > 0` y 0 marcas. **No falsea la cobertura**: el calendario y el motor cuentan marcas, no cabeceras (RN-013/RN-014 [DOC]). La alerta de anulación tampoco la ve, porque filtra cargas con marcas en el rango. El daño es un historial que miente sobre una carga. |
| Probabilidad | Baja: SQLite local en una sola PC, sin red. Requiere caída del proceso, disco lleno o una carrera de dos cargas simultáneas del mismo archivo. |
| Recuperabilidad | Total y manual: anular la cabecera huérfana y recargar. Nada se pierde, porque el archivo existe. |
| Reintento y hash | Mismo equipo: la UI avisa y permite continuar. Otro equipo: el rechazo «ya vigente en otro equipo» se activaría por la cabecera huérfana; la salida es anularla primero. Fricción, no bloqueo. |
| Consistencia posterior | El detalle queda consistente; solo los contadores de la cabecera no cuadran. |
| Prioridad | **P2**. Es deuda confirmada con solución conocida (`CLAUDE.md` ya prescribe `ExecuteAsync`), pero no altera el tareo. |

### 8.4 Solicitud aprobada parcial — VALIDADA (no reverificada)

Se mantiene como **REQUIERE NEGOCIO** (pregunta 8). Severidad media, **P2**.

### 8.5 Sin lista de marcas consumidas ni versión o parámetros aplicados — VALIDADA CON MATICES

La reproducibilidad descansa en insumos inmutables (marcas) o con vigencia (asignaciones, versiones, parámetros). Se rompe en dos casos concretos: (a) una **corrección en sitio** de `VersionTurno` cuando «rige desde» es igual o anterior a la vigente (`CLAUDE.md`), que reescribe el horario histórico; (b) una edición retroactiva de parámetros. Para un periodo cerrado el tareo queda congelado, así que el riesgo es de **explicabilidad**, no de cifras. Se clasifica como **DECISIÓN DE DISEÑO** con riesgo de auditoría. **P3**.

### 8.6 Refrigerio nocturno — COMPORTAMIENTO CONFIRMADO + NEGOCIO PENDIENTE

Comportamiento exacto [CÓDIGO `CalculoTareo.cs:910-939`]:

1. `trabajados = max(0, brutos − MinutosRefrigerio)`: descuento **completo**, sin ubicar el refrigerio en el tiempo y sin mínimo de jornada.
2. `nocturnos` se calcula sobre el intervalo **bruto** `[inicioComputo, finComputo]`: el refrigerio **no** se resta de la franja.
3. Los extras se ubican como cola: `inicioExtras = finComputo − extras`. Como `extras` ya excluye el refrigerio, el refrigerio queda implícitamente **dentro del tramo ordinario**.
4. Valor actual: **0 en los cinco turnos**. Hoy el efecto es nulo.

Pendiente de negocio: si el refrigerio es pagado (como dice `CLAUDE.md`) o no pagado (como sugiere la hipótesis de §6.1); en qué tramo horario cae en el turno nocturno; si se descuenta en jornadas cortas. Prioridad **P2**, ligada a NH-07.

### 8.7 Una sola justificación y una sola incidencia por día

| Elemento | Clasificación |
|---|---|
| Una incidencia priorizada | **DECISIÓN DE DISEÑO** documentada en `ConIncidencia` (L336-356) y en `CLAUDE.md`. Información perdida: un `TurnoDiscrepante` no se ve cuando hay `FueraDeTurno` o `Incompleto`; el conteo de marcas fuera de ventana sí se conserva. Aceptable. |
| Prioridad cuando el día está justificado | **RIESGO FUNCIONAL** (NH-03). No es decisión de diseño: el comentario dice «un día ya justificado no tiene nada pendiente», y eso es falso para los tipos que no cambian el estado. |
| Una justificación por día | **DECISIÓN DE DISEÑO** respaldada por un índice. Se vuelve **GAP** cuando el día necesita dos actos: un permiso más una regularización, o tardanza más salida anticipada (pregunta 9). |

---

## 9. Revisión de PMV

| Tema | Resultado |
|---|---|
| **A. Aislamiento** | **VALIDADO**: sigue siendo un módulo separado, sin escritura en tablas principales [DOC 3B]. Nada de lo leído lo contradice. |
| **B. Alcance por planta** | **VALIDADO y agravado**: equipos de todas las plantas en el propio desplegable, datos personales de la otra razón social y contradicción con el invariante aplicado en el resto del sistema (NH-09). Severidad **ALTA** como autorización y aislamiento de datos. **P0**. |
| **C. Marcas sin dueño** | **No constituyen error por sí mismas.** Causas separables: padrón desactualizado (existe asignación del par `(equipo, ID)` en otra fecha); vigencia ausente (el trabajador existe con ese ID pero el rango no cubre la marca); trabajador inexistente (ninguna asignación del par, incluido el *master* `1`, esperado); dato inválido (ceros perdidos por carga binaria, o un ID del archivo de otro aparato). Se pueden clasificar con una consulta de solo lectura (NH-12). |
| **D. Organización histórica** | Actual: planta, área (no usa `AsignacionArea`), centro de costo, DNI y nombres. Histórico: atribución del marcador y turno asignado. Riesgo: medio, con traslados. |
| **E. Turno por cuadrilla** | Simplificación exacta: si la asignación es por cuadrilla, la columna «Turno» lleva **el nombre de la cuadrilla**; no resuelve `ProgramacionCuadrilla.TurnoEn` ni el tramo del día. Impacto actual nulo (0 cuadrillas). |
| **F. Rango** | Inconsistencia confirmada: la consulta no tiene tope y la exportación se rechaza por encima de 40 días (controlador L139; la vista oculta el botón). El usuario puede ver 90 días y no exportarlos. El efecto colateral es memoria, porque todo se materializa. Severidad baja, **P3**. |

---

## 10. Revisión de la cobertura de pruebas

1. **El recuento real del catálogo es 83 / 14 / 11** (R-01). El resumen de Fase 4 subestima las «directas» en 4 y sobreestima las «parciales» en 4.
2. «TESTEADA» significa que **existe** una prueba declarada; ni Fase 4 ni esta revisión las ejecutaron. `CLAUDE.md` declara 642 pruebas en verde en otro corte.

Clasificaciones demasiado optimistas:

| Regla | Fase 4 | Revisión | Motivo |
|---|---|---|---|
| RN-088 Alerta de vigencia | TESTEADA | **PARCIAL** | Se prueban 5 de 7 fuentes; no se prueban anulación, asignación de ID, borrados ni recálculo parcial. |
| RN-055 Prioridad de incidencias | TESTEADA | **PARCIAL** | La supresión por justificación solo se prueba con `MarcacionIncompleta` (NH-03). |
| RN-024 Rotación | TESTEADA | TESTEADA para la aritmética; **la regla de negocio, SIN TEST** | Ninguna prueba combina el ciclo con `DiasAplicables` (domingo dentro de un bloque de 14 días). |
| RN-019 Población y vínculo | PARCIAL | PARCIAL; el **mantenimiento del vínculo, SIN TEST** en el camino real | `BajaTrabajadorTests` llena la colección a mano y no pasa por `TrabajadorRepository` (NH-02). |
| RN-090 Liquidación | SIN TEST | SIN TEST **con defecto** | NH-01 habría salido con un solo caso de domingo. |
| RN-094 Histórico en Excel | PARCIAL | PARCIAL | No se prueban OBS «CESE» posterior ni traslado de planta. |
| RN-047 / RN-046 Horas extra | TESTEADA | TESTEADA **contra la regla documentada en `CLAUDE.md`**, que la regla confirmada desmiente | Que la prueba pase no significa que la regla sea correcta (NH-07). |
| PMV-RN-012 Autorización | SIN TEST | SIN TEST | Coincide. No hay pruebas Web de autorización PMV. |

Prioridades sin cobertura directa: horas extra frente a la regla confirmada, sábado y domingo con DOM1/DOM2, descanso en rotación, liquidación, supresión de incidencias, mantenimiento de `PeriodoLaboral`, alerta con recálculo parcial y borrados, autorización PMV.

---

## 11. Contradicciones reclasificadas

| ID | Contradicción | Claude | Severidad | Tipo |
|---|---|---|---|---|
| C-01 | `TareoDiario` documenta «ordinario hasta 480»; el motor usa `MinutosJornadaOrdinaria` | VALIDADA; además ambas formas difieren de la regla confirmada (NH-07) | Baja como documento; crítica como regla | DOCUMENTACIÓN DESACTUALIZADA (+ GAP vía NH-07) |
| C-02 | El contrato de importación dice que «resuelve contra el padrón» | VALIDADA [DOC] | Baja | DOCUMENTACIÓN DESACTUALIZADA |
| C-03 | La semántica de `FilasRechazadas` | VALIDADA [DOC] | Baja | DOCUMENTACIÓN DESACTUALIZADA |
| C-04 | Tres hojas contra cuatro | VALIDADA CON MATICES: `CLAUDE.md` sí documenta la hoja `Trazabilidad`; solo quedan comentarios viejos | Baja | DOCUMENTACIÓN DESACTUALIZADA |
| C-05 | Nombres AO–BF contra columnas AN–BE | VALIDADA [DOC] | Media (mapeo externo) | DOCUMENTACIÓN DESACTUALIZADA |
| C-06 | Nocturnidad superpuesta o excluyente | VALIDADA; además AL ⊂ AI | Media | REQUIERE NEGOCIO |
| C-07 | «Nada se borra» frente al borrado de justificaciones | VALIDADA y ampliada: **dos** vías de borrado físico; auditadas por el interceptor; rompen la alerta (NH-04) | Media | BUG POTENCIAL (invariante 7 violado sin ADR) |
| C-08 | Solicitud «Aprobada» con días omitidos | VALIDADA [DOC] | Media | REQUIERE NEGOCIO |
| C-09 | `PeriodoLaboral` existe pero no gobierna | DIVIDIDA: consumo plano = decisión consciente; duplicados = NH-02 | Alta (integridad) | DECISIÓN DE DISEÑO + BUG CONFIRMADO |
| C-10 | Tasas con empresa no filtradas | VALIDADO el mecanismo; refutado como riesgo presente (§7.2) | Baja | BUG POTENCIAL (latente) |
| C-11 | `MAX(CalculadoEn)` oculta recálculos parciales | **Promovida** de inferencia a confirmada por código (§8.1) | Alta | BUG CONFIRMADO (por código, no ejecutado) |
| C-12 | Fuentes omitidas en la alerta | VALIDADA y depurada: feriados, tipo de ausencia, equipos y fecha de ingreso sí deberían estar; parámetros, sueldos y reintegros no afectan al tareo | Media-alta | GAP FUNCIONAL |

---

## 12. Preguntas de negocio reclasificadas

| # | Pregunta (Fase 4) | Clasificación | Nota |
|---:|---|---|---|
| 1 | ¿Jornada ordinaria de 12 h, o limitada a 8? | **BLOQUEA DISEÑO** | La regla confirmada (17:22, sábado entero como extra) **responde en parte**, pero contradice `CLAUDE.md`. Hay que reconciliar cuál rige y desde qué fecha (vigencia). Falta el turno nocturno. |
| 2 | Refrigerio en jornadas cortas o nocturnas | IMPORTANTE PERO NO BLOQUEANTE | Hoy vale 0. Se vuelve bloqueante si la hipótesis de los 46 min se confirma. |
| 3 | Tolerancia: umbral contra corrimiento | IMPORTANTE PERO NO BLOQUEANTE | El código ya lo marca «⚠️ Confirmar con GCA» (L983). |
| 4 | ¿Margen de 6 h universal? | PUEDE RESOLVERSE POR CÓDIGO / DATOS | Medible sobre los attlog reales: distribución de marcas tras el fin del turno. |
| 5 | Primera y última marca ante trabajos partidos | IMPORTANTE PERO NO BLOQUEANTE | Es una regla de diseño documentada; falta la confirmación de GCA. |
| 6 | Nocturnidad superpuesta o excluyente | **BLOQUEA DISEÑO** (acotado a AJ–AL e interpretación de AI/AL) | El bono nocturno en soles no depende de esta respuesta. |
| 7 | ¿Cualquier minuto nocturno da el bono diario completo? | IMPORTANTE PERO NO BLOQUEANTE | Afecta dinero. Hoy: cualquier minuto (`> 0`). |
| 8 | ¿Estado parcial de la solicitud? | IMPORTANTE PERO NO BLOQUEANTE | — |
| 9 | Tardanza y salida anticipada justificadas el mismo día | IMPORTANTE PERO NO BLOQUEANTE | Se extiende a permiso + regularización (NH-03). |
| 10 | ¿Tasa por empresa o global? | PUEDE RESOLVERSE POR CÓDIGO | Sin ruta de escritura ni datos con empresa: no es urgente (§7.2). |
| 11 | ¿Qué se congela al cerrar el periodo? | **BLOQUEA DISEÑO** | Planta, CECO, `TieneHijos` y cese alteran Excel cerrados (§8.2). |
| 12 | ¿Bloquear la exportación si está desactualizado? | IMPORTANTE PERO NO BLOQUEANTE | Antes hay que hacer fiable la alerta (§8.1). |
| 13 | Reingresos y duplicados de `PeriodoLaboral` | PUEDE RESOLVERSE POR CÓDIGO (duplicados) | Los duplicados son artefacto de NH-02, no reingresos. Se puede confirmar cruzando `registro_auditoria` (modificaciones de `trabajador`) con el `CreadoEn` de la segunda fila. Los reingresos siguen siendo negocio, sin urgencia. |
| 14 | ¿PMV respeta el alcance? ¿Resuelve la rotación? | **YA ESTÁ RESPONDIDA** (alcance) | El invariante de alcance existe y se aplica en todo el sistema (NH-09). La rotación en PMV es de negocio y sin urgencia. |
| 15 | ¿Qué hacer con las 165 jornadas sin dueño? | PUEDE RESOLVERSE POR CÓDIGO (clasificación) | Consulta de solo lectura (NH-12). Decidir la acción sigue siendo de negocio. |

Totales: **BLOQUEA DISEÑO 3** · **IMPORTANTE PERO NO BLOQUEANTE 7** · **PUEDE RESOLVERSE POR CÓDIGO 4** · **YA ESTÁ RESPONDIDA 1**.

---

## 13. Priorización P0–P3

| Hallazgo | Severidad | Probabilidad | Impacto | Requiere negocio | Requiere código | Prioridad |
|---|---|---|---|---|---|---|
| NH-07 Horas extra frente a la regla confirmada y contradicción con `CLAUDE.md` | Crítica | Cierta (cada día laborable y cada sábado) | Pago de sobretiempo en todo trabajador DIU/NOC | **Sí** (reconciliar) | Sí | **P0** |
| PMV sin alcance de planta (PMV-RN-012 + NH-09) | Alta | Media (está en el desplegable) | Datos personales de la otra razón social | No | Sí | **P0** |
| NH-01 Doble conteo en la Liquidación | Alta | Cierta en cuanto hay noches o domingos | Horas equivalentes infladas en una pantalla «para planilla» | No | Sí | **P1** |
| NH-03 Supresión de incidencias por justificación | Alta | Media | Día trabajado que queda en 0 h, invisible y sin vía de regularización | Parcial (pregunta 9) | Sí | **P1** |
| §8.1 + NH-04 Alerta de vigencia | Alta | Media-alta (Administradores por planta) | Excel «al día» desactualizado enviado a planilla | Pregunta 12 | Sí | **P1** |
| NH-02 Mantenimiento de `PeriodoLaboral` | Alta (integridad) | Cierta (cada edición) | Historial corrupto que crece; bloquea su uso futuro | Elegir fila sobreviviente | Sí | **P1** |
| NH-11 / §3.1 Descanso móvil en la rotación | Alta | Cierta al activar cuadrillas | Faltas o 100 % equivocados en todo el personal rotativo | Sí | Sí (modelo) | **P1** |
| §8.2 / NH-06 / NH-05 Excel histórico mutable | Media-alta | Media | Reemisiones que no coinciden con lo pagado | **Sí** (pregunta 11) | Sí | **P1** (decisión) / P2 (código) |
| NH-08 Domingo con DOM1/DOM2 | Media | Baja-media | Domingo sin 100 % | Sí | Sí | **P2** |
| §8.6 Refrigerio y nocturnidad | Media | Hoy nula | Doble base si el refrigerio pasa a ser no pagado | Sí | Sí | **P2** |
| §8.3 Importación no atómica | Media | Baja | Historial inconsistente; recuperable | Pregunta 7 de 3B | Sí | **P2** |
| C-08 Aprobación parcial | Media | Media | Estado engañoso | Sí | Sí | **P2** |
| C-07 Borrado físico de justificaciones | Media | Cierta al retirar | Invariante 7; mitigado por la auditoría | ADR | Sí | **P2** |
| Pregunta 7 Bono por cualquier minuto nocturno | Media | Media | Bono diario completo por minutos | Sí | Quizá | **P2** |
| §3.3 Sin «validar sin corregir» en la bandeja | Baja | Alta | Ruido permanente en la bandeja | No | Sí | **P3** |
| §7.2 Tasas por empresa | Baja | Muy baja | Ninguno sin SQL directo | Pregunta 10 | Mínimo | **P3** |
| PMV rango, hash y etiqueta de cuadrilla | Baja | Baja | Funcional menor | Parcial | Sí | **P3** |
| §8.5 Sin linaje de versión y marcas | Baja-media | Baja | Explicabilidad en auditoría | No | Opcional | **P3** |
| Documentación (C-01..C-05, NH-09, NH-10) | Baja | — | Confusión | No | Solo documentos | **P3** |
| Margen de 6 h, rebote, columnas vacías | Baja | — | Decisiones documentadas | Confirmar | No | **P3** |

Totales: **P0 = 2 · P1 = 6 · P2 = 6 · P3 = 6**.

---

## 14. Qué debe abordar Fase 5

1. **Antes de cualquier diseño:** una matriz de reglas vigentes por fecha que reconcilie `CLAUDE.md` («Reglas confirmadas por GCA») con la regla 17:22 / sábado / domingo, y que fije el turno nocturno, el refrigerio y la tolerancia. Sin esto, todo cambio del motor se construye sobre dos verdades.
2. **Casos de aceptación por regla**, con fechas literales: DIU puntual y tardío, sábado de 8 y de 12 h, domingo DIU y DOM1, noche con extras, rotación de 22 días con descanso móvil, permiso más olvido de salida. Escritos como especificación ejecutable, no implementados todavía.
3. **Pruebas dirigidas de solo lectura** (no la suite entera) que confirmen NH-01, NH-02 (ruta del repositorio), NH-03 y NH-04, y el falso «al día» tras un recálculo parcial.
4. **Consultas de solo lectura** sobre la base canónica: clasificar las 165 jornadas PMV sin dueño (NH-12); confirmar NH-02 cruzando `registro_auditoria` con el `CreadoEn` de las filas duplicadas de `periodo_laboral`; inventariar las tasas con `EmpresaId` (se espera 0).
5. **Decisiones de negocio** sobre las 3 preguntas bloqueantes (1, 6, 11).
6. **Plan de corrección priorizado** P0→P1 como documento, sin ejecutarlo. Incluye el plan de reparación de datos de `periodo_laboral`, que requiere elegir con autor qué fila sobrevive.
7. **Actualizar `CLAUDE.md`** en alcance por planta, hojas y columnas, solo cuando se autorice.

---

## 15. Nivel de confianza

- **Sobre la implementación documentada en Fase 4:** ALTA. Las fórmulas, el linaje y las fronteras revisadas coinciden con el código.
- **Sobre la calibración de riesgos de Fase 4:** MEDIA. Un crítico sobrevalorado, un crítico mal diagnosticado y cuatro hallazgos de severidad alta ausentes.
- **Sobre la alineación del sistema con el negocio:** BAJA en horas extra y descanso rotativo, por la contradicción de fuentes y el gap de modelado.
- **Comprensión técnica estimada tras la revisión:** ~93 %. La parte que falta es de negocio, no de código.
- Límites de esta revisión: nada se ejecutó; NH-01..NH-05 están confirmados por lectura de código, no por ejecución; la base no se abrió.

---

## Anexo A — Registro de verificaciones selectivas

| # | Afirmación | Fuente documental | Archivo / método revisado | Motivo | Resultado |
|---:|---|---|---|---|---|
| 1 | Tasas con empresa no filtradas | Fase 4 crítico #2, C-10 | `TasaSobretasa.cs`; `ServicioCalculoTareo.LiquidarPeriodoAsync` L796-808; `ServicioReportes.ObtenerTasaSobretasaAsync` L885-892 | Severidad | Mecanismo cierto; sin ruta de escritura → latente |
| 2 | ¿Existe un alta o edición de tasas? | — | Búsqueda de `new TasaSobretasa` / `TasasSobretasa.Add` en `src`; búsqueda de «Tasa» en `Web` | Reproducibilidad | Solo la semilla (`EmpresaId` nulo); ninguna vista |
| 3 | La alerta usa el máximo y siete fuentes | RN-088, C-11, C-12 | `ServicioReportes.EvaluarVigenciaDelCalculoAsync` L488-593; `EstadoCalculoTareo` | Completitud | Confirmado; sin filtro por planta, a propósito |
| 4 | El recálculo del Administrador es parcial | RN-093 | `ServicioCalculoTareo` L67-76 | Falso «al día» | Confirmado |
| 5 | Borrado físico de justificaciones | C-07 | `ServicioSolicitudes.AnularAsync` L399-449; `ServicioJustificaciones.EliminarAsync` L577-614; `InterceptorAuditoria` L232-234 | Alerta e invariante | Dos vías de borrado; auditadas; la alerta no las ve |
| 6 | Pruebas de la alerta | RN-088 TESTEADA | `AlcanceYVigenciaReportesTests` (nombres) | Cobertura | 5 fuentes probadas; faltan anulación, borrado, parcial y feriados |
| 7 | Aritmética de ordinario y extra | RN-046/047 | `CalculoTareo.Jornada` L870-965 | Comparación con la regla confirmada | Duración de 480 desde la entrada; diverge de 17:22 |
| 8 | Turnos sembrados | — | `DatosSemilla.cs` L430-479 | Sábado, domingo y refrigerio | DIU/NOC de lunes a sábado; DOM1/DOM2 solo domingo; refrigerio 0 |
| 9 | ¿La regla 17:22 está documentada? | — | Búsqueda en `*.md` | Fuente de la regla | No está en el repositorio |
| 10 | Liquidación y horas equivalentes | RN-090 | `LiquidarPeriodoAsync` L772-831; `SobretasaDtos.cs`; `Asistencia/Index.cshtml` L271-293 | Fórmula | Doble conteo (NH-01) |
| 11 | Prioridad de incidencias | RN-055 | `CalculoTareo.ConIncidencia` L358-421; `Aplicar*` L544-784 | Exclusividad | Supresión por cualquier justificación (NH-03) |
| 12 | Prueba del día justificado | RN-055 | `IncidenciasDelDiaTests` L286-304 | Cobertura | Solo `MarcacionIncompleta` |
| 13 | Nocturnidad y refrigerio | RN-051/052 | `CalculoTareo` L288-294, L910-939, L1013-1039 | Comportamiento exacto | Confirmado; refrigerio latente |
| 14 | Turno descanso posible | RN-025 | `ServicioTurnos.cs:323` | Atajo de modelado | Rechazado: el gap queda confirmado |
| 15 | PMV sin alcance | PMV-RN-012 | `MarcacionesProvisionalesController` L13-145; `ProveedorAlcancePlantasHttp` L10-62 | Severidad | Confirmado; el alcance existe en el resto del sistema |
| 16 | OBS «CESE» | Excel K | `ServicioReportes.cs:1182-1184` | Histórico | Retroactivo (NH-05) |
| 17 | Padrón del reporte | RN-094 | `ServicioReportes.ResolverTrabajadoresAsync` L645-719 | Histórico | Filtros por empresa, planta y CECO actuales (NH-06) |
| 18 | Origen de los duplicados de `PeriodoLaboral` | 3B §12.1, pregunta 13 | `TrabajadorRepository.ObtenerPorIdAsync`; `ServicioTrabajadores` L262-378, L695-721; `Trabajador` L148-244; búsqueda de carga diferida | Causa raíz | Falta el `Include` → NH-02 |
| 19 | Pruebas del vínculo | — | `BajaTrabajadorTests` (búsqueda) | Por qué no se detectó | Colección armada en memoria, sin repositorio |
| 20 | Recuento de cobertura | Fase 4 §12 | `BUSINESS_RULES.md` (recuento por columna) | Aritmética | 83 / 14 / 11 |
| 21 | Acciones de la bandeja | §15 del encargo | `IncidenciasController` (firmas) | Correcciones manuales | Solo `Regularizar` y `Resolver` |

---

```text
Reglas revisadas: 108 (lectura documental completa); 49 con veredicto explícito
Validadas: 32
Matizadas: 12
Refutadas: 4 (conclusiones: el resumen de cobertura, la severidad de las tasas,
          los reintegros en el Excel cerrado y el origen de los duplicados de 3B;
          ninguna regla del catálogo describe mal el código)
No demostradas: 5
Nuevos hallazgos: 12

Contradicciones revisadas: 12
Preguntas de negocio reclasificadas: 15 (3 bloquean diseño · 7 importantes · 4 resolubles por código · 1 ya respondida)

P0: 2
P1: 6
P2: 6
P3: 6

¿Existe algún hallazgo que obligue a detener la ingeniería inversa?:
NO. Ninguno invalida la reconstrucción técnica. Sí hay uno que obliga a NO diseñar
cambios del motor todavía: NH-07 (la regla de horas extra confirmada contradice tanto
la implementación como las «Reglas confirmadas por GCA» de CLAUDE.md).

Recomendación para Fase 5:
Convertir reglas en especificación verificable antes que en código: reconciliar la
matriz de reglas vigentes (horas extra, sábado, domingo, refrigerio, descanso móvil),
escribir casos de aceptación con fechas literales, confirmar NH-01..NH-04 con pruebas
dirigidas, clasificar con consultas de solo lectura los datos PMV y de periodo_laboral,
y producir un plan de corrección P0→P1 sin ejecutarlo.
```

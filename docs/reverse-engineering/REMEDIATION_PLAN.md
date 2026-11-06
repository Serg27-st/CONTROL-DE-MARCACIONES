# Plan de remediación P0–P3

Fecha de corte: 2026-10-09  
Restricción de Fase 5: plan solamente; no se modificó código de producción ni la base canónica.

## Principios de ejecución

- Separar siempre **corrección de código** de **reparación de datos**.
- Congelar primero los contratos de aceptación; después corregir por una prioridad a la vez.
- Ejecutar migraciones y reparaciones sobre copia verificada, con conteos antes/después y reversa documentada.
- No cerrar un hallazgo por tener un test cercano: el caso exacto debe pasar.

## Overlay vigente de Fase 7

El inventario de hardening del 2026-10-09 está en
`PHASE_07_HARDENING_INVENTORY.md` y prevalece para la priorización nueva. Reprodujo build
0/0 y 807/807 pruebas, sin ejecutar OP-03 ni modificar producción. Añade 23 hallazgos
(2 P0, 6 P1, 11 P2, 4 P3), incluidos seguridad de configuración, higiene de bases SQLite,
readiness de arranque y cobertura HTTP.

Las cifras antiguas de `DATA-PERIODOLABORAL` (85 grupos/170 filas) quedan como evidencia
histórica **OBSOLETA para planificación actual**. La base local auditada en Fase 7 presentó 307
trabajadores con múltiples periodos abiertos, 868 filas abiertas involucradas y 900 pares
solapados. Fase 3B saneó todos esos casos solo sobre copia controlada; antes de intervenir otra
base debe repetirse el perfilado en modo read-only y conservarse la separación entre reparación
de datos y cambio de esquema.

### Estado Fase 7A — corte histórico, superado para SEG-006

- P0-SEC-01: en este corte estaba contenido localmente y abierto por rotación; **SUPERADO** por
  `P0-SEG-006-FINAL` del 2026-10-19.
- P0-DATA-01: **CONTENIDO**; 76/76 copias verificadas, originales retirados, cero sensibles en
  el árbol y cero archivos SQLite/backup en el índice actual.
- Baseline posterior: 809/809, 0 fallos, 0 omitidos; build 0/0.
- Se ejecutó cuarentena local autorizada y `git rm --cached` solo sobre cuatro archivos; no se
  reescribió historia ni se hizo cambio productivo.

Fuente: `PHASE_07A_SECURITY_DATA_CONTAINMENT.md`.

### P1-STARTUP-HEALTH — resuelto

El proceso vivo y la disponibilidad operativa ya no se confunden. `/vivo` conserva detección
de instancia; `/estado` devuelve 503 ante fallo de migración, esquema atrasado/futuro,
estructura física incompleta, integridad o FK inválida, y vuelve a 200 tras reparación compatible.
Mientras la preparación está incompleta, todas las rutas funcionales responden 503; solo
`/vivo` y `/estado` permanecen expuestos. Contratos SH-01..07 verdes. Baseline vigente:
862/862 y build 0/0. Ver `P1_STARTUP_HEALTH.md`.

## Backlog maestro

| ID | Problema | Tipo | Evidencia | Cambio futuro | Test requerido | Prioridad |
|---|---|---|---|---|---|---|
| P0-01 | Corte ordinario, umbral HE, sábado y domingo | GAP FUNCIONAL/BUG · **RESUELTO EN CÓDIGO** (R-P0-03 + R-P0-03.1) | HE-01..05, DOM-01..05, HE-DIU, HE-NOC, canónicos, HE-SAB y LIQ-04 verdes | Corte en `FinTurno` teórico, umbral de 15 min L–V, sábado sin ordinarios ni umbral, domingo 100 % sin umbral | contratos citados | RESUELTO; queda la acción operativa OP-01 |
| OP-01 | DIU/NOC sembrados 07:00–19:00 / 19:00–07:00 | CONFIGURACIÓN DE DATOS · **VALIDADO EN COPIA** | `OP_01_SHIFT_VERSION_UPDATE.md`: versiones nuevas sin reescritura; piloto conforme | Ejecutar `OP_01_PRODUCTION_PROCEDURE.md` (rollback verificado R1–R7) | Verificar `version_turno` y el recálculo | **Bloqueado por FechaRigeDesde (negocio)** |
| UX-01 | Editar un turno con el mismo horario y otra fecha no hace nada y no avisa; una fecha ≤ inicio de la versión abierta reescribe en sitio sin confirmación | UX/RIESGO OPERATIVO | OP-01 R1/R6 | Avisar o confirmar en `/Turnos` | Prueba de servicio/controlador | P2 |
| DEPLOY-01 | Nuevas instalaciones siembran DIU/NOC obsoletos | DATA/DEPLOYMENT | `DatosSemilla` (HasData) | Decidir entre migración con versión nueva y paso de instalación documentado | Instalación limpia con horarios vigentes | P2 |
| P0-02 | PMV cruza plantas | SEGURIDAD · **RESUELTO** | SEC-01..04 verdes; suite sin regresiones | `IProveedorAlcancePlantas` aplicado en listado/historial, importación y consulta/exportación | SEC-01..04 | RESUELTO |
| P0-03 | Tolerancia 5 aplicada a planilla | BUG · **RESUELTO** | TAR-00..04 y nocturno verdes | Planilla calcula desde inicio teórico; campo histórico no interviene | TAR-00..04 | RESUELTO |
| P0-04 | Tolerancia 5 aplicada a salida anticipada | BUG · **RESUELTO** | SA-00..04 y nocturno verdes | Planilla calcula hasta fin teórico; campo histórico no interviene | SA-00..04 | RESUELTO |
| P1-01 | Doble conteo en liquidación | BUG · **RESUELTO** (R-P1-01) | LIQ-01..04 y LIQ-HE-NOC-01 verdes | Nocturnidad aporta solo prima; no laborables trabajados no conservan base ordinaria duplicada | contratos citados | RESUELTO |
| P0-FER-002/007 | Economía de feriado trabajado y 1 de mayo | GAP FUNCIONAL · **RESUELTO** | derecho ordinario separado; base + 100 % total/parcial; cuatro/dos componentes del 1 de mayo; 15/15 dirigidos y 958/958 globales | Mantener días-persona separados de minutos físicos; no inventar sustitutorio | FER-001/002/007 y FER-NAC-05/06 | RESUELTO; FER-003/005 siguen separados |
| P1-02 | Duplicados/cese de PeriodoLaboral | BUG · **RESUELTO Y CONSOLIDADO** (R-P1-02 + 3D) | PL-01/02 y PL3D verdes | Historia completa, cese exacto y persistencia selectiva | PL-01/02, PL3D | RESUELTO |
| DATA-PERIODOLABORAL | Historia canónica, duplicados y consumidores legacy | DATOS + CONCURRENCIA · **CERRADO TÉCNICAMENTE 3B–3E-E** | Corte antiguo 85/170 **OBSOLETO**; copia 3B: 307 trabajadores/868 filas/900 solapamientos → 0; 352 plantas Nivel A; 937/937 y 108/108 | Mantener contratos y repetir inventario/dry-run solo ante una operación productiva autorizada | 3B–3E-D, FK, concurrencia, rollback y regresión integrada | CERRADO; producción no ejecutada |
| P1-03 | Incidencia suprimida | BUG · **RESUELTO** (R-P1-03) | INC-01..03 e INC-PARCIAL-01 verdes | Incidencia derivada del resultado posterior a la justificación, no de `JustificacionId` | contratos citados | RESUELTO |
| P1-04 | Falso “tareo actualizado” | BUG · **RESUELTO** (R-P1-04) | `MIN(CalculadoEn)` por periodo/planta + fuentes usadas + auditoría para borrados | Combinación localizada, sin persistencia nueva | VIG-01..05 y VIG-07..09 | RESUELTO |
| P1-05 | Descanso móvil no modelable | **RESUELTO** (R-P1-05) | tipo explícito, secuencia finita, dos pasadas BD-01 y migración SQLite | Mantener contratos; no añadir repetición ni turno ficticio | ROT-DESC-01..09, ROT-MARK-01..06, ROT-FER-01/02, ROT-REP-01 verdes | RESUELTO |
| OP-02 / OP-02B | Ensayo operativo de migración ROT-DESC | **VALIDADO; OP-03 NO EJECUTADO** | 7 programaciones/13 tramos controlados sin regresión; 34/34 resoluciones; restore real; UI-ROT-01..07; 807/807 pruebas | Preparar OP-03 con parada de escrituras, backup restaurable y verificación inmediata por advertencia no transaccional de EF | `OP_02B_ROT_DESC_REPRESENTATIVE_REHEARSAL.md`, `R_WEB_ROT_DESC_FORM_FIX.md` | LISTO PARA PREPARAR OP-03 |
| OP-03A | Readiness previo a ventana ROT-DESC | **NO VALIDADO; NO CALENDARIZAR** | release frozen y hashes; 807/807; historial candidato read-only; cadena real de 7 migraciones ensayada con integrity ok/FK 0; piloto y storage analizados | Cerrar ruta efectiva, smoke autenticado, aprobación piloto/período, responsables, backup final y cuenta ADMINISTRADOR limitada | `OP_03A_ROT_DESC_PRODUCTION_READINESS.md`, artifacts OP-03A | BLOQUEADO |
| P1-06 | Histórico mutable fuera del contexto laboral | GAP FUNCIONAL | vínculo, empresa y planta ya resueltos; quedan otros maestros actuales/no congelados | snapshot o resolución histórica para los maestros residuales | traslado y reemisión | P1 separado; no reabre DATA-PERIODOLABORAL |
| P1-07 | Importación principal no atómica | DEUDA TÉCNICA · **RESUELTA** (R-P1-07) | dos `SaveChanges` y aprendizaje de serie sin transacción común | transacción SQLite tras parsing/dedupe; commit único y rollback con propagación | IMP-AT-01..07 verdes | RESUELTO |
| P1-08 | Ausencia/licencia pagada completa acredita 480 | REQUISITO VIGENTE · **CERRADO** (R-P1-08) | `Aplicar` usa `MinutosJornadaOrdinaria`; DIU/NOC conservan 480 aunque el intervalo sea 622 | Sin cambio productivo; contratos y documentación reconciliados | AUS-PAG-01..05 verdes | RESUELTO |
| P2-02 | Aprobación parcial | BUG/GAP | servicio permite omisiones | todo-o-nada o estado parcial | choque/periodo cerrado | P2 |
| P2-03 | Borrado físico | DATOS/DISEÑO · **RESUELTO (P2-AUS-008)** | La anulación conserva `Justificacion` como `Cancelada`, con resolución, versión y guarda de periodo | Mantener contratos; no reconstruir historia perdida sin fuente | AUS008-01..06 | RESUELTO |
| P2-04 | Fórmula nocturna/refrigerio | REQUIERE NEGOCIO | políticas divergentes | decisión formal antes de código | matriz nocturna | P2 |
| P3-01 | Drift, hardcodes y logging | DOCUMENTACIÓN/DEUDA | inventario Fase 5 | centralizar dueños y métricas | fronteras/configuración | P3 |

## P0 — antes de usar resultados para pago o exponer PMV a administradores

| ID | Problema | Código | Datos | Criterio de salida |
|---|---|---|---|---|
| P0-01 | **RESUELTO EN CÓDIGO (R-P0-03 + R-P0-03.1)** — DIU 07:00–17:22, NOC 19:00–05:22, umbral HE de 15 min L–V, sábado completo y domingo completo al 100 % sin umbral | Corte en `FinTurno` teórico (`Repartir`) y umbral en `Jornada`; sin migración, liquidación, refrigerio, nocturnidad económica ni rotaciones tocadas. Tests históricos actualizados por requisito posterior (6 en R-P0-03, 10 en R-P0-03.1) | **OP-01**: nueva versión de DIU/NOC (la semilla sigue en 19:00/07:00) y recálculo de periodos abiertos; periodos cerrados requieren decisión formal de reliquidación | Contratos HE/DOM/DIU/NOC verdes; 0 regresiones inesperadas |
| P0-02 | **RESUELTO** — PMV-RN-012: fuga entre plantas | `IProveedorAlcancePlantas` filtra catálogo/historial y valida equipo en importación/consulta; exportación usa la consulta protegida | No requirió reparación ni migración | SEC-01..04 verdes; alcance global existente conservado |
| P0-03 | **RESUELTO** — tardanza de planilla usaba tolerancia configurada | Cálculo minuto a minuto desde la entrada teórica, sin consumir `MinutosTolerancia` | No se recalcularon datos ni periodos cerrados | TAR-00..04 y nocturno verdes |
| P0-04 | **RESUELTO** — salida anticipada usaba anticipación configurada | Cálculo minuto a minuto hasta la salida teórica, sin consumir `MinutosAnticipacionSalida` | No se recalcularon datos ni periodos cerrados | SA-00..04 y nocturno verdes |

## P1 — integridad y exactitud operativa

| ID | Problema | Código | Datos | Criterio de salida |
|---|---|---|---|---|
| P1-01 | **RESUELTO** — NH-01: doble base en liquidación nocturna/no laborable | `ConceptoLiquidado` trata nocturnidad como prima; agregación excluye ordinarios no laborables y deriva HE100 del remanente sábado→domingo | No se modificó `tareo_diario`; revisar externamente liquidaciones emitidas si existieran | LIQ-01..04 y LIQ-HE-NOC-01 verdes; 0 regresiones inesperadas |
| P1-02 | **RESUELTO Y CONSOLIDADO** — NH-02 y escrituras futuras | El repositorio carga la historia completa y persiste selectivamente; cese/reingreso validan cardinalidad e invariantes | Ningún dato productivo modificado | PL-01/02, PL3D y PL3EC verdes |
| DATA-PERIODOLABORAL | **CERRADO TÉCNICAMENTE 3B–3E-E** | Índices parciales, guardas de dominio, resolución canónica, consumidores y reingreso completados | Saneamiento/backfill ejecutados únicamente sobre copia controlada: 307 múltiples abiertos y 900 solapamientos → 0; producción intacta | 937/937, regresión dirigida 108/108, FK/integridad/rollback/concurrencia verdes |
| P1-03 | **RESUELTO (R-P1-03)** — NH-03: justificación parcial suprimía incidencia | `ConIncidencia` evalúa el estado posterior; permiso, tardanza o regularización parcial conservan el extremo pendiente | Sin reparación ni migración; los días se corrigen al recalcular | INC-01..03 e INC-PARCIAL-01 verdes; regularización completa fuera de pendientes |
| P1-04 | **RESUELTO (R-P1-04)** — NH-04/C-11/C-12: vigencia incompleta | Detector usa la marca calculada más antigua del periodo + plantas visibles; observa entradas reales y usa auditoría existente para eliminación/anulación | Sin migración, reparación ni recálculo automático; periodos cerrados solo se señalan | VIG-01..05 y VIG-07..09 verdes; VIG-06 no aplica |
| P1-05 | **RESUELTO Y VALIDADO EN WEB** — NH-11: descanso móvil explícito | `Tipo`, FK nullable, `CHECK`, resolución discriminada, secuencia finita, tareo sin turno ficticio y formulario MVC con validación condicional | `DescansoProgramadoEnRotaciones` hace backfill solo a Trabajo; OP-02B confirmó datos controlados, restore y smoke autenticado; no quedó aplicada a producción | 807/807 verdes; contratos ROT-DESC/MARK/FER/REP y UI-ROT-01..07 |
| P1-06 | NH-05/NH-06: reporte histórico mutable | Definir snapshot o resolución histórica para planta, área, sección, CECO, puesto, hijos y cese | Si se adopta snapshot, backfill solo con evidencia histórica suficiente | Reemisión cerrada reproduce exactamente archivo aprobado |
| P1-07 | **RESUELTO (R-P1-07)** — importación principal no atómica | Transacción del `MarcacionDbContext` abarca serie aprendida, cabecera/errores, marcas y auditoría; parsing y dedupe quedan fuera | Sin migración ni reparación automática; cualquier inventario histórico sería una intervención de datos separada | IMP-AT-01..07 verdes; fallo técnico deja cero filas nuevas y propaga la excepción |

## P2 — reglas incompletas y preservación de evidencia

| ID | Acción | Código | Datos/operación |
|---|---|---|---|
| P2-02 | Aprobación parcial de solicitudes | Hacer aprobación todo-o-nada o estado explícito `AprobadaParcial` | Inventariar solicitudes aprobadas con días sin justificación |
| P2-03 | **RESUELTO (P2-AUS-008)** — borrado físico de justificaciones | `AnularAsync` conserva filas como `Cancelada`, registra resolución, incrementa versión y afirma periodos; la bandeja prioriza la vigente | Base controlada sin filas afectadas; no recrear evidencia histórica sin fuente |
| P2-05 | Refrigerio/nocturnidad | Resolver con negocio descuento, tramo y jornadas cortas antes de tocar fórmula | Sin reparación hasta decisión formal |
| P2-06 | Tasas por empresa/fecha | Aplicar `EmpresaId`, unicidad y política temporal determinista | Auditar tasas cargadas por SQL directo |

## P3 — mantenibilidad, observabilidad y experiencia

- Decidir el destino técnico de `MinutosJornadaOrdinaria`, que ya no decide el corte L–V pero sigue rigiendo feriados/descansos fuera de domingo y el crédito confirmado de ausencia pagada completa (480, no 622). El valor funcional de ese crédito ya no está pendiente. El umbral de 15 min es constante del motor: decidir si pasa a dato versionado.
- Centralizar constantes de dominio hoy dispersas: 480, 360/6 h, 22:00, 06:00, 5 min, 20 MB, 30 y 40 días. No todas deben ser configurables, pero cada una debe tener dueño y test de frontera.
- Eliminar contradicciones de `CLAUDE.md`, XML comments y documentación antigua después de aceptar P0-01; conservar registro histórico, no reescribir fases cerradas.
- Añadir logs estructurados de inicio/fin, filas, duración, periodo, planta y resultado para importación, recálculo y exportación; nunca DNI/nombres en claro.
- Añadir correlación y métricas de fallos transaccionales, invalidaciones y exportaciones bloqueadas.
- Separar el servicio PMV temporal mediante fecha/criterio de retiro; reducir duplicación de consulta/exportación con el flujo principal solo después de estabilizar reglas.
- Agregar test Web para roles y alcance; hoy la cobertura se concentra en dominio e infraestructura.

## Secuencia recomendada

1. Aprobar formalmente `ACCEPTANCE_RULES.md`.
2. Corregir P0-02 (aislamiento) y desplegar independientemente.
3. Corregir juntos P0-01, P0-03 y P0-04 en el contrato del motor, con feature flag o periodo piloto; no recalcular meses cerrados automáticamente.
4. **SUPERADA:** la instrucción de iniciar `DATA-PERIODOLABORAL` se cumplió en 3B–3E-E sobre
   código y copias controladas. No repetirla. Si se autoriza producción, tratarla como una
   operación nueva: copia contemporánea, inventario, dry-run, backup, ventana y reversa.
5. P1-01/P1-02/P1-03/P1-04 resueltos; conservar sus contratos.
6. R-P1-07 y R-P1-05 quedaron resueltos como cambios aislados. Mantener P1-06 separado.
   OP-02B está validado tras R-WEB; OP-03 tiene runbook y OP-03A congeló un release y ensayó
   las siete migraciones pendientes. No calendarizar hasta cerrar R1/R4/R6/R7/R8/R9/R11.
   Mantener backup, parada de escrituras y verificación inmediata para ROT-DESC.
   REG-01..04 confirman que el flujo estándar de regularización conserva y audita la marca.
7. Completar P2/P3 con métricas de rendimiento y operación.

## Reversibilidad

- Código: cambios pequeños por hallazgo, despliegues independientes y bandera para motor nuevo.
- Datos: respaldo, script idempotente, tabla de correspondencia `fila_origen → fila_sobreviviente`, conteos y validación de FK.
- Periodos cerrados: nunca recalcular ni sobrescribir sin autorización de negocio y evidencia de reliquidación.

## R-QA-CAT-001 — cierre 2026-10-28

**RESUELTO.** El alta genérica de puesto, planta, área, sección y tipo de documento devolvía
la clave antes de que SQLite la generara. `GuardarAsync` conserva el único `SaveChangesAsync`
y ahora obtiene la clave desde la entidad seguida después del guardado. Empresa, centro y
centro de costo no son altas admitidas por este servicio y se clasificaron `NO APLICA`.

Criterio de salida cumplido: `QACAT-001..006` con 9/9 ejecuciones SQLite, regresión dirigida
112/112, suite completa 1,037/1,037 y build Release 0 errores/0 warnings. Sin migración,
producción, OP-03, commit ni reejecución del piloto Hotel Equo. Detalle: `R_QA_CAT001.md`.

# CAT-DYN-001 (2026-10-28)

**COMPLETADO.** La carga masiva crea o reutiliza controladamente unidades técnicas `Planta`,
equipos, marcadores, asignaciones y puestos tras previsualización confirmada. Se cerraron los
riesgos de catálogos huérfanos mediante savepoint por fila, previsualización obsoleta mediante
huella conjunta y acceso implícito mediante validación del alcance sin altas `UsuarioPlanta`.
No requiere migración ni remediación de datos existentes.

Evidencia: 30/30 contratos CAT-DYN, 80/80 regresión dirigida, 1,067/1,067 suite completa y
build Release 0 errores/0 advertencias. Producción y OP-03 permanecen intactos.

# CAT-DYN-003 (2026-10-29)

**COMPLETADO EN CÓDIGO Y PRUEBAS.** La brecha entre trabajador importado y trabajador listo
para tareo queda observable mediante un diagnóstico histórico de solo lectura. Se distinguen
LISTO, INCOMPLETO y CONFLICTO con motivos y acciones; la seguridad por unidad se aplica al
resultado y a los filtros visibles. La integración posterior a carga y previa a recálculo es
informativa y no bloqueante.

Evidencia: CATREADY-01..20, 20/20 dirigidas; suite 1,087/1,087; Build Release 0/0. No se
crearon migraciones ni se tocaron producción, OP-03, despliegue o commit.
# P1-DESKTOP-INSTALLER (2026-10-12)

**COMPLETADO.** Implementado el empaquetado local temporal y la política automática
diaria/20 MB/30 días de logs. El ciclo elevado real validó instalación, ejecución,
reinstalación, desinstalación y reinstalación posterior con conservación de SQLite.
Build 0/0; pruebas 855/855. Producción, OP-03 y migraciones productivas permanecen fuera
de alcance.

# P1-LOCAL-FIRST-RUN (2026-10-13)

**RESUELTO EN CÓDIGO Y ENSAYO AISLADO; CICLO REAL PENDIENTE DE INTERACCIÓN.** La causa era una
base compatible sin usuarios tratada como excepción fatal cuando faltaban las variables de
bootstrap. Se incorporó un wizard Web de un solo uso, estados explícitos de readiness, apertura
de `/setup` y diálogo nativo para fallos pre-HTTP. El instalador 1.0.1 ya no solicita ni propaga
credenciales. Build 0/0, pruebas 862/862 y smoke del publish válidos. Falta completar en la
instalación real el setup con una clave elegida por el usuario y repetir el ciclo elevado de
preservación; producción y migraciones productivas permanecen fuera de alcance.

# P1-DATA-PERIODOLABORAL — Fase 3C (2026-10-13)

**COMPLETADA EN CÓDIGO Y COPIA CONTROLADA.** Vigencia, empresa/planta históricas, reportes,
tareo y alcance por planta de jornadas migraron a la historia canónica. Build 0/0 y 900/900;
SQLite `ok`, FK 0 y health `LISTO`. No quedan fallbacks legacy en rutas históricas críticas.

Próximo paso permitido: Fase 3D. Antes de cualquier operación productiva siguen vigentes las
restricciones de backup, ventana, credencial y OP-03; esta fase no autoriza producción.

La indicación “Próximo paso: Fase 3D” queda **SUPERADA**.

# P1-DATA-PERIODOLABORAL — cierre 3E-E (2026-10-15)

**CERRADO TÉCNICA Y DOCUMENTALMENTE.** 3B saneó/backfilleó una copia controlada; 3C migró
consumidores históricos; 3D consolidó invariantes y escrituras; 3E-A inventarió la deuda legacy;
3E-B corrigió cuadrillas/PMV; 3E-C implementó reingreso; 3E-D validó 937/937 y 108/108; 3E-E
reconcilió el contexto maestro.

Deuda residual no bloqueante del cierre: proyecciones planas y dual-write de compatibilidad;
separación futura de edición/importación frente a eventos laborales; ausencia de constraint
general de solapamiento cerrado, CHECK completo y token propio de concurrencia; P1-06 para otros
maestros históricos. No eliminar columnas ni ejecutar saneamiento/migración productivos sin una
fase independiente autorizada.

Producción no tocada. OP-03 no ejecutado. Commit no creado.

# P0-SEG-006-FINAL — cierre operacional (2026-10-19)

**CERRADO.** La instalación local autorizada recibió un coordinador sustituto creado por el
flujo Web normal. Se validaron login, relogin, gestión de usuarios y acceso operativo antes de
desactivar la cuenta comprometida. Esta quedó inactiva, con bloqueo indefinido y
`SecurityStamp` rotado; la única prueba posterior de la credencial histórica fue rechazada. El
nuevo coordinador conserva acceso administrativo.

Respaldo previo verificado y preservado; SQLite `ok`, FK 0; escaneo posterior de configuración,
ZIP y logs en cero. La exposición histórica queda contenida en Git/cuarentena y esos artefactos
no pueden volver a desplegarse. No hubo cambio de código, migración productiva, OP-03, producción
ni commit.


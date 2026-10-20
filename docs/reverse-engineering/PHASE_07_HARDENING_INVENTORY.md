# Fase 7 — Inventario de hardening, datos y pruebas integrales

Fecha de corte: 2026-10-09  
Estado: **BLOQUE 1 CERRADO — INVENTARIO Y PRIORIZACIÓN**  
Cambios de código productivo en esta fase: **ninguno**  
OP-03 ejecutado: **no**  
Migración productiva: **no**

## 1. Alcance y guardas

Esta iteración consolida evidencia antes de corregir. Se revisaron las fuentes maestras,
las fichas modulares y los documentos BD/OP exigidos para la Fase 7; también se ejecutaron
pruebas, cobertura, auditoría de dependencias y consultas de solo lectura sobre la base SQLite
incluida en el árbol fuente.

No se hizo ninguna de estas acciones:

- modificar código productivo;
- aplicar migraciones;
- abrir o escribir la base candidata de producción;
- ejecutar OP-03;
- crear un backup productivo definitivo;
- rotar credenciales o borrar archivos versionados;
- reparar datos de `PeriodoLaboral`.

El texto recibido para la Fase 7 terminaba en `No reabr...`. Por ello, este inventario respeta
los estados ya cerrados y no presupone instrucciones posteriores que no fueron recibidas.

## 2. Baseline verificado

| Control | Resultado |
|---|---|
| Build Release | 0 errores, 0 warnings |
| Domain.Tests | 368/368 |
| Infrastructure.Tests | 435/435 |
| Web.Tests | 4/4 |
| Total | **807/807, 0 fallos, 0 omitidos** |
| Integridad SQLite local del árbol fuente | `ok` |
| Violaciones FK en esa copia local | 0 |
| Vulnerabilidades NuGet conocidas por el feed consultado | 0 reportadas |

El verde de la suite confirma la regresión automatizada existente, no una cobertura integral
de todos los bordes HTTP, operativos, legales o de concurrencia.

## 3. Cobertura observada

Los reportes Cobertura quedaron en
`artifacts/phase7-hardening-20261009/coverage/`.

| Suite | Ensamblado principal | Líneas | Ramas |
|---|---|---:|---:|
| Domain.Tests | Domain | 70.24 % | 79.12 % |
| Infrastructure.Tests | Infrastructure | 96.95 % | 57.30 % |
| Web.Tests | Web | 10.80 % | 5.98 % |

La capa Web tiene 21 controladores. Solo `CuadrillasController` presenta cobertura de líneas
completa y `CuentaController` cobertura parcial; los otros 19 aparecen con 0 % de líneas en
el reporte. Los cuatro tests Web verifican una parte valiosa del flujo ROT-DESC/login, pero no
constituyen todavía una matriz HTTP de autorización, antiforgery, validación, concurrencia y
errores por controlador.

## 4. Criterio de prioridad

- **P0**: exposición de secretos o datos, o condición que exige contención antes de distribuir
  artefactos o compartir el repositorio.
- **P1**: integridad, exactitud legal/contable, disponibilidad o historia que puede producir un
  resultado materialmente incorrecto.
- **P2**: defecto acotado, carrera o gap funcional con mitigación operativa disponible.
- **P3**: mantenibilidad, observabilidad o simplificación que reduce riesgo futuro.

`Confirmado` significa que hay evidencia directa en código, Git, base local o ejecución.
`Gap` significa que la capacidad no existe y necesita contrato/decisión antes de implementarse.

## 5. Backlog priorizado

| ID | P | Tipo | Estado | Hallazgo y evidencia | Contrato de salida siguiente |
|---|---:|---|---|---|---|
| F7-SEC-01 | P0 | Seguridad/release | Confirmado | La configuración versionada contiene valores no vacíos de bootstrap administrativo. La misma configuración llega a salidas normales de build; OP-03A tuvo que sanear el ZIP manualmente. No se reproduce aquí ningún valor. | Credencial vigente rotada por el dueño; secretos fuera de Git y de artefactos; configuración de muestra sin secreto; gate automatizado que rechace secretos antes de build/release. |
| F7-DATA-01 | P0 | Privacidad/repositorio | Confirmado | Git rastrea una base SQLite y un backup; el árbol también conserva sidecars/backups no ignorados. La base local contiene información laboral y un registro de autenticación. `.gitignore` no cubre SQLite ni backups. | Inventario y retención aprobados; copia segura si corresponde; eliminación del árbol e historia según política; patrones de exclusión; fixture mínimo anonimizado; escaneo posterior sin datos sensibles. |
| F7-BOOT-01 | P1 | Disponibilidad/integridad | **Resuelto — P1-STARTUP-HEALTH** | `/vivo` identifica proceso; `/estado` verifica conexión, migraciones, integridad, FK y estructura física. Un fallo de preparación deja 503, bloquea rutas funcionales y no se propaga como salud falsa. | SH-01..07 verdes; recuperación 503→200 y rehabilitación del tráfico demostradas sin reinicio. |
| F7-DATA-02 | P1 | Datos/concurrencia | Confirmado en BD local, no en producción | La base del árbol tiene 307 trabajadores con más de un periodo abierto, 868 filas abiertas en esos grupos y 900 pares solapados. El modelo carece de unicidad física para el periodo abierto. Las cifras históricas de 85/170 quedaron obsoletas. | Saneamiento independiente, reversible y auditado; conteos reconciliados; índice/defensa física compatible con SQLite; test de escritura concurrente. |
| F7-HIST-01 | P1 | Exactitud histórica | Gap conocido | RQ-013/P1-06 sigue abierto: una reemisión puede resolver maestros actuales en vez de reproducir lo aprobado. | Una reemisión cerrada reproduce byte a byte o semánticamente el conjunto aprobado; definir snapshot o resolución temporal y backfill solo con evidencia. |
| F7-FER-01 | P1 | Legal/negocio | Gap conocido | Descanso sustitutorio, día no laborable compensable y valorización especial del 1 de mayo no tienen todos los datos/contratos ejecutables necesarios. ROT-FER conserva minutos físicos, pero no cierra toda la liquidación económica. | Decisiones de negocio firmadas, representación explícita y tests FER-NAC/ROT-FER que demuestren no duplicación física y valorización correcta. |
| F7-TEST-01 | P1 | Pruebas/seguridad | Confirmado | 19 de 21 controladores tienen 0 % de cobertura de líneas y la suite Web solo tiene 4 tests. La cobertura de infraestructura no sustituye contratos HTTP. | Matriz por rol/planta/verbo: 401/403, antiforgery, model validation, PRG, periodo cerrado y error controlado; priorizar endpoints que escriben o exportan. |
| F7-DOC-01 | P1 | Documentación/decisión IA | Confirmado | Fuentes maestras discrepan sobre baseline (718/695/23, 803/803 y 807/807), P0 ya cerrados y estado de frescura/seguridad/ROT-DESC. `REQUIREMENTS_TRACEABILITY` contradice RQ-004 entre matriz y prioridad. | Un único estado vigente 807/807; filas históricas rotuladas; requisitos y deuda reconciliados; validación automática de referencias/estados críticos. |
| F7-CONC-01 | P2 | Concurrencia PMV | Confirmado por diseño | `HashContenido` tiene índice no único y la comprobación ocurre antes de la transacción. Dos cargas iguales pueden superar el precheck; archivos sin marcas válidas pueden duplicar cabeceras. | Un hash aceptado por alcance lógico como máximo; constraint o protocolo atómico; test SQLite con dos importaciones concurrentes. |
| F7-SOL-01 | P2 | Flujo funcional | Confirmado | La aprobación puede omitir días con conflicto/cierre y aun dejar la solicitud como `Aprobada`, sin semántica explícita de aprobación parcial. | Negocio elige todo-o-nada o `AprobadaParcial`; estado, auditoría, mensaje y tests de conflicto quedan alineados. |
| F7-PMV-01 | P2 | Recursos/UX | Confirmado | Consulta PMV avisa y bloquea exportación por tamaño, pero no limita la consulta y materializa todo el rango en memoria. No propaga cancelación. | Límite/paginación y cancelación de extremo a extremo; respuesta estable ante rango excesivo; prueba de volumen. |
| F7-PMV-02 | P2 | Validación | Confirmado por código | El límite inclusivo usa `fechaFin.AddDays(1)` sin rechazar `DateTime.MaxValue`; una entrada autorizada extrema puede terminar en 500. | Fronteras de fecha validadas antes de aritmética; 400/mensaje de dominio, nunca excepción; tests min/max. |
| F7-PMV-03 | P2 | Exactitud funcional | Gap conocido | PMV muestra etiqueta de cuadrilla y dimensiones actuales, no necesariamente el turno diario y la organización histórica efectiva. | Resolver turno por fecha y dimensiones temporales; contrato con traslado/cambio de rotación y consulta retrospectiva. |
| F7-EVID-01 | P2 | Evidencia/datos | Gap conocido | Justificaciones aún admiten borrado físico. La auditoría ayuda a frescura, pero la evidencia funcional no queda como estado versionado/anulado. | Anulación lógica con actor, motivo y fecha; cálculo excluye anuladas; historial y auditoría conservan la evidencia. |
| F7-TURN-01 | P2 | Historia | Confirmado por diseño | Cuando falta `VersionTurno`, la resolución histórica cae al turno actual. No existe un contrato directo para impedir una reinterpretación silenciosa. | Política explícita de fallback o fallo; métrica/alerta; test con versión ausente y turno modificado. |
| F7-DATA-03 | P2 | Privacidad de tests | Revisión requerida | El recurso de prueba `AEH2200760069_attlog.txt` se describe como archivo real y contiene 2,509 filas/50 identificadores. No se demostró su anonimización. | Dueño confirma origen/licencia; fixture anonimizado y mínimo; escaneo sin identificadores personales reales. |
| F7-DEP-01 | P2 | Dependencias | Confirmado | No se reportaron vulnerabilidades, pero existen parches disponibles para paquetes Microsoft 10.0.10, SDK de test y coverlet. Un salto mayor de SQLite no debe mezclarse automáticamente. | Actualización por lote pequeño, release notes revisadas y 807+ pruebas verdes; auditoría de vulnerabilidades repetida. |
| F7-UX-01 | P2 | UX/operación | Gap conocido | Editar un turno con el mismo horario puede no informar cambios; fechas retroactivas pueden reescribir estado con riesgo operativo. | Confirmación/explicación visible y tests de no-op, fecha igual, anterior y nueva versión. |
| F7-CONFIG-01 | P2 | Configuración | Confirmado por código | La ruta de SQLite se normaliza en el runbook, pero el arranque admite una ruta relativa y depende del directorio del proceso. | Resolver y registrar ruta absoluta; rechazar configuración ambigua; test con cwd distinto. |
| F7-MAINT-01 | P3 | Mantenibilidad | Confirmado | Servicios críticos concentran entre ~1,100 y ~1,700 líneas; cálculo, reportes e importación tienen alta superficie de cambio. | Extraer por regla/caso de uso conservando contratos; límites de tamaño/complexidad graduales; pruebas antes de cada extracción. |
| F7-TIME-01 | P3 | Testabilidad/tiempo | Confirmado | `DateTime.Now` está disperso en servicios y persistencia; los bordes temporales dependen del reloj del proceso. | Reloj inyectable con zona Lima explícita donde aplique; pruebas de medianoche, cierre y cambio de fecha. |
| F7-OBS-01 | P3 | Observabilidad | Gap conocido | Hay logging básico, pero no métricas/correlación suficientes para importación, recálculo, migración y fallos de readiness. | IDs de correlación, métricas de duración/conteos/fallos y runbook de alertas sin registrar PII. |
| F7-CONST-01 | P3 | Dominio/configuración | Gap conocido | Constantes y umbrales están dispersos; algunos campos obsoletos siguen expuestos aunque ya no intervengan en planilla. | Catálogo de constantes con dueño; eliminar o rotular configuración obsoleta mediante cambio compatible y tests. |

## 6. Riesgos confirmados que no se reabren como bugs

- ROT-DESC permanece resuelto: sus contratos, migración ensayada y UI pasan.
- OP-02B permanece validado.
- OP-03 y la migración productiva permanecen no ejecutados.
- P0 de aislamiento PMV, tardanza, salida anticipada y reglas HE ya corregidos no se reabren.
- Importación principal sigue atómica y sus contratos permanecen verdes.
- La advertencia EF por cambio de `foreign_keys` en reconstrucciones SQLite continúa como
  condición operativa; no invalida el ensayo, pero exige parada de escrituras, backup y
  readiness real cuando exista una futura ventana autorizada.

## 7. Controles positivos observados

- warnings tratados como errores y analizadores habilitados;
- autenticación con mensajes genéricos y lockout;
- cookie HttpOnly/SameSite Strict y cabeceras de seguridad;
- autorización global y restricciones de rol/planta;
- protección antiforgery observada en los POST revisados;
- alcance de planta PMV fail-closed en los casos cubiertos;
- transacciones en importación principal y PMV;
- integridad/FK de la base local consultada correctas;
- ninguna vulnerabilidad NuGet conocida reportada en el corte.

Estos controles reducen riesgo, pero no compensan F7-SEC-01, F7-DATA-01 ni el hueco de
pruebas HTTP.

## 8. Orden de remediación propuesto

### Bloque 7A — Contención de seguridad y datos

Atender F7-SEC-01 y F7-DATA-01. Requiere coordinación humana para rotación, retención e
historia Git. No borrar ni reescribir historia sin autorización y backup verificable.

### Bloque 7B — Arranque y readiness — RESUELTO

F7-BOOT-01 quedó resuelto por `P1_STARTUP_HEALTH.md`. F7-CONFIG-01 permanece separado: la
normalización de rutas relativas no se modificó en esta intervención.

### Bloque 7C — Integridad de `PeriodoLaboral`

Perfilar la base objetivo autorizada, definir sobrevivientes, ensayar saneamiento y luego
agregar la defensa física. Nunca mezclar esta intervención con ROT-DESC.

### Bloque 7D — Contratos HTTP integrales

Ampliar Web.Tests empezando por escritura, exportación, roles y plantas. La meta no es un
porcentaje aislado, sino contratos de seguridad y error por endpoint crítico.

### Bloque 7E — Historia y reglas económicas

Resolver F7-HIST-01 y obtener decisiones para F7-FER-01/F7-SOL-01 antes de implementar.

### Bloque 7F — Concurrencia y recursos

Cerrar hash PMV, límites/cancelación, fronteras de fecha y resolución histórica de PMV/turnos.

### Bloque 7G — Mantenibilidad

Actualizar dependencias por lotes, introducir reloj/observabilidad y extraer servicios grandes
sin mezclar cambios de reglas de negocio.

## 9. Contrato de salida del inventario

El bloque de inventario se considera cerrado porque:

1. el baseline se reprodujo;
2. los hallazgos tienen ID, prioridad, evidencia y condición de salida;
3. código, datos y decisiones de negocio están separados;
4. no se modificó producción ni se ejecutó OP-03;
5. el siguiente trabajo puede comenzar por un hallazgo, con tests primero, sin reauditar todo.

La Fase 7 completa **no** está cerrada. Su siguiente bloque recomendado es **7A — contención
de seguridad y datos**.

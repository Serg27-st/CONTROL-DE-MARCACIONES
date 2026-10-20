# P1-DATA-PERIODOLABORAL — decisiones de negocio y contrato objetivo

Fecha de corte: 2026-10-12  
Estado: **DECISION GATE APROBADO**  
Fecha de aprobación: **2026-10-12**  
Resultado: **5/5 decisiones aprobadas; 0 pendientes**  
Naturaleza: contrato aprobado; su implementación continúa por fases controladas

Baseline de entrada: Build Release 0 errores/0 warnings; Domain 368/368,
Infrastructure 436/436 y Web 9/9, total 813/813, 0 omitidos. Producción intacta, OP-03 no
ejecutado y cuatro eliminaciones staged de Fase 7A preservadas.

## 1. Las 5 decisiones originales

Las cinco decisiones se extraen literalmente del gate de
`P1_DATA_PERIODOLABORAL.md`. No se agrega una sexta decisión. Los detalles sobre fechas futuras,
correcciones o unicidad son subcláusulas necesarias de estas cinco.

### PL-PN01 — autoridad canónica y significado de los campos planos `[APROBADA]`

| Elemento | Detalle |
|---|---|
| Pregunta original | ¿`Trabajador.FechaIngreso/FechaCese/EmpresaId/PlantaId` serán snapshot vigente, cache derivada o seguirán siendo autoridad? |
| Por qué importa | Hoy gobiernan tareo, reportes, filtros y UI; `PeriodoLaboral` conserva otra representación que puede divergir. |
| Componentes | Domain, Personal, DbContext, EF, tareo, reportes, UI, importaciones, readiness. |
| Sin decisión | No se puede definir qué dato gana, corregir divergencias ni migrar consumidores sin cambiar resultados de forma accidental. |
| Opciones | (A) planos canónicos; (B) periodos canónicos y planos derivados; (C) doble autoridad sincronizada; (D) vínculo canónico completo + proyección vigente explícita. |
| Riesgos | A no expresa historia múltiple; B aplicada directamente consume datos inconsistentes; C perpetúa carreras; D requiere transición y migración por etapas. |

### PL-PN02 — reingreso, cese, corrección y cambio de empresa `[APROBADA]`

| Elemento | Detalle |
|---|---|
| Pregunta original | ¿Cómo se registra un reingreso y cuándo puede corregirse un vínculo cerrado? |
| Por qué importa | El sistema no tiene operación de reingreso; editar un cesado crea hoy un periodo cerrado adicional. |
| Componentes | `Trabajador`, `PeriodoLaboral`, alta/baja, importación, asignaciones, tareo, auditoría. |
| Sin decisión | Un segundo ingreso no puede representarse de forma válida; cambiar empresa o corregir un cese reinterpreta historia. |
| Opciones | Sobrescribir el vínculo anterior; crear nuevo trabajador; crear nuevo periodo en el mismo trabajador; permitir o prohibir correcciones de periodos cerrados. |
| Riesgos | Sobrescribir destruye historia; nuevo trabajador duplica identidad/documento; nuevo periodo exige invariantes; corrección libre altera planillas cerradas. |

### PL-PN03 — traslado de planta y fecha efectiva `[APROBADA]`

| Elemento | Detalle |
|---|---|
| Pregunta original | ¿Un traslado de planta abre un nuevo vínculo laboral o una vigencia organizativa separada? |
| Por qué importa | `PeriodoLaboral` no tiene planta y `Trabajador.PlantaId` reescribe reportes y recálculos históricos. |
| Componentes | Organización, seguridad por planta, turnos, feriados, tareo, reportes, PMV e importaciones. |
| Sin decisión | No se puede reconstruir qué planta aplicaba en una fecha ni qué feriados/turnos correspondían. |
| Opciones | (A) `PlantaId` en `PeriodoLaboral`; (B) asignación temporal separada; (C) planta solo plana; (D) snapshot por reporte. |
| Riesgos | A confunde traslado con vínculo legal; B agrega una línea temporal; C mantiene el defecto; D reproduce reportes pero no resuelve el tareo. |

### PL-PN04 — saneamiento de duplicados, solapes y huecos `[APROBADA]`

| Elemento | Detalle |
|---|---|
| Pregunta original | ¿Cómo se elige y documenta el sobreviviente de duplicados/solapes sin inventar historia? |
| Por qué importa | La última copia local auditada tenía 307 trabajadores con múltiples abiertos, 868 filas abiertas y 900 pares solapados. |
| Componentes | Datos, auditoría, migraciones, constraints, readiness y rollback. |
| Sin decisión | No se pueden agregar invariantes físicos ni usar la tabla como fuente canónica. |
| Opciones | Dedupe determinista de copias exactas; cierre por reglas derivadas; revisión humana; conservar anomalía bloqueando al trabajador. |
| Riesgos | Una automatización agresiva puede inventar ceses/empresas; revisión total puede ser costosa; no sanear bloquea la migración. |

### PL-PN05 — reproducibilidad de reportes cerrados `[APROBADA]`

| Elemento | Detalle |
|---|---|
| Pregunta original | ¿Los reportes cerrados se reproducen por snapshot o por resolución temporal versionada? |
| Por qué importa | Una reemisión usa hoy maestros actuales y puede no reproducir lo aprobado/pagado. |
| Componentes | Reportes, exportaciones, cierre de periodo, auditoría, retención y operación. |
| Sin decisión | Corregir historia o maestros puede modificar silenciosamente un reporte cerrado. |
| Opciones | Snapshot al cierre; resolución temporal completa; archivo final inmutable; estrategia híbrida. |
| Riesgos | Snapshot ocupa y duplica datos; resolución exige versionar todos los insumos; archivo solo no facilita consulta; híbrido requiere gobernanza de versiones. |

## 2. Alternativas

### 2.1 Autoridad de datos — PL-PN01

| Alternativa | Historia | Tareo/reportes | Auditoría | Riesgo de migración |
|---|---|---|---|---|
| Planos canónicos | No representa múltiples vínculos ni huecos | Cero cambio inmediato | Simple pero incompleta | Bajo ahora; deuda permanente alta |
| `PeriodoLaboral` canónico actual | Empresa/fechas sí; planta no | Requiere cambiar ambos conjuntamente | Mejor trazabilidad | **Inaceptable hoy** por duplicados y falta de planta |
| Doble autoridad | Ambigua | Exige sincronización perfecta | Difícil saber qué corrigió qué | Medio, con riesgo acumulativo alto |
| Vínculo completo + proyección | Completa si se añade planta temporal | Un resolvedor por fecha; planos solo transición | Autoridad y proyección distinguibles | Medio-alto, reducible por etapas |

### 2.2 Reingreso y cambio de empresa — PL-PN02

| Alternativa | Compatibilidad histórica | Impacto |
|---|---|---|
| Sobrescribir periodo anterior | Nula | Cambia tareos y reportes antiguos |
| Nuevo `Trabajador` | Conserva fechas, duplica identidad | Rompe documento, historial personal y asignaciones |
| Mismo `Trabajador`, nuevo periodo | Alta | Separa identidad personal de vínculos legales |

Para correcciones de un vínculo cerrado hay tres políticas: prohibición absoluta, edición libre o
flujo excepcional versionado. La edición libre no es aceptable para datos que sustentan planilla.

### 2.3 Planta histórica — PL-PN03

| Alternativa | Cambio de planta sin cese | Cambio de empresa | Tareo histórico | Evaluación |
|---|---|---|---|---|
| A. `PlantaId` en `PeriodoLaboral` | Obliga a partir el vínculo laboral | Natural dentro del nuevo periodo | Resuelve si se parte cada traslado | Acopla dos hechos distintos |
| B. `AsignacionPlanta` temporal | Cierra una asignación y abre otra | La asignación nueva debe pertenecer a la empresa del nuevo periodo | Resuelve por fecha | Mejor separación conceptual |
| C. Solo `Trabajador.PlantaId` | Sobrescribe | Sobrescribe | No resuelve | Mantiene el defecto |
| D. Solo snapshot de reporte | No modela operación | No modela operación | Reproduce salida, no recálculo | Complemento, no fuente laboral |

### 2.4 Saneamiento — PL-PN04

| Alternativa | Ventaja | Riesgo |
|---|---|---|
| Dedupe exacto automático condicionado | Reduce volumen sin inferir fechas | “Exacto” debe excluir solo metadatos y demostrar ausencia de dependencias |
| Regla de último/primer registro | Simple | El orden de inserción no prueba verdad laboral |
| Inferir por marcaciones/tareo | Aporta evidencia | Una marca no demuestra contrato, empresa ni cese exacto |
| Revisión humana dirigida | Preserva decisiones dudosas | Coste operativo |
| Bloqueo conservador | No inventa historia | Demora la migración de esos trabajadores |

### 2.5 Reportes cerrados — PL-PN05

| Alternativa | Reproducción | Correcciones posteriores | Complejidad |
|---|---|---|---|
| Snapshot estructurado al cierre | Determinista | Nueva versión o ajuste explícito | Media |
| Resolución temporal completa | Determinista solo si todos los maestros están versionados | Recalcula con la versión aprobada | Alta |
| Archivo final inmutable | Byte a byte | No facilita explotación ni explicación | Baja-media |
| Híbrida: snapshot + archivo + hechos temporales | Máxima | Versionado y trazabilidad explícitos | Alta, pero controlable |

## 3. Recomendaciones

Estas recomendaciones fueron sometidas al gate humano y las cinco quedaron aprobadas el
2026-10-12, sin alterar su sentido original.

### Recomendaciones de negocio sujetas a aprobación

| ID | Recomendación | Fundamento e impactos |
|---|---|---|
| PL-PN01 | Aprobar la opción D: vínculo temporal canónico completo y proyección vigente explícita | Preserva historia, permite reingresos y evita doble autoridad. Tareo/reportes migran al mismo resolvedor. Riesgo de migración medio-alto; auditoría clara. |
| PL-PN02 | Una persona conserva el mismo `Trabajador`; cada reingreso o cambio de empleador abre un `PeriodoLaboral` nuevo. Los cerrados solo cambian mediante corrección excepcional auditada | Mantiene identidad e historia. Altas/bajas pasan a operar sobre periodos. Empresa deja de cambiarse directamente. |
| PL-PN03 | Modelar planta en una asignación temporal separada, contenida dentro de un vínculo laboral y coherente con su empresa | Un traslado interno no equivale a cese. Tareo y reportes resuelven planta por fecha. Requiere migración y backfill con evidencia. |
| PL-PN04 | Automatizar únicamente duplicados exactos bajo precondiciones verificables; toda diferencia material, solape no idéntico o historia de planta incompleta pasa a revisión humana/bloqueo | No inventa fechas ni organización. El saneamiento debe ser reversible y previo a constraints. |
| PL-PN05 | Estrategia híbrida: hechos temporales canónicos para consultas abiertas + snapshot estructurado y artefacto versionado al cerrar | El histórico explica el hecho; el snapshot reproduce lo aprobado. Una corrección posterior genera versión/ajuste, no mutación silenciosa. |

### Decisiones técnicas condicionadas a las anteriores

Estas sí pueden tratarse como consecuencia técnica una vez que negocio apruebe el contrato:

- un único resolvedor de vínculo y planta por fecha;
- límites inclusivos de ingreso y cese, conservando el comportamiento vigente;
- constraints físicos después del saneamiento;
- proyecciones legacy de solo escritura interna, nunca editables directamente;
- divergencia canónico/proyección como error observable, no como elección silenciosa;
- consultas por lote para evitar N+1;
- migración de tareo y reportes en una misma etapa controlada o bajo comparador dual.

### Impactos resumidos

- **Tareo:** deja de usar el rango plano y resuelve vínculo/planta por día.
- **Reportes:** población, empresa y planta se resuelven por fecha; los cerrados usan snapshot.
- **Altas/bajas/reingresos:** operan sobre periodos; la ficha personal no se duplica.
- **Empresa:** pertenece al vínculo legal.
- **Planta:** pertenece a una asignación temporal dentro del vínculo.
- **Migración:** exige perfilado, saneamiento y backfill; no admite big bang.
- **Auditoría:** cada apertura, cierre, traslado, corrección y reconciliación conserva actor, fecha,
  motivo y valores anteriores/nuevos.

## 4. Decisiones aprobadas por el usuario

El usuario aprobó explícitamente y sin cambios estas cinco declaraciones:

1. `[APROBADA]` **PL-PN01:** `PeriodoLaboral` será la fuente canónica de fechas y empresa; la planta tendrá
   fuente temporal; los campos planos serán proyección de compatibilidad.
2. `[APROBADA]` **PL-PN02:** reingreso y cambio de empleador crean un periodo nuevo en el mismo trabajador;
   los periodos cerrados solo se corrigen mediante flujo excepcional auditado.
3. `[APROBADA]` **PL-PN03:** un cambio de planta no cierra por sí mismo el vínculo laboral; crea una nueva
   asignación temporal de planta con fecha efectiva.
4. `[APROBADA]` **PL-PN04:** solo duplicados exactos y demostrablemente equivalentes pueden entrar a una regla
   automática; el resto requiere evidencia o revisión humana.
5. `[APROBADA]` **PL-PN05:** al cierre se conserva snapshot estructurado + artefacto/versionado; la consulta
   temporal sigue explicando los hechos, pero no reescribe lo aprobado.

Decisiones originales cerradas automáticamente: **0**.  
Decisiones aprobadas por el usuario: **5**.  
Decisiones pendientes: **0**.

No se introducen decisiones adicionales independientes. La aceptación de periodos futuros, la
inmutabilidad de cerrados y la cardinalidad de planta se presentan como subcláusulas de PL-PN02 y
PL-PN03 y deben aprobarse junto con ellas.

## 5. Contrato objetivo

Contrato conceptual propuesto, todavía no implementado:

| Pregunta | Respuesta objetivo propuesta |
|---|---|
| 1. Fuente canónica de ingreso | `PeriodoLaboral.FechaIngreso` del vínculo que corresponda |
| 2. Fuente canónica de cese | `PeriodoLaboral.FechaCese`; nulo significa vínculo sin cierre conocido |
| 3. Fuente canónica de vigencia | Existencia de exactamente un periodo canónico válido que contenga la fecha |
| 4. Fuente canónica de empresa | `PeriodoLaboral.EmpresaId` |
| 5. Fuente canónica de planta | Asignación temporal de planta vigente en la fecha |
| 6. Reingreso | Mismo `Trabajador`, nuevo `PeriodoLaboral`, nueva asignación de planta |
| 7. Cambio de empresa | Cierre del periodo anterior y apertura de otro; nunca update directo de la empresa histórica |
| 8. Cambio de planta | Cierre de asignación de planta y apertura de otra; no exige cese si la empresa no cambia |
| 9. Huecos | Permitidos; durante el hueco no existe vínculo laboral |
| 10. Periodos futuros | Permitidos si no solapan; no producen vigencia antes de su ingreso. Su habilitación operativa espera a que consumidores no dependan de `FechaCese == null` |
| 11. Solapamientos | Prohibidos para el mismo trabajador |
| 12. Más de un periodo abierto | Prohibido, incluso si uno empieza en el futuro; para programar otro debe existir cierre del anterior |
| 13. `Trabajador.Activo` | Disponibilidad lógica/administrativa de la ficha, no vigencia laboral ni borrado de historia |
| 14. Campos planos | Permanecen durante la transición; su retiro físico se decide después de migrar consumidores |
| 15. Rol de campos planos | Proyección/cache derivada de compatibilidad, escrita solo por el proyector |
| 16. Divergencia | Se registra, bloquea escrituras sensibles y degrada readiness del módulo; no se autocorrige dentro de una petición |
| 17. Fuente que gana | Gana la fuente temporal canónica. Para reproducir un reporte ya cerrado gana la versión/snapshot aprobado, sin alterar los hechos canónicos |

### 5.1 Modelo conceptual

```text
Trabajador
  identidad personal y estado administrativo
  │
  ├── 0..N PeriodoLaboral
  │      EmpresaId
  │      FechaIngreso
  │      FechaCese?
  │      Motivo/observación de cese
  │
  └── 0..N AsignacionPlanta
         PlantaId
         FechaInicio
         FechaFin?
         contenida en un PeriodoLaboral

ProyeccionLaboralActual/legacy
  deriva del periodo y planta efectivos; no es autoridad
```

La asignación de planta puede referenciar el `PeriodoLaboral` que la contiene. Eso permite validar
que la planta pertenece a la empresa del vínculo y evita usar una asignación a través de un hueco.

## 6. Invariantes

El estado mostrado es la **recomendación para aprobación**, no una aprobación ejecutada.

| ID | Invariante | Estado propuesto | Explicación |
|---|---|---|---|
| INV-01 | Un trabajador no puede tener más de un periodo abierto | `[ACEPTAR]` | Evita dos futuros indefinidos y hace determinista la proyección |
| INV-02 | Dos periodos del mismo trabajador no pueden solaparse | `[ACEPTAR]` | Una fecha debe resolver cero o un vínculo, nunca dos |
| INV-03 | `FechaCese >= FechaIngreso` | `[ACEPTAR]` | Los límites son inclusivos; permite vínculo de un día |
| INV-04 | La proyección vigente corresponde al vínculo/planta efectivos | `[ACEPTAR]` | Es condición de compatibilidad, no autoridad paralela |
| INV-05 | Empresa actual proviene del periodo efectivo | `[ACEPTAR]` | Impide update independiente del snapshot |
| INV-06 | Planta actual proviene de la asignación temporal efectiva | `[ACEPTAR]` | Conserva traslados e historia |
| INV-07 | Un trabajador puede tener múltiples periodos históricos | `[ACEPTAR]` | Necesario para reingresos y retorno a una empresa anterior |
| INV-08 | Un reingreso crea periodo nuevo | `[ACEPTAR]` | Nunca sobrescribe el vínculo cerrado |
| INV-09 | Toda asignación de planta está contenida en un vínculo | `[ACEPTAR]` | No hay planta laboral durante un hueco |
| INV-10 | La planta pertenece a la empresa del vínculo en la fecha | `[ACEPTAR]` | Evita combinaciones organizativas imposibles |
| INV-11 | Hay exactamente una planta laboral por día con vínculo | `[REQUIERE NEGOCIO]` dentro de PL-PN03 | Si existe trabajo simultáneo multisitio, se necesita otro modelo |
| INV-12 | Un periodo cerrado no se edita por mantenimiento ordinario | `[ACEPTAR]` | Corrección excepcional versionada y con motivo |
| INV-13 | Un periodo futuro no implica vigencia presente | `[ACEPTAR]` | La fecha de inicio gobierna |
| INV-14 | Una divergencia no se resuelve eligiendo `FirstOrDefault` | `[ACEPTAR]` | Debe fallar de forma observable y reparable |

INV-11 no crea una sexta decisión: es la cardinalidad que debe confirmarse dentro de la decisión
original de historización de planta.

## 7. Modelo de vigencia

### 7.1 Función conceptual

```text
EstaVigente(trabajador, fecha):
  candidatos = PeriodosLaborales canónicos y no anulados
               donde TrabajadorId = trabajador.Id
               y FechaIngreso <= fecha
               y (FechaCese es NULL o fecha <= FechaCese)

  si candidatos.Count = 0 → false
  si candidatos.Count = 1 → true
  si candidatos.Count > 1 → ERROR DE INTEGRIDAD, no elegir uno
```

Reglas:

- `FechaIngreso` es inclusiva.
- `FechaCese` es inclusiva, igual que el comportamiento actual.
- Un periodo futuro no cuenta antes de `FechaIngreso`.
- Un hueco devuelve `false`.
- Un solapamiento o múltiples candidatos no devuelven `true`: bloquean el cálculo afectado y
  producen diagnóstico explícito.
- Un único periodo abierto puede empezar en el futuro; no está vigente antes de su inicio.
- `Trabajador.Activo` no borra vigencia histórica. Para operaciones actuales puede existir otra
  función `EstaOperableYVigente = Trabajador.Activo && EstaVigente(hoy)`.
- Un periodo anulado/inválido se conserva para auditoría, pero se excluye del conjunto canónico.

### 7.2 Ejemplos

| Periodos | Fecha consultada | Resultado |
|---|---|---|
| 2024-01-10 a 2024-08-31 | 2024-01-09 | `false` |
| 2024-01-10 a 2024-08-31 | 2024-01-10 | `true` |
| 2024-01-10 a 2024-08-31 | 2024-08-31 | `true` |
| 2024-01-10 a 2024-08-31 | 2024-09-01 | `false` |
| anterior + reingreso 2025-02-01 abierto | 2024-12-15 | `false`, hueco |
| anterior + reingreso 2025-02-01 abierto | 2025-02-01 | `true` |
| periodo futuro 2027-01-01 abierto | 2026-12-31 | `false` |
| dos periodos que contienen 2025-02-10 | 2025-02-10 | error de integridad |

La planta se resuelve solo después de confirmar el vínculo:

```text
ResolverPlanta(trabajador, fecha)
  → exactamente una AsignacionPlanta contenida en el periodo resuelto
  → cero o más de una = error/dato incompleto según el gate aprobado
```

## 8. Reingresos

Ejemplo contractual:

```text
Primer ingreso: 2024-01-10
Cese:          2024-08-31
Reingreso:     2025-02-01
```

Representación:

```text
Trabajador #T
  Activo = true

PeriodoLaboral #P1
  TrabajadorId = T
  EmpresaId = empresa del vínculo 2024
  FechaIngreso = 2024-01-10
  FechaCese = 2024-08-31

PeriodoLaboral #P2
  TrabajadorId = T
  EmpresaId = empresa del vínculo 2025
  FechaIngreso = 2025-02-01
  FechaCese = NULL
```

- Es el **mismo `Trabajador`** porque representa la misma persona/documento.
- Se crea un **nuevo `PeriodoLaboral`**; P1 no se sobrescribe.
- La proyección legacy queda, tras el reingreso: `FechaIngreso = 2025-02-01`,
  `FechaCese = null`, `EmpresaId` y `PlantaId` efectivos en P2.
- `Trabajador.Activo` permanece `true`; el cese no lo apaga. Si fue desactivado
  administrativamente, el reingreso exige reactivación explícita y auditada.
- Empresa actual proviene de P2; planta actual, de la asignación de planta de P2.
- Reporte de 2024 resuelve P1 y la planta vigente en 2024. No muestra la empresa/planta de 2025.
- Reporte de 2025 anterior al 1 de febrero no lo considera vigente; desde el 1 de febrero resuelve
  P2.
- El hueco 2024-09-01 a 2025-01-31 no genera faltas, horas ni pertenencia laboral.

Un retorno posterior a la empresa de P1 crea P3 con el mismo `EmpresaId` de P1; no reabre ni
edita P1.

## 9. Empresa/planta histórica

### 9.1 Decisión recomendada

- **Empresa:** congelada en cada `PeriodoLaboral` porque identifica al empleador legal.
- **Planta:** congelada por tramos de `AsignacionPlanta` porque puede cambiar sin que termine el
  vínculo con la empresa.
- **Reporte cerrado:** congela además la versión aprobada de las dimensiones mostradas.

### 9.2 Escenarios

| Escenario | Representación objetivo | Reporte/recálculo histórico |
|---|---|---|
| Cambio de planta, misma empresa | Mismo periodo; cierra planta A en D-1 y abre planta B en D | Cada día usa su planta, feriados y turnos aplicables |
| Cambio de empresa | Cierra periodo y planta anterior; abre nuevo periodo y planta de la nueva empresa | Historia anterior conserva empleador/planta |
| Retorno a empresa anterior | Nuevo periodo con esa empresa | No modifica periodos previos |
| Cambio de planta y empresa el mismo día | Nuevos periodo y asignación coordinados | Corte único, sin solape |
| Reporte histórico abierto | Resuelve periodo + planta por fecha | Refleja hechos canónicos |
| Reporte histórico cerrado | Usa snapshot/version aprobado | Reproduce lo pagado; una corrección genera nueva versión/ajuste |
| Recálculo histórico autorizado | Resuelve hechos temporales, no campos planos | Si difiere de un cierre, exige política de reapertura/reintegro |

Información que debe quedar históricamente congelada o versionada:

- trabajador y vínculo legal;
- empresa;
- planta por fecha;
- ingreso/cese y causal;
- turnos, cuadrilla, área, marcador y sueldo ya temporales;
- calendarios/feriados y reglas aplicadas cuando la reproducibilidad legal lo requiera;
- snapshot/artefacto aprobado del reporte cerrado y su hash.

No debe reconstruirse planta histórica copiando ciegamente `Trabajador.PlantaId` actual. Cuando no
haya evidencia suficiente, el tramo queda como dato pendiente/indeterminado y bloquea el corte del
consumidor correspondiente.

## 10. Compatibilidad legacy

Los campos planos permanecen inicialmente porque hoy tienen muchos consumidores. Su contrato
cambia de autoridad a proyección:

| Campo legacy | Valor proyectado | Escritura permitida |
|---|---|---|
| `Trabajador.FechaIngreso` | Ingreso del periodo efectivo; si no hay efectivo, último periodo iniciado según política de transición | Solo proyector |
| `Trabajador.FechaCese` | Cese del periodo proyectado; nulo si ese periodo está abierto | Solo proyector |
| `Trabajador.EmpresaId` | Empresa del periodo proyectado | Solo proyector |
| `Trabajador.PlantaId` | Planta de la asignación proyectada | Solo proyector |

La selección de compatibilidad es determinista: (1) periodo efectivo hoy; si no existe, (2) el
último periodo iniciado y ya finalizado; si la persona aún nunca ingresó, (3) el primer periodo
futuro. La proyección explícita debe guardar además `PeriodoLaboralId`, estado
`SinVinculo/Vigente/Cesado/Programado` y, cuando corresponda, `AsignacionPlantaId`. Los cuatro
campos legacy por sí solos no distinguen un vínculo futuro de uno vigente; por eso no pueden seguir
siendo la fuente de verdad.

Durante la transición:

1. todo comando laboral escribe primero la fuente temporal y actualiza la proyección en la misma
   unidad transaccional;
2. un verificador compara canónico y legacy;
3. ninguna pantalla o importación puede escribir directamente los cuatro campos;
4. si divergen, el canónico gana conceptualmente, pero el sistema no corrige silenciosamente:
   registra el caso, bloquea operaciones sensibles y exige reconciliación auditada;
5. periodos futuros no se habilitan para uso normal mientras un consumidor interprete
   `FechaCese == null` como “vigente hoy” sin mirar `FechaIngreso`;
6. al final, los campos pueden mantenerse como cache, convertirse en vista/proyección o retirarse
   en una fase posterior. Su eliminación no forma parte de este gate.

## 11. Estrategia de saneamiento

No se modifican datos en esta fase. La clasificación siguiente define el tratamiento futuro.

| Anomalía | Clasificación | Tratamiento propuesto |
|---|---|---|
| Dos o más abiertos idénticos en todos los campos de negocio | `[AUTOCORREGIBLE]` **condicionado** | Solo tras demostrar equivalencia, ausencia de dependencias semánticas y regla de sobreviviente; conservar mapa y reversa |
| Duplicados cerrados exactos | `[AUTOCORREGIBLE]` condicionado | Misma regla; no borrar evidencia, anular/relacionar según diseño aprobado |
| Múltiples abiertos con cualquier diferencia | `[REQUIERE REVISIÓN HUMANA]` | No elegir por `Id`, fecha de creación ni `FirstOrDefault` |
| Solapamiento no idéntico | `[REQUIERE REVISIÓN HUMANA]` | Determinar contrato real con documentos/evidencia; bloquear trabajador hasta resolver |
| Periodo abierto sin cierre | `[REQUIERE REGLA]` | Puede ser válido si es el vínculo actual/futuro; contrastar estado real y planos |
| Hueco entre periodos | `[NO CORREGIR AUTOMÁTICAMENTE]` | Los huecos son válidos; no rellenarlos con continuidad inventada |
| Plano divergente de periodo | `[REQUIERE REGLA]` durante transición | Antes del corte no hay autoridad aprobada; después gana el canónico y se repara la proyección con bitácora |
| Empresa histórica inconsistente | `[REQUIERE REVISIÓN HUMANA]` | La empresa es hecho legal; no inferir solo desde la ficha actual |
| Ausencia de planta histórica | `[NO CORREGIR AUTOMÁTICAMENTE]` | Backfill solo con evidencia fechada: asignaciones de marcador/área, padrones u otra fuente aprobada; “desconocida” es preferible a falsa |
| Trabajador sin periodo | `[REQUIERE REVISIÓN HUMANA]` | Construir periodo solo con evidencia suficiente |
| Rango invertido | `[REQUIERE REVISIÓN HUMANA]` | No intercambiar fechas automáticamente |

### Controles del futuro saneamiento

- copia autorizada y hash antes/después;
- ejecución dry-run con conteos y detalle anonimizado;
- tabla/archivo de correspondencia `original → sobreviviente/acción`;
- motivo, actor, fecha y evidencia por decisión humana;
- transacción y reversa ensayada;
- reconciliación de trabajadores, periodos, abiertos, solapes y divergencias;
- constraints solo después de cero anomalías incompatibles;
- nunca mezclar saneamiento con ROT-DESC, OP-03 o un despliegue productivo ordinario.

## 12. Plan por etapas

| Etapa | Objetivo | Esquema | Código | Tests | Riesgo | Rollback conceptual |
|---|---|---|---|---|---|---|
| 1. Invariantes preventivos | Impedir nuevas duplicaciones/edición accidental sin cambiar consumidores | Ninguno inicialmente; constraints solo si datos ya permiten | Corregir edición de cesado, centralizar comandos y detectores | Edición cesado, baja, concurrencia lógica, divergencia | Bajo-medio | Revertir guardas; no hay transformación de datos |
| 2. Fuente temporal canónica en sombra | Incorporar modelo completo y sanear copia objetivo | Migración para planta temporal, referencias e índices; constraints tras saneamiento | Escritor temporal y resolvedor, consumidores aún planos | Migración, backfill, integridad, rendimiento | Alto de datos | Restaurar copia/rollback ensayado; consumidores siguen legacy |
| 3. Proyección vigente explícita | Derivar legacy desde canónico y comparar | Posibles FKs/IDs de proyección; no retirar campos | Proyector transaccional, verificador/readiness, dual-write controlado | Consistencia, fallos atómicos, futuro/hueco | Medio | Desactivar proyector nuevo y conservar canónico en sombra |
| 4. Migrar consumidores | Tareo, reportes, UI, filtros, exportaciones e importación usan resolvedor | Normalmente no adicional | Corte por consumidor con comparación dual | T01–T18, E2E, rendimiento, reportes diferenciales | Alto funcional | Feature flag/corte individual al lector legacy; sin deshacer datos |
| 5. Deprecar legacy | Eliminar escritura y luego redefinir/retirar campos | Migración posterior opcional | Limpiar DTOs, formularios y consultas | Búsqueda de consumidores, compatibilidad, despliegue | Medio | Mantener vista/proyección compatible durante una versión |

### Gates entre etapas

- 1→2: las cinco decisiones aprobadas y contratos de tests aceptados.
- 2→3: copia objetivo reconciliada, cero solapes incompatibles y rollback probado.
- 3→4: proyección y canónico coinciden en el 100 % del conjunto autorizado.
- 4→5: resultados duales reconciliados para tareo/reportes y ventana operativa aprobada.

## 13. Matriz de impacto

| Componente | Estado actual | Cambio futuro | Riesgo | Etapa |
|---|---|---|---|---:|
| `Trabajador` | Fechas/empresa/planta operativas planas | Identidad + proyección derivada | Compatibilidad | 3–5 |
| `PeriodoLaboral` | Historia parcial de fechas/empresa | Fuente canónica del vínculo legal | Datos existentes | 2 |
| Planta temporal | No existe | Nueva asignación por rango, ligada al vínculo | Backfill incompleto | 2 |
| DbContext | Expone ambos sin resolvedor común | Repositorios/resolvedor temporal + proyector | Consultas complejas | 2–3 |
| Configuraciones EF | Sin unicidad abierta/solapes/checks | FKs, índices, checks y defensa de concurrencia | Migración SQLite | 2 |
| Migraciones | Modelo original | Esquema y backfill por fases | Reconstrucción/rollback | 2, 5 |
| Servicios Personal | Escriben planos y sincronizan sombra | Comandos de abrir/cerrar/trasladar/corregir | Regresión de flujos | 1–3 |
| Cálculo/tareo | Rango y planta planos | Vínculo/planta por fecha | Cambio material de resultados | 4 |
| Reportes | Planos actuales | Hechos temporales; snapshot al cierre | Reemisión distinta | 4 |
| UI | Edita ingreso/empresa/planta planos | Acciones explícitas con fecha efectiva | UX/errores humanos | 4 |
| Filtros | Snapshot actual | Semántica “actual” o “en fecha” explícita | Resultados inesperados | 4 |
| Exportaciones | Reutilizan DTO plano | DTO temporal/snapshot versionado | Formato/compatibilidad | 4 |
| Importaciones | Create/Update genérico | Alta, traslado, cese y reingreso explícitos | Inferencias incorrectas | 4 |
| Pruebas | 813 verdes, cobertura multiperiodo insuficiente | T01–T20 + concurrencia/migración/E2E | Falsa seguridad por volumen | Todas |
| Operaciones/readiness | No valida divergencia laboral | Checks de integridad/proyección, métricas y runbook | Bloqueos operativos | 3–5 |

## 14. Tests futuros

Contratos mínimos para implementar después de la aprobación:

| ID | Contrato |
|---|---|
| T01 alta inicial | Crea un trabajador, un periodo abierto y una planta vigente; la proyección coincide |
| T02 cese | Cierra el único periodo y la planta en fecha inclusiva; al día siguiente no hay vigencia |
| T03 reingreso | Mismo trabajador, segundo periodo y nueva planta; primero permanece intacto |
| T04 dos periodos históricos | Consulta cada fecha contra el periodo correcto y el hueco contra ninguno |
| T05 solapamiento rechazado | Application da error entendible y DB impide persistencia alternativa/concurrente |
| T06 segundo abierto rechazado | No se puede abrir otro si el anterior no tiene cese |
| T07 hueco permitido | Dos periodos separados persisten; el hueco no genera vínculo ni falta |
| T08 periodo futuro | Persiste sin volver vigente al trabajador antes de `FechaIngreso` |
| T09 cambio empresa | Cierra vínculo anterior, abre otro y conserva ambas razones sociales |
| T10 cambio planta | Parte solo la asignación de planta, no el vínculo, con límite sin solape |
| T11 histórico empresa | Reporte de cada rango muestra la empresa de su periodo |
| T12 histórico planta | Reporte y feriado/turno usan la planta efectiva de cada día |
| T13 vigencia antes ingreso | `EstaVigente = false`; tareo no genera falta |
| T14 vigencia durante periodo | Primer y último día inclusivos devuelven `true` |
| T15 vigencia después cese | Día siguiente devuelve `false`; tareo no genera falta |
| T16 tareo histórico | Segundo vínculo calcula; hueco no; primer vínculo no usa planta actual |
| T17 reporte histórico | Reemisión cerrada reproduce snapshot/versión aprobada |
| T18 divergencia legacy/canónico | Readiness/guard detecta, canónico no se sobrescribe y operación sensible se bloquea |
| T19 saneamiento duplicados | Dry-run, acción idempotente, mapa de sobrevivientes, conteos y reversa coinciden |
| T20 datos incompatibles | Solape/empresa/planta sin evidencia quedan bloqueados para revisión, no corregidos |

Cobertura transversal adicional:

- SQLite real para índices, checks y concurrencia;
- test de migración desde copia anonimizada representativa;
- pruebas Web de antiforgery, rol y alcance por planta para cada nueva acción;
- comparación diferencial legacy/canónico antes del corte;
- volumen representativo para demostrar ausencia de N+1;
- rollback de cada migración y de saneamiento.

## 15. Riesgos residuales

- La aprobación conceptual no aporta evidencia de planta histórica donde nunca se guardó.
- Algunos trabajadores pueden quedar bloqueados hasta revisión humana.
- Un snapshot reproduce lo aprobado, pero también conserva un error aprobado; la corrección debe
  generar una versión o ajuste, no reemplazarlo.
- Una fecha de cese inclusiva debe conservarse uniformemente en asignaciones y consultas.
- Los periodos futuros chocan con consumidores legacy que equiparan “cese nulo” con vigencia;
  deben habilitarse después de migrarlos.
- Los constraints de no solapamiento pueden requerir estrategia adicional en SQLite; la guardia de
  servicio por sí sola no cubre concurrencia ni escrituras manuales.
- El alcance de seguridad histórico puede diferir del actual. La política de quién puede consultar
  un traslado pasado debe respetar seguridad sin ocultar el reporte autorizado.
- El saneamiento sobre una copia local no prueba el estado de producción.
- Migrar tareo y reportes en momentos distintos puede romper la coherencia que hoy, aunque limitada,
  es intencional.
- Los campos legacy pueden prolongarse indefinidamente si no existe un gate de retiro medible.

## 16. Gate para implementación

Antes de escribir código o crear una migración deben cumplirse todos estos puntos:

- [ ] aprobación explícita de PL-PN01..PL-PN05;
- [ ] confirmación de INV-11 dentro de PL-PN03;
- [ ] aprobación de la política de corrección de periodos cerrados;
- [ ] copia de datos objetivo autorizada, identificada por hash y abierta solo en lectura;
- [ ] perfilado actualizado con consultas y conteos reconciliables;
- [ ] reglas de saneamiento y responsables aprobados;
- [ ] T01–T20 aceptados como contratos;
- [ ] diseño de rollback de esquema y datos ensayable;
- [ ] ningún uso de producción, OP-03 o despliegue incluido implícitamente;
- [ ] las cuatro eliminaciones staged de Fase 7A permanecen intactas.

Hasta entonces:

```text
NO implementar fix
NO crear migración
NO sanear datos
NO cambiar consumidores
NO tocar producción
```

### Resumen del decision gate

```text
=== P1-DATA-PERIODOLABORAL — DECISION GATE ===

Código productivo modificado:
NO

Migraciones creadas:
NO

Datos modificados:
NO

Producción tocada:
NO

Decisiones cerradas automáticamente:
0

Decisiones aprobadas por el usuario:
5

Decisiones pendientes:
0

Fuente canónica propuesta de vigencia:
PeriodoLaboral canónico, resuelto por fecha con límites inclusivos

Modelo de reingreso:
Mismo Trabajador + nuevo PeriodoLaboral + nueva asignación temporal de planta

Empresa histórica:
PeriodoLaboral.EmpresaId

Planta histórica:
AsignacionPlanta temporal separada y contenida en el vínculo laboral

Campos planos Trabajador:
Proyección/cache derivada de compatibilidad; nunca autoridad paralela

Saneamiento requerido:
Sí; automático solo para duplicados exactos bajo precondiciones, resto con revisión humana

Estado:
[DECISION GATE APROBADO]
```


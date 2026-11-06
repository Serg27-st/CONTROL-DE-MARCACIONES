# BD-REP004 — definición de snapshot histórico para reemisión reproducible

Fecha de análisis: 2026-10-23

Estado: **BLOQUEADO — DECISIONES DE NEGOCIO PENDIENTES**

Alcance: análisis de datos, diseño conceptual y política histórica. No se modificaron código,
tests, migraciones ni datos.

## 1. Resultado ejecutivo

La estrategia general ya fue aprobada en `PL-PN05`: los hechos temporales explican la operación
y, al cerrar, un snapshot estructurado más un artefacto versionado reproduce lo aprobado. El
repositorio aún no tiene esas estructuras ni un concepto separado de “reporte aprobado”. Solo
existe el cierre/reapertura de `Periodo`.

El inventario confirma que congelar únicamente los maestros faltantes no basta. Una reemisión
exacta también debe leer resultados aprobados —minutos, conteos, montos, reintegros y anexos— sin
recalcularlos con código o reglas futuras. A la vez, esos resultados no deben duplicarse dentro del
snapshot de contexto personal. La frontera recomendada es:

```text
versión aprobada de un periodo
├─ contexto común por trabajador
├─ resultados agregados por trabajador
├─ excepciones por día
├─ anexos del tareo final
└─ artefactos por tipo de reporte + SHA-256
```

La recomendación no se considera aprobada. Faltan decisiones sobre qué acto constituye la
aprobación, qué versión se entrega por defecto, cómo tratar correcciones legales y organizativas,
qué hacer con periodos antiguos y desde qué corte será obligatorio el snapshot nativo.

## 2. Inventario completo de salidas

Se inventariaron **129 posiciones visibles/exportadas** antes de contar encabezados repetidos:

| Salida | Posiciones |
|---|---:|
| Resumen de tareo | 20 |
| Excepciones | 10 |
| Hoja Tareo | 57 |
| Hoja Descanso Medico | 12 |
| Bloque Vacaciones | 9 |
| Bloque Licencias | 10 |
| Hoja Trazabilidad | 11 |
| **Total** | **129** |

Las tablas siguientes enumeran todas las posiciones. Cuando varios campos tienen la misma fuente y
tratamiento se agrupan, pero sus nombres se conservan explícitos.

Leyenda de clasificación:

- `A`: `HECHO_CALCULADO` o hecho operativo persistido usado como resultado;
- `B`: `DATO_LABORAL_HISTORICO`;
- `C`: `DATO_IDENTIFICATORIO`;
- `D`: `DATO_ORGANIZATIVO`;
- `E`: `DATO_MAESTRO_ACTUAL`;
- `F`: `METADATO_DE_REPORTE`.

### 2.1 Resumen de tareo — 20

| Campo visible | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---|---|---:|---:|---:|
| Trabajador | C/E | nombres de `Trabajador` | No | Sí | Sí |
| ID marcador · equipo | D/B | `AsignacionMarcador` del rango + nombre actual de equipo | Parcial | Sí | Sí |
| Empresa | B/E | `PeriodoLaboral.EmpresaId` + razón social actual | Parcial | Sí | Sí |
| Planta | B/E | `AsignacionPlanta` + nombre actual | Parcial | Sí | Sí |
| Centro | D/E | CECO actual; fallback al código de área | No/parcial | Sí | Sí |
| Centro de costo | D/E | CECO actual; fallback a sección actual | No | Sí | Sí |
| Puesto | D/E | `Trabajador.PuestoId` + nombre actual | No | Sí | Sí |
| Días puntual | A | agregación de `TareoDiario` | Hecho persistido | Sí, si se recalcula | Sí |
| Días tardanza | A | agregación de `TareoDiario` | Hecho persistido | Sí | Sí |
| Días falta | A | agregación de `TareoDiario` | Hecho persistido | Sí | Sí |
| Días ausencia justificada | A | agregación de `TareoDiario` | Hecho persistido | Sí | Sí |
| Días incompleto | A | agregación de `TareoDiario` | Hecho persistido | Sí | Sí |
| Días no laborable trabajado | A | descanso + feriado trabajados | Hecho persistido | Sí | Sí |
| Horas ordinarias | A | minutos de `TareoDiario` convertidos a horas | Hecho persistido | Sí | Sí |
| Horas extra 25 % | A | minutos de `TareoDiario` | Hecho persistido | Sí | Sí |
| Horas extra 35 % | A | minutos de `TareoDiario` | Hecho persistido | Sí | Sí |
| Horas nocturnas | A | minutos de `TareoDiario` | Hecho persistido | Sí | Sí |
| Horas de tardanza acumuladas | A | minutos de `TareoDiario` | Hecho persistido | Sí | Sí |
| Bono nocturno | A | minutos persistidos + parámetros/sueldo por fecha | Se recalcula al emitir | Sí | Sí |
| Movilidad | A | días asistidos + tarifa a fin del rango | Se recalcula al emitir | Sí | Sí |

### 2.2 Excepciones — 10

| Campo visible | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---|---|---:|---:|---:|
| Trabajador | C/E | nombres actuales | No | Sí | Sí |
| ID marcador · equipo | D/B | asignación del rango + equipo actual | Parcial | Sí | Sí |
| Fecha | A | `TareoDiario.Fecha` | Sí | Solo por nueva versión | Sí |
| Turno | D/E | `TareoDiario.TurnoId` + código actual del turno | Parcial | Sí | Sí |
| Entrada | A | `TareoDiario.EntradaReal` | Sí | Solo por nueva versión | Sí |
| Salida | A | `TareoDiario.SalidaReal` | Sí | Solo por nueva versión | Sí |
| Estado | A | `TareoDiario.Estado` | Sí | Solo por nueva versión | Sí |
| Minutos tardanza | A | `TareoDiario` | Sí | Solo por nueva versión | Sí |
| Minutos salida anticipada | A | `TareoDiario` | Sí | Solo por nueva versión | Sí |
| Observación | A | `TareoDiario.Observacion` | Sí | Solo por nueva versión | Sí |

### 2.3 Hoja Tareo — 57

| Campos visibles | Cantidad | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---:|---|---|---:|---:|---:|
| N° | 1 | F | ordinal de la emisión | No | Sí | Sí, para igualdad exacta |
| DNI | 1 | C/E | documento actual | No | Sí | Sí |
| Nombre | 1 | C/E | nombres actuales | No | Sí | Sí |
| Planta | 1 | B/E | historia de planta + nombre actual | Parcial | Sí | Sí |
| Centro; Centro de costo; Descrip. CECO | 3 | D/E | `CentroCostoId` y catálogos actuales | No | Sí | Sí |
| Área | 1 | D/B/E | `AsignacionArea`; fallback actual | Parcial | Sí | Sí |
| Fecha ingreso; Fecha cese | 2 | B | `PeriodoLaboral` aplicable al rango | Sí | Solo por corrección histórica | Sí |
| OBS | 1 | A | derivada de cese | Parcial | Sí | Sí |
| Faltas; Faltas justificadas; Total faltas | 3 | A | agregados de tareo | Hecho persistido | Sí | Sí |
| Suspensión | 1 | E | sin fuente; celda vacía | No aplica | No | No mientras siga vacía |
| DM; Vacaciones; Lic. s/goce; Lic. c/goce; Lic. paternidad; Sub. maternidad; Sub. enfermedad; Permisos | 8 | A | tareo + tipo de ausencia | Hecho persistido | Sí | Sí |
| Tardanza | 1 | A | minutos de tareo convertidos a horas | Hecho persistido | Sí | Sí |
| Ausencias; Tardanza manual; Días efectivas; Días pendientes; Días trabajados | 5 | E | columnas manuales vacías | No aplica | Fuera del sistema | No en la emisión del sistema |
| Hrs normales; HE25; HE35; HE100 | 4 | A | agregados de tareo | Hecho persistido | Sí | Sí |
| Movilidad calculada | 1 | A | días asistidos + tarifa | Se recalcula al emitir | Sí | Sí |
| B. nocturna normal; HE25 nocturna; HE35 nocturna; HE100 nocturna | 4 | A | agregados de tareo | Hecho persistido | Sí | Sí |
| Asignación familiar | 1 | A | `TieneHijos` actual + parámetros a fin de periodo | No | Sí | Sí |
| Reintegro horas normales; HE25; HE35; HE100 | 4 | A | `ReintegroTareo` por trabajador/periodo | Sí mientras periodo cerrado | Sí tras reapertura | Sí |
| Reintegro movilidad | 1 | A | `ReintegroTareo` | Sí mientras cerrado | Sí | Sí |
| Reintegro nocturno normal; HE25; HE35; HE100 | 4 | A | `ReintegroTareo` | Sí mientras cerrado | Sí | Sí |
| Viático; DM días; subsidio maternidad días; subsidio accidente días; licencia paternidad días; licencia con goce; reintegro asignación familiar; reintegro afecto; descuento pago indebido | 9 | A | `ReintegroTareo` | Sí mientras cerrado | Sí | Sí |

### 2.4 Descanso médico — 12

| Campo visible | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---|---|---:|---:|---:|
| Mes | F | periodo | Sí | No, salvo nueva versión | Sí |
| Razón social | B/E | contexto/razón social actual | Parcial | Sí | Sí |
| Empresa | B/E | en realidad muestra la planta histórica con nombre actual | Parcial | Sí | Sí |
| DNI / CE | C/E | documento actual | No | Sí | Sí |
| Apellidos y nombres | C/E | nombres actuales | No | Sí | Sí |
| Fecha inicio; Fecha fin | A | solicitud aprobada | Sí | Sí tras reapertura | Sí |
| Días en el mes | A | derivado contra el periodo | Recalculado | Sí | Sí |
| Contingencia; Diagnóstico; Centro de atención; Observación | A | solicitud/tipo de ausencia | Persistido | Sí | Sí |

### 2.5 Vacaciones — 9

| Campos visibles | Cantidad | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---:|---|---|---:|---:|---:|
| N° | 1 | F | ordinal de emisión | No | Sí | Sí |
| DNI; Nombre | 2 | C/E | ficha actual | No | Sí | Sí |
| Días; Fecha inicio; Fecha término; días del periodo; días del mes siguiente; retorno | 6 | A | solicitud y cálculos de reparto | Persistido/derivado | Sí | Sí |

### 2.6 Licencias — 10

| Campos visibles | Cantidad | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---:|---|---|---:|---:|---:|
| Mes | 1 | F | periodo | Sí | No, salvo nueva versión | Sí |
| DNI; Apellidos y nombres | 2 | C/E | ficha actual | No | Sí | Sí |
| Fecha inicio; Fecha fin; días en el mes | 3 | A | solicitud + derivación | Persistido/derivado | Sí | Sí |
| Contingencia; Diagnóstico; Centro de atención; Observación | 4 | A | tipo/solicitud | Persistido | Sí | Sí |

### 2.7 Trazabilidad — 11

| Campo visible | Clasificación | Fuente actual | ¿Histórico hoy? | ¿Puede cambiar? | ¿Afecta reproducibilidad? |
|---|---|---|---:|---:|---:|
| Razón social | B/E | tareo final/contexto actual | Parcial | Sí | Sí |
| Alcance; Desde; Hasta | F | filtro/rango | Sí en el archivo | Sí entre emisiones | Sí |
| Periodo código/nombre | F/E | catálogo `Periodo` | Parcial | Sí si está abierto | Sí |
| Estado del periodo | F | `Periodo.Estado` | Sí | Sí por reapertura | Sí |
| Filtros aplicados | F | filtro de emisión | No se persiste | Sí | Sí |
| Trabajadores en el libro | F/A | conteo emitido | No se persiste | Sí | Sí |
| Generado el; Generado por | F | reloj/usuario de emisión | No se persiste | Sí | Sí |
| Estado del cálculo | F/A | freshness calculado al emitir | No se persiste | Sí | Sí |

### 2.8 Hallazgo semántico que no debe congelarse sin validar

La columna `EMPRESA` de la hoja `Descanso Medico` se alimenta actualmente con
`porId[TrabajadorId].Planta`: el DTO llama `Empresa` al campo, el exportador escribe el encabezado
`EMPRESA`, pero `ArmarAnexosAsync` le entrega la planta. Esta fase no lo corrige porque su objetivo
es diseñar el snapshot, pero tampoco debe copiar ese desacople como contrato histórico. Planilla
debe confirmar si la columna espera empresa o planta antes de aprobar D01.

## 3. Qué debe congelarse y qué no

### 3.1 Snapshot común de contexto propuesto

Se proponen **15 valores renderizados por trabajador y versión**, suficientes para reproducir las
columnas maestras sin consultar la ficha actual:

1. `TrabajadorId` — identidad estable, no texto mostrado;
2. `NombreCompleto`;
3. `TipoDocumentoCodigo`;
4. `NumeroDocumento`;
5. `EmpresaTexto`;
6. `PlantaTexto`;
7. `CentroCodigo`;
8. `CentroCostoCodigo`;
9. `CentroCostoDescripcion`;
10. `AreaTexto`;
11. `PuestoTexto`;
12. `FechaIngreso`;
13. `FechaCese`;
14. `IdMarcadorTexto`;
15. `EquipoTexto`.

Además, para consulta y filtros históricos, los IDs y tramos que produjeron esos textos deberían
conservarse en filas estructuradas hijas: `PeriodoLaboralId`, empresa, asignaciones de planta, área
y marcador, sus fechas y orden. Esto evita convertir textos concatenados como “A → B” en una falsa
fuente relacional.

### 3.2 Resultados que deben congelarse por separado

No se propone copiar los siguientes valores dentro del contexto personal. Deben formar parte de
los resultados estructurados de la misma versión:

- todos los conteos, minutos/horas y estados visibles de Resumen y Tareo;
- bono nocturno, movilidad y asignación familiar ya valorizados;
- reintegros y descuentos;
- filas de excepción con entrada, salida, turno mostrado, estado y observación;
- filas de descanso médico, vacaciones y licencias;
- metadatos de aprobación, filtros, versión y procedencia.

La reemisión de una versión cerrada no debe recalcular estos resultados con código, parámetros,
catálogos o reglas posteriores. Los hechos fuente continúan disponibles para auditoría o para una
nueva versión autorizada.

### 3.3 Campos excluidos del snapshot común

Se excluyen **12 campos** del snapshot de contexto:

1. `Unidad`: no aparece en ninguna salida inventariada;
2. `TieneHijos` como dato familiar bruto: no es visible y duplica PII innecesaria;
3. teléfono;
4. dirección;
5. correo electrónico;
6. sexo;
7. `Ausencias` manual;
8. `Tardanza` manual;
9. `Días efectivas` manual;
10. `Días pendientes` manual;
11. `Días trabajados` manual;
12. `Suspensión`, mientras siga explícitamente sin fuente.

Para asignación familiar se congela el **monto aprobado** y, si Auditoría exige explicar la causa,
un indicador mínimo `AplicaAsignacionFamiliar`, no la composición familiar. Esta elección sigue
pendiente de RRHH/Privacidad.

## 4. Momento de creación

| Opción | Ventaja | Riesgo |
|---|---|---|
| A. Recalcular tareo | Tiene resultados listos | Puede ocurrir muchas veces; no expresa aprobación. |
| B. Cerrar periodo | Ya existe, bloquea escrituras y guarda actor/fecha/version | Hoy “cerrar” no valida que todos los reportes estén revisados. |
| C. Aprobar reporte | Semántica exacta y separada | El concepto y workflow no existen. |
| D. Primera exportación | No agrega paso operativo | El contenido dependería de quién exportó primero y cuándo. |

**Recomendación:** `B`, siempre que RRHH/Planilla confirme que cerrar equivale a aprobar. El
snapshot estructurado debe crearse atómicamente con el cierre; una falla impide cerrar. Si cierre y
aprobación son actos distintos, debe elegirse `C` y modelar el evento explícito. `A` y `D` se
descartan como frontera autoritativa.

**Decisión:** pendiente.

## 5. Granularidad recomendada

No alcanza una sola fila por trabajador+periodo: Excepciones necesita días individuales y el tareo
final contiene anexos. Tampoco conviene repetir DNI/empresa/CECO por cada día.

Se recomienda:

- raíz `Periodo + Version`;
- contexto y agregados `Trabajador + Periodo + Version`;
- detalle solo para días que aparecen en Excepciones;
- anexos por registro aprobado incluido;
- artefacto por `TipoReporte + Version`.

Los reportes por mes/día/rango libre no tienen hoy un cierre. Deben seguir siendo consultas vivas y
no llamarse “reemisión aprobada” salvo que negocio amplíe el concepto fuera de `Periodo`.

**Decisión:** pendiente.

## 6. Inmutabilidad, versionado y correcciones

Una versión cerrada no debe actualizarse ni borrarse. La raíz conceptual necesita:

```text
NumeroVersion
Estado = EnPreparacion | Aprobada | Sustituida | Anulada
Vigente
CreadoEn
CreadoPor
MotivoVersion
VersionPeriodo
VersionAplicacion
HashEstructurado
```

Reabrir conserva `v1`. Un nuevo cálculo y cierre genera `v2`; al aprobarse, `v2` pasa a vigente y
`v1` queda `Sustituida`, pero auditable. Ninguna corrección de maestro reescribe una versión.

Correcciones propuestas:

- personales legales (nombre/documento): conservar el original aprobado y crear versión corregida
  solo mediante flujo autorizado y motivo; nunca sustituir silenciosamente todas las versiones;
- organizativas (CECO/área/puesto): corregir la fuente, reabrir el periodo si corresponde,
  recalcular y cerrar como nueva versión; editar solo el maestro no cambia el snapshot;
- un ajuste de planilla que no reabre debe entrar como reintegro/ajuste explícito, no como mutación
  del cierre previo.

**Decisión:** pendiente para ambos tipos de corrección.

## 7. Política de reemisión

Tres políticas posibles:

| Política | Resultado |
|---|---|
| ORIGINAL | Siempre entrega v1, aunque haya una corrección aprobada. |
| ULTIMA_VIGENTE | Entrega la corrección actual, pero oculta versiones anteriores al usuario común. |
| SELECCIONABLE | Predetermina la vigente y permite elegir una versión histórica con trazabilidad. |

**Recomendación:** `SELECCIONABLE`, con la última vigente como opción predeterminada. “Descargar
original” devuelve el artefacto aprobado sin modificar bytes. “Reemitir copia certificada” usa la
misma versión de datos, agrega fecha/usuario de reemisión y por ello produce un artefacto y hash
nuevos enlazados al original.

**Decisión:** pendiente.

## 8. Periodos preexistentes y backfill

No existe evidencia para afirmar que la ficha actual representa lo aprobado en un periodo antiguo.
Se recomienda una política mixta:

| Calidad | Criterio | Uso permitido |
|---|---|---|
| `EXACTO` | Artefacto aprobado verificable o fuentes históricas completas y concordantes | Reemisión certificada. |
| `RECONSTRUIDO` | Se usaron maestros actuales o inferencias declaradas | Consulta comparativa; nunca “original”. |
| `INCOMPLETO` | Faltan campos o hay contradicción | Bloquea reemisión certificada. |
| `NO_DISPONIBLE` | No hay evidencia suficiente | Solo informa la imposibilidad histórica. |

No se recomienda backfill masivo automático con maestros actuales. Debe reconstruirse solo donde
haya evidencia; lo demás queda `INCOMPLETO` o `NO_DISPONIBLE`.

**Fecha de corte:** pendiente. Recomendación: primer periodo completo cerrado después de activar el
nuevo modelo, nunca una fecha retroactiva inventada.

## 9. Datos sensibles

- Conservar DNI/CE únicamente porque el tareo final y anexos lo requieren para el cruce de
  planilla. No copiar teléfono, dirección, correo, sexo ni composición familiar.
- Congelar el monto o indicador mínimo de asignación familiar, no `TieneHijos` salvo obligación
  expresa de auditoría.
- Aplicar la misma retención, respaldo, cuarentena y autorización de los reportes actuales.
- Un artefacto histórico no debe quedar en rutas públicas ni logs.

**Decisión:** pendiente de RRHH/Privacidad sobre documento completo, indicador familiar y plazo de
retención.

## 10. Autorización y auditoría

Estado actual:

- `COORDINADOR` y `ADMINISTRADOR` pueden cerrar/reabrir periodos;
- `COORDINADOR` y `GERENCIA` pueden consultar/exportar reportes;
- no existe permiso específico para versiones históricas.

Recomendación sin crear roles:

| Acción | Roles propuestos |
|---|---|
| Cerrar y crear snapshot | COORDINADOR, ADMINISTRADOR |
| Reabrir y generar nueva versión | COORDINADOR, ADMINISTRADOR, con motivo |
| Reemitir versión vigente | COORDINADOR, GERENCIA |
| Ver/reemitir versiones sustituidas | Pendiente: GERENCIA sola o también COORDINADOR |
| Anular una versión | Pendiente; al menos doble control o autorización superior |

Auditoría mínima: creación, aprobación/cierre, reapertura, sustitución/anulación, generación de
artefacto, descarga original y reemisión certificada; actor, fecha, motivo, versión y hash, sin
registrar contenido PII en el log.

## 11. Reglas, formato e integridad

No existe un `RuleSetVersion` único. Para no diseñar un motor paralelo, la versión debería guardar
como mínimo:

- versión de aplicación/cálculo desplegada;
- versión de esquema/migración;
- `Periodo.Version` y rango;
- instante mínimo/máximo de `CalculadoEn` incluido;
- IDs de versiones temporales relevantes cuando ya existan;
- formato del snapshot y del exportador.

El snapshot de resultados evita recalcular con reglas futuras; estas referencias explican cómo se
obtuvo, no sirven para reconstruirlo.

Formato recomendado: **modelo híbrido**. Tablas estructuradas para versión, trabajador, resultados,
excepciones y anexos; artefacto inmutable por tipo de reporte. JSON puede conservar un manifiesto
canónico extensible, pero no debe ser la única fuente de filtros o consultas.

Hash/integridad: **REQUERIDO técnicamente** para el manifiesto estructurado y cada artefacto. Usar
SHA-256 sobre una serialización canónica con versión explícita; guardar algoritmo, hash y tamaño.
Esta recomendación queda sujeta a aprobación de D12.

Los Excel de una copia certificada deberían mostrar versión aprobada, fecha/usuario de aprobación,
fecha/usuario de reemisión, calidad histórica y hash o identificador corto verificable.

## 12. Modelo conceptual recomendado

Sin definir todavía tablas, FKs ni migración:

1. `VersionReportePeriodo`: raíz de aprobación, estado, vigencia, calidad y hashes.
2. `SnapshotReporteTrabajador`: los 15 valores comunes renderizados por trabajador.
3. `SnapshotContextoTemporalTrabajador`: IDs/tramos que permiten filtros históricos sin parsear
   textos concatenados.
4. `SnapshotResultadoTrabajador`: agregados y montos aprobados compartidos por Resumen/Tareo final.
5. `SnapshotExcepcionReporte`: detalle diario mostrado en Excepciones.
6. `SnapshotAnexoTareo`: descanso médico, vacaciones y licencias incluidos en la versión.
7. `ArtefactoReportePeriodo`: tipo, formato, versión, tamaño, SHA-256 y almacenamiento del original
   o referencia durable controlada.

La raíz correcta propuesta es `Periodo + Version`, no `TareoDiario` ni una exportación aislada.
`TareoDiario` explica los hechos de origen; la versión aprobada conserva lo entregado.

## 13. Decisiones BD-REP004

> Nota de trazabilidad (2026-10-23): esta tabla conserva la numeración de las propuestas técnicas
> originales. El formulario posterior `BD-REP004-APROBACION` redistribuye los temas e incorpora
> `D02A`; su numeración es la canónica para capturar aprobaciones. No se recibió ninguna respuesta
> ni fuente autorizada, según `BD_REP004_APROBACION.md`. Por tanto, las recomendaciones siguientes
> siguen siendo antecedentes técnicos y no reglas aprobadas.

| ID | Tema | Recomendación | Estado |
|---|---|---|---|
| BD-REP004-D01 | Campos congelados | 15 de contexto + resultados/metadatos visibles separados | PENDIENTE |
| BD-REP004-D02 | Momento | Cierre de periodo, si cierre equivale a aprobación; de lo contrario aprobación explícita | PENDIENTE |
| BD-REP004-D03 | Granularidad | Raíz periodo+versión; trabajador; excepción-día; anexo; artefacto | PENDIENTE |
| BD-REP004-D04 | Reemisión | Seleccionable, última vigente por defecto | PENDIENTE |
| BD-REP004-D05 | Inmutabilidad/versionado | v1 permanece; corrección crea v2 vigente | PENDIENTE |
| BD-REP004-D06 | Correcciones personales | Nueva versión excepcional; nunca sobrescritura silenciosa | PENDIENTE |
| BD-REP004-D07 | Correcciones organizativas | Reapertura/reproceso o ajuste; maestro solo no altera cierre | PENDIENTE |
| BD-REP004-D08 | Históricos/backfill | Mixto por evidencia; calidad EXACTO/RECONSTRUIDO/INCOMPLETO/NO_DISPONIBLE | PENDIENTE |
| BD-REP004-D09 | Fecha de corte | Primer periodo completo posterior a activación; fecha exacta por aprobar | PENDIENTE |
| BD-REP004-D10 | PII | Documento necesario; minimizar indicador familiar y retención | PENDIENTE |
| BD-REP004-D11 | Autorización | Conservar roles; definir acceso a sustituidas y anulación | PENDIENTE |
| BD-REP004-D12 | Integridad/formato | Híbrido estructurado+artefacto; SHA-256 requerido | PENDIENTE |

Decisiones BD-REP004 nuevas aprobadas: **0**. Decisiones pendientes: **12**. `PL-PN05` permanece
como decisión previa aprobada y limita las alternativas, pero no responde estos doce detalles.

## 14. Preguntas de negocio

1. **RRHH/Planilla:** ¿Cerrar un periodo significa que sus reportes ya fueron revisados y
   aprobados, o hace falta una aprobación adicional?
2. **Planilla:** ¿Qué archivos deben quedar congelados: tareo final, resumen, excepciones o los
   tres? En `Descanso Medico`, ¿la columna `EMPRESA` espera realmente empresa o planta?
3. **Planilla/Auditoría:** Cuando existe una corrección, ¿la descarga normal debe entregar la
   última versión y permitir elegir la original?
4. **Auditoría:** ¿Debe conservarse para siempre cada versión sustituida y cada archivo original?
5. **RRHH:** Si luego se corrige legalmente un nombre o documento, ¿el reporte anterior conserva el
   dato aprobado o se emite una versión corregida?
6. **Planilla:** Si CECO, área o puesto estaban mal, ¿se reabre y vuelve a cerrar el periodo o se
   registra únicamente un ajuste/reintegro?
7. **RRHH/Privacidad:** ¿Es obligatorio conservar el documento completo y el indicador de
   asignación familiar en el snapshot? ¿Por cuánto tiempo?
8. **Auditoría:** ¿Un histórico reconstruido con datos actuales puede consultarse si aparece
   claramente como `RECONSTRUIDO`, o debe bloquearse?
9. **Gerencia:** ¿Qué fecha o primer periodo marcará el inicio obligatorio de snapshots nativos?
10. **Gerencia:** ¿Quién puede consultar versiones sustituidas: solo GERENCIA o también
    COORDINADOR?
11. **Gerencia/Auditoría:** ¿Quién puede anular una versión y se exige aprobación de dos personas?
12. **Auditoría:** ¿La “reemisión” debe descargar el archivo original idéntico o una copia nueva que
    muestre quién y cuándo la reemitió? ¿Se necesitan ambas opciones?

## 15. Estado de salida

```text
Código productivo: 0 cambios
Tests:              0 ejecutados / 0 creados
Migraciones:        0
Datos:              0 cambios
Producción:         NO TOCADA

REP-004 = BLOQUEADO
```

Siguiente paso: responder y aprobar `BD-REP004-D01..D12`. Solo entonces diseñar el esquema físico,
los contratos `REP004-*`, el tratamiento de backfill y la migración en una fase separada.

### Captura formal posterior

El formulario `BD-REP004-APROBACION` fue recibido sin respuestas seleccionadas ni fuentes
autorizadas. Su validación formal quedó registrada en `BD_REP004_APROBACION.md`:

```text
Aprobadas:       0
Pendientes:      13 (D01, D02, D02A, D03..D12)
Contradictorias: 0

REP-004 = BLOQUEADO
```

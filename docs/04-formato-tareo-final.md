# Formato del tareo final — especificación verificada

Fuente: `TAREO NOVIEMBRE 2024_PLANTA 1.xls` (CA Outsourcing SAC, periodo 01/11/2024–30/11/2024).
Extraído leyendo el libro con Excel COM: valores **y fórmulas**, no a ojo.

## 1. Cómo está armado el libro de GCA

El archivo trae 8 hojas. Solo tres son entregable; el resto es andamiaje manual:

| Hoja | Rol |
|---|---|
| `Plantilla` | **Hoja de trabajo manual.** 247 columnas: la grilla día por día del mes. Es lo que nuestro motor reemplaza. |
| `Tareo` | **Entregable.** 58 columnas (A:BF). No calcula nada. |
| `Descanso Medico` | **Entregable.** Detalle de DM y subsidios. |
| `Vacaciones-Licencias` | **Entregable.** Dos bloques en una hoja. |
| `Ceco` | Catálogo SAP de centros de costo de Intradevco (antiguo → nuevo). |
| `Hoja1`, `Tareo.`, `Practicante` | Restos de versiones anteriores / planilla de practicantes. |

**El hallazgo que define el diseño:** la hoja `Tareo` es una proyección pura por `VLOOKUP`
contra un rango llamado `DATA`, que es `Plantilla` empezando en la columna B (el DNI es la
clave de búsqueda). Cada celda es literalmente:

```
=VLOOKUP(B7, DATA, 193, 0)     → HRS NORMALES
=VLOOKUP(B7, DATA, 194, 0)     → horas extras 25 %
=M7+O7                          → TOTAL FALTAS = FALTAS + FALTAS JUS.
```

Es decir: `Tareo` no resume nada por sí misma, **transcribe** los totales que la persona de
RRHH cuadró a mano en `Plantilla`. Nuestro sistema calcula esos totales directamente, así
que la exportación escribe valores y `Plantilla` desaparece.

## 2. Hoja `Tareo` — estructura exacta

Encabezado:

| Celda | Contenido |
|---|---|
| `A1` | Razón social (`CA OUTSOURCING SAC`) |
| `A2` | `TAREO MENSUAL (OPERARIOS)` |
| `A3` | `MES: NOVIEMBRE` |
| `AG3` / `AH3` | `DEL` / fecha inicio |
| `AJ3` / `AK3` | `AL` / fecha fin |

Los títulos ocupan tres filas (4 = grupo, 5 = subgrupo, 6 = hoja), y los datos arrancan en
la **fila 7**. La última fila lleva totales por columna.

### Bloque identificación (A–K, título en fila 4)

`N°` · `DNI` · `NOMBRE` · `PLANTA` · `CENTRO` · `CENTRO DE COSTO` · `DESCRIP. CECO` ·
`AREA` · `FECHA INGRESO` · `FECHA CESE` · `OBS`

> Son 11 columnas, según el formato vigente de GCA (`RESUMEN FIN DE MES ... - PLANTA ...`).
> El formato anterior traía además `Texto Centro` y `C. Costo`, que ya no existen; por eso
> todo lo que sigue va corrido una columna a la izquierda respecto de aquel archivo.

### Bloque `REPORTE DE MOVIMIENTO DE PERSONAL EN DIAS` (L–X)

| Col | Título | Índice en `DATA` |
|---|---|---|
| M | FALTAS | 166 |
| N | SUSPENSION | 167 |
| O | FALTAS JUS. | 168 |
| P | TOTAL FALTAS | *fórmula* `=M+O` |
| Q | DM | 169 |
| R | VACACI. | 170 |
| S | LIC. S/ GOC | 171 |
| T | LIC. C/ GOC | 172 |
| U | LIC. PAT. | 173 |
| V | SUB. MAT | 174 |
| W | SUB. ENF | 175 |
| X | PERMISOS | 176 |
| Y | TARDANZA | 177 |

Todo en **días**, salvo `TARDANZA`, que viene en horas decimales (`0.55`, `5.40`).

### Columnas sueltas (Z–AD)

`Ausencias` · `Tardanza` · `Dias Efectivas` · `Dias Pendientes` · `Dias Trabajados`.
**Vienen vacías en todo el archivo**: son de llenado manual.

### Bloque `R E S U M E N` (AE–AM) — el corazón del reporte

| Col | Fila 5 | Fila 6 | Índice |
|---|---|---|---|
| AE | HORARIO DIURNO | HRS NORMALES | 193 |
| AF | | 0.25 | 194 |
| AG | | 0.35 | 195 |
| AH | | 1 | 196 |
| AI | MOVIL S/. | | 202 |
| AJ | HORARIO NOCTURNO | B.NOCT. (HORAS) | 197 |
| AK | | 0.25 | 198 |
| AL | | 0.35 | 199 |
| AM | | 1 | 200 |

Los títulos `0.25` / `0.35` / `1` son **números con formato de porcentaje**, no texto.

### Bloques de ajuste (AN–BF)

`ASIG. FAMILIAR` (AN) y `REINTEGROS` (AO–BF). El bloque de reintegros repite la misma
estructura del resumen (normales, 25 %, 35 %, 100 %, movilidad, nocturno ×4) y le agrega
`VIATICO`, `D.M. (DIAS)`, `SUB. MATERNIDAD (DIAS)`, `SUB. X ACCIDENTE (DIAS)`,
`LIC. PATER. (DIAS)`, `LICENCIA CON GOCE`, `REINT. ASIG. FAMILI`, `REINT. AFECTO`,
`DESC. PAGO INDEBIDO`.

Son **correcciones de periodos anteriores y conceptos de dinero**, cargados a mano por
planilla. Vienen en cero en el archivo modelo.

## 3. Hoja `Descanso Medico`

Títulos en la fila 4, datos desde la 5:

`MES` · `RAZÓN SOCIAL` · `EMPRESA` · `DNI / CE` · `APELLIDOS Y NOMBRES` · `FECHA INICIO` ·
`FECHA FIN` · `DIAS EN EL MES` · `CONTINGENCIA` · `DIAGNÓSTICO` · `CENTRO DE ATENCIÓN` ·
`OBSERVACION`

- `DIAS EN EL MES` es fórmula: `=FECHA FIN − FECHA INICIO + 1`.
- `RAZÓN SOCIAL` = la empresa de GCA (`CA OUTSOURCING SAC`).
- `EMPRESA` = cliente y planta (`INTRADEVCO-PLANTA 01`).
- `CONTINGENCIA` ∈ {`ENFERMEDAD COMUN`, `ACCIDENTE LABORAL`}.
- `CENTRO DE ATENCIÓN` ∈ {`ESSALUD`, `PARTICULAR`}.
- `OBSERVACION` ∈ {`DESCANSO MEDICO`, `SUBSIDIO`} — en la muestra, `SUBSIDIO` aparece
  cuando el descanso supera los 20 días.

## 4. Hoja `Vacaciones-Licencias`

Son **dos tablas apiladas** en la misma hoja.

**Bloque `VACACIONES`** (título `A1`, cabecera fila 2, datos fila 3+):

`N°` · `DNI` · `NOMBRE` · `DIAS` · `FEC_INICIO` · `FEC_TERMINO` · `«Mes del periodo»` ·
`«Mes siguiente»` · `RETORNO`

- `FEC_TERMINO` = `FEC_INICIO + DIAS − 1`
- `RETORNO` = `FEC_TERMINO + 1`
- Las dos columnas de mes reparten los días entre el mes del periodo y el siguiente; sus
  títulos son el **nombre del mes**, así que cambian en cada emisión.

**Bloque `LICENCIAS`** (título `A11`, cabecera fila 12, datos fila 13+):

`MES` · `DNI / CE` · `APELLIDOS Y NOMBRES` · `FECHA INICIO` · `FECHA FIN` ·
`DIAS EN EL MES` · `CONTINGENCIA` · `DIAGNÓSTICO` · `CENTRO DE ATENCIÓN` · `OBSERVACION`

Mismas columnas que `Descanso Medico` sin `RAZÓN SOCIAL` ni `EMPRESA`. En la muestra
`CONTINGENCIA` lleva el tipo (`LICENCIA CON GOCE / GESTANTE`) y las tres últimas van vacías.

## 5. Qué puede llenar el sistema hoy

| Columna del modelo | Origen en el sistema | Estado |
|---|---|---|
| N°, DNI, Apellidos y Nombres | `Trabajador` | ✅ |
| Planta | `Planta` | ✅ |
| F. Ingreso, F. Cese | `Trabajador` | ✅ |
| observacion | derivable del cese / estado | ⚠️ hay que definir el vocabulario |
| FALTAS | `TareoDiario.Estado = Falta` | ✅ |
| FALTAS JUS. | `Estado = AusenciaJustificada` | ✅ |
| TOTAL FALTAS | suma de las dos | ✅ |
| DM / VACACI. / LIC. S/ GOC / LIC. C/ GOC / LIC. PAT. / SUB. MAT / SUB. ENF / PERMISOS | `Justificacion.TipoAusenciaId` → `TipoAusencia.Codigo` (`DM`, `VAC`, `LSG`, `LCG`, `LPAT`, `LMAT`, `SUBE`, `PER`) | ✅ el catálogo sembrado calza 1:1 |
| TARDANZA | `MinutosTardanza / 60` | ✅ |
| HRS NORMALES | `MinutosOrdinarios` | ✅ |
| 0.25 / 0.35 diurnas | `MinutosExtra25` / `MinutosExtra35` | ✅ |
| 100 % diurna | días con `TipoDiaNoLaborable ≠ Ninguno` | ✅ derivable |
| B.NOCT. (HORAS) | `MinutosNocturnos` | ✅ |
| 0.25 / 0.35 / 1 nocturnas | `MinutosExtra25Nocturnos` / `MinutosExtra35Nocturnos` / derivado | ✅ el motor ya separa la nocturnidad por tramo |
| SUSPENSION | — | ❌ no existe el tipo de ausencia |
| CENTRO | `Centro.Codigo` — el `CO_AREA` del catálogo de SAP, vía `Trabajador.CentroCostoId` | ✅ real |
| CENTRO DE COSTO | `CentroCosto.Codigo` — el `CO_SECC` del catálogo de SAP | ✅ real |
| DESCRIP. CECO | `CentroCosto.Descripcion` — el `DE_SECC` del catálogo de SAP | ✅ real |
| AREA | `Area.Nombre` (catálogo interno) | ✅ real; **no se deriva del CECO** |
| MOVIL S/. | días asistidos × `ParametroLegal.ValorMovilidadDiaria` vigente | ✅ |
| ASIG. FAMILIAR | 10 % de `ParametroLegal.RemuneracionMinimaVital` vigente, si `Trabajador.TieneHijos` | ✅ monto fijo mensual, no prorrateado |
| REINTEGROS (18 columnas, AO-BF) | `ReintegroTareo`, capturado a mano en su propia pantalla | ✅ el sistema no los calcula; alguien de planilla los carga por trabajador y periodo |
| Ausencias, Dias Efectivas, Dias Pendientes, Dias Trabajados | — | ⬜ vacías también en el modelo original |
| DM: CONTINGENCIA, DIAGNÓSTICO, CENTRO DE ATENCIÓN | `SolicitudAusencia` | ✅ real; obligatorias en los tipos subsidiados |
| Vacaciones: DIAS, fechas, reparto por mes, RETORNO | `SolicitudAusencia` (`VAC`) | ✅ calculable |
| Licencias: fechas y días | `SolicitudAusencia` | ✅ |

✅ **Centro/Ceco ya usan el catálogo real de SAP**, entregado por GCA
(`CENTROS DE COSTO POR EMPRESA.xlsx`, 106 centros de costo bajo 5 centros SAP: 066D/E/F de
CA Outsourcing, 066G/H de Consulting Advisers) — sembrado en `Organizacion/Centro` y
`Organizacion/CentroCosto`. Se asigna por trabajador en Personal (cascada Empresa → Centro →
Centro de costo, igual que Empresa → Planta → Área → Sección) y queda en
`Trabajador.CentroCostoId` — campo plano, sin vigencia por fecha, igual que Área/Sección hoy.

**Sin aproximación con Área/Sección.** Si el trabajador todavía no tiene `CentroCostoId`
asignado, las tres columnas de CECO salen **vacías**. Rellenarlas con la jerarquía interna
—como se hacía antes de tener el catálogo— produciría un código de centro de costo que no
existe en SAP, y planilla no tiene cómo distinguirlo de uno real. El dato falta y así se ve.

**`AREA` es una columna propia y no se deriva del CECO.** En el archivo de GCA el CECO
`PE21420300` aparece con tres áreas distintas (`COORD. RRHH`, `SUP. PRODUCCION`,
`VOLANTES`), así que es un atributo por trabajador. Área sigue existiendo en Personal y
**convive** con el CECO en vez de haber quedado reemplazada por él.

**`Texto Centro` y `C. Costo` ya no existen.** Eran columnas del archivo modelo anterior
(`TAREO NOVIEMBRE 2024_PLANTA 1.xls`), donde además venían vacías en las 183 filas. El
formato vigente (`RESUMEN FIN DE MES ... - PLANTA ...`) las reemplazó por `CENTRO DE COSTO`
y `AREA`. Al quitarlas, el bloque de identificación pasó de 12 a 11 columnas y el resto de
la hoja corrió una posición a la izquierda: el ancho total es ahora **57 columnas (A-BE)**.

El bono nocturno en soles (D.S. 007-2002-TR, art. 8) **no está en esta hoja**: las 58
columnas de `Tareo` ya están verificadas celda por celda contra el archivo real de GCA, y el
bloque `HORARIO NOCTURNO` (AJ-AM) son horas, sin ninguna celda pensada para soles. Vive en el
**Resumen mensual** (`ResumenTareoDto.MontoBonoNocturno`), que sí es un reporte nuevo, sin la
restricción de calzar con un archivo modelo ya cerrado.

### Brechas de datos: ninguna abierta

Las dos que había están cerradas.

**Detalle clínico del descanso médico** — `SolicitudAusencia` ya guarda contingencia,
diagnóstico y centro de atención, y son **obligatorios** en los tipos cuyo comportamiento es
`Subsidiada` (hoy `DM`, `SUBE` y `SUBA`). La obligatoriedad la decide el comportamiento del
catálogo y no una lista de códigos, así que un tipo subsidiado nuevo la hereda sin tocar
código. Contingencia y centro de atención son enums (`ENFERMEDAD COMUN` / `ACCIDENTE
LABORAL`, `ESSALUD` / `PARTICULAR`) y se escriben en la hoja con el texto exacto del archivo
de GCA, no con el `ToString()` del enum.

> **El diagnóstico es un dato sensible de salud** (Ley 29733, art. 2.5), con protección más
> estricta que un dato personal común. Por eso: la bitácora de auditoría registra que alguien
> lo cambió pero nunca su valor; el listado de solicitudes muestra contingencia y centro de
> atención pero **no** el diagnóstico; y cambiar el tipo de una solicitud a uno no subsidiado
> borra los tres campos, para que no sobreviva un diagnóstico sin causa que lo justifique.

**CECO real** — catálogo `Centro`/`CentroCosto` sembrado desde el archivo que entregó GCA,
asignable por trabajador en Personal.

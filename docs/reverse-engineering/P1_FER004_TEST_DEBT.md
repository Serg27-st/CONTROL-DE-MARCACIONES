# P1-FER-004 — cierre de deuda de test

Fecha de revisión: 2026-10-22

Estado: **CERRADO**

Producción: **no tocada**

## Definición exacta

```text
FER-004:
  turno iniciado antes de un feriado y terminado dentro de él
  → conserva la clasificación del día de inicio

  turno iniciado durante el feriado y terminado en D+1
  → conserva la clasificación de feriado hasta el final de la jornada

  en ambos casos
  → una sola cantidad de minutos físicos
```

Fuentes: FER-NAC-05/06 y BD02-D08/D09. La regla es de clasificación temporal por fecha de
jornada; no redefine la economía ya cerrada por FER-002 ni el sustitutorio de FER-003.

## Estado inicial reconciliado

La implementación ya existía y era correcta. También existían dos pruebas de dominio
(`FER_NAC_05...` y `FER_NAC_06...`), pero ambas recibían `EntradaCalculo.EsFeriado` ya
resuelto. Por tanto, demostraban el comportamiento del motor una vez conocida la
clasificación, no que el servicio consultara el catálogo con la fecha de inicio.

La documentación tenía una inconsistencia interna:

- la fila `RQ-FER-004` de la matriz decía `NINGUNO / IMPLEMENTADO` y citaba las pruebas de
  dominio;
- `REQUISITOS_SIN_TEST` afirmaba correctamente que faltaba el contrato directo de la
  frontera contra el catálogo real.

La deuda se clasifica como `DEUDA_TEST`; no se encontró un bug productivo.

## Implementación confirmada

| Archivo | Clase / método | Responsabilidad |
|---|---|---|
| `Domain/Asistencia/CalculoTareo.cs` | `CalculadoraTareo.CalcularSinJustificar`, `Jornada` | Calcula toda la jornada usando `EntradaCalculo.Fecha`, incluida la salida D+1. |
| `Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs` | `ServicioCalculoTareo.RecalcularPeriodoAsync` | Para cada fecha de jornada resuelve `EsFeriado` con `Feriado.Fecha == fecha` y el alcance nacional/planta; no consulta la fecha de salida. |
| `Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs` | `Volcar` | Persiste fecha, minutos y `TipoDiaNoLaborable` resultantes sin partir ni duplicar la jornada. |

## Contratos directos agregados

Archivo:

`tests/ControlMarcacion.Infrastructure.Tests/Asistencia/FeriadoFechaInicioAcceptanceTests.cs`

Los contratos usan SQLite real, un `Feriado` nacional cargado en catálogo, turno nocturno
19:00–05:22, asignación temporal, marcaciones crudas e invocación de
`RecalcularPeriodoAsync`.

### FER004-01

```text
27/07 normal 19:00
→ 28/07 feriado 05:22

Resultado:
EstadoTareo = Puntual
TipoDiaNoLaborable = Ninguno
MinutosTrabajados = 622
MinutosOrdinarios = 622
HE25/HE35 = 0
```

### FER004-02

```text
28/07 feriado 19:00
→ 29/07 normal 05:22

Resultado:
EstadoTareo = Puntual
TipoDiaNoLaborable = Feriado
MinutosTrabajados = 622
MinutosOrdinarios persistidos = 622
HE25/HE35 = 0
```

Los minutos ordinarios persistidos describen la distribución física del tareo. FER-002 ya
garantiza que la liquidación los excluya de la base ordinaria al ver la clasificación
`Feriado` y valorice una sola base de 622 minutos con su recargo. FER-004 no duplica esa
economía ni cambia su representación.

## Validación dirigida

```text
FeriadoFechaInicioAcceptanceTests: 2/2 verdes
FeriadosEconomiaAcceptanceTests:   5/5 verdes
Total dirigido:                    7/7 verdes
Fallos:                            0
Omitidos:                          0
```

Durante la construcción del contrato se descartaron dos expectativas incorrectas del propio
test: `TareoDiario` no persiste `MinutosExtra100` y conserva la distribución ordinaria física
del feriado trabajado. Ninguna de las dos fue un fallo productivo; los contratos finales
quedaron alineados con la separación física/económica vigente.

## Decisión

```text
Implementación presente = SÍ
Comportamiento correcto = SÍ
Contrato directo previo servicio→catálogo→tareo = NO
Contratos directos agregados = 2
Código productivo modificado = NO
Migración = NO
Bug productivo encontrado = NO

FER-004 = CERRADO
```

## Validación final

```text
Build Release:  0 errores, 0 warnings
Suite completa: 977/977 verdes, 0 fallos, 0 omitidos
Regresiones:    0
```

No se ejecutó OP-03, no se aplicaron migraciones productivas, no se tocó producción y no se
creó commit. FER-005, ASIS-001, PeriodoLaboral, PMV, BKtransaction, Identity e instalador
permanecen fuera de alcance.


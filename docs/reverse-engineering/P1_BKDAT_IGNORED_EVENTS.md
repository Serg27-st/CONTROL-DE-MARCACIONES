# P1-BKDAT-IGNORED-EVENTS — separación de eventos de equipo y errores

Fecha de cierre: 15/10/2026. Alcance: clasificación de lectura, contadores de importación y presentación web. Producción no fue tocada, no se creó migración y no se integró el SDK ZKTeco.

## Resultado

El flujo distingue ahora tres resultados físicos:

```text
Aceptada  = marcación humana válida
Ignorada  = evento estructuralmente válido del equipo, sin persona
Rechazada = dato inválido, corrupto o no interpretable
```

Los registros binarios con layout y timestamp válidos cuyo identificador físico es cero se representan como `EventoEquipoIgnorado`. No se convierten en `MarcacionLeida`, no se persisten como `MarcacionCruda` y tampoco se guardan como `ErrorImportacion`.

No se asignó significado funcional a los códigos 27, 751 o 752. El modelo conserva únicamente el número físico del registro y su código opaco.

## Inventario y cambios

| Componente | Estado anterior | Estado nuevo |
|---|---|---|
| `ResultadoLectura` | `Marcaciones` + `Rechazadas` | añade `EventosEquipoIgnorados` |
| `LectorRespaldoZkteco.Extraer` | ID cero producía `LineaRechazada` | ID cero, después de validar layout y timestamp, produce `EventoEquipoIgnorado` |
| `ServicioImportacion` | todos los rechazos se guardaban en `ErrorImportacion` | solo los errores reales se guardan; ignorados no generan entidad |
| `ResultadoImportacion` | leídas, insertadas, duplicadas, rechazadas | añade `FilasIgnoradas` y `FilasAceptadas` |
| `ImportacionHistorialDto` | no distinguía ignorados | expone `FilasIgnoradas` y `FilasAceptadas` |
| resultado web | mostraba los eventos como rechazados | muestra detectados, válidos, insertados, duplicados, ignorados y errores |
| historial web | columna `Rechazadas` | columnas separadas `Ignoradas` y `Errores` |

La decodificación binaria permanece igual: misma cabecera, selección de desplazamiento, offsets, little-endian y algoritmo de timestamp. La condición de seguridad también permanece: ID cero nunca puede ser una marcación.

## Contrato de datos sin migración

No se añadió columna ni tabla. El contador histórico se deriva de los contadores ya persistidos:

```text
ignoradas = max(
    filas leídas
  - filas insertadas
  - filas duplicadas
  - filas rechazadas,
  0)
```

La identidad es válida porque `insertadas + duplicadas` son todas las marcaciones humanas aceptadas, `rechazadas` son errores reales y `leídas` incluye las tres categorías. Se aplica tanto al resultado recargado como al historial. Para importaciones históricas que registraron los eventos ID cero como rechazados, el resultado derivado sigue siendo cero: no se reescribe su historia ni se reclasifican filas persistidas sin migración.

El detalle de códigos no se persiste. Es una decisión deliberada: el requisito operativo necesita el contador, y código más cantidad bastan para diagnóstico en memoria/log sin crear una nueva superficie de datos. La UI no muestra agrupación histórica por código en esta fase.

## Protecciones conservadas

- Un ID físico cero no crea `MarcacionCruda` bajo ninguna circunstancia.
- Un timestamp imposible continúa en `LineaRechazada` y `ErrorImportacion`.
- Un layout desconocido continúa rechazando el archivo.
- Un cuerpo con bytes de un registro incompleto se rechaza explícitamente como truncado; no entrega el prefijo como una carga aparentemente completa.
- Los ignorados no se mezclan con la deduplicación.
- IDs humanos sin asignación siguen siendo marcaciones conservadas y un aviso de padrón; no se confunden con eventos de equipo.

La validación explícita de remanente corrige la protección de truncamiento sin cambiar cómo se decodifica ningún registro completo.

## Contratos BKIGN

| Contrato | Evidencia ejecutable |
|---|---|
| BKIGN-01 | ID cero con estructura y timestamp válidos queda ignorado y no rechazado |
| BKIGN-02 | carga solo de ignorados produce cero `MarcacionCruda` y cero `ErrorImportacion` |
| BKIGN-03 | código 27 se conserva como ignorado |
| BKIGN-04 | código 751 se conserva como ignorado |
| BKIGN-05 | código 752 se conserva como ignorado |
| BKIGN-06 | timestamp imposible sigue rechazado |
| BKIGN-07 | registro truncado produce error estructural |
| BKIGN-08 | fixture sintético de 981 registros da 851 aceptados, 130 ignorados y cero errores |
| BKIGN-09 | una importación solo con ignorados queda completada (`HuboProblemas = false`) y el contador sobrevive a la recarga/historial |
| BKIGN-10 | resultado e historial Razor contienen contadores separados de ignorados y errores; la compilación valida las vistas |

El fixture BKIGN-08 reproduce solo la estructura y distribución verificadas; no incorpora el archivo real ni identificadores personales:

```text
981 físicos
851 aceptados, código 3
126 ignorados, código 27
2 ignorados, código 751
2 ignorados, código 752
0 errores
```

El fixture real mínimo ya versionado (`Recursos/BKtransaction.dat`) queda en:

```text
9 físicos
7 aceptados
2 ignorados
0 errores
7 MarcacionCruda
0 ErrorImportacion por eventos del equipo
```

## Resultado esperado para BKtransaction(5).dat

La auditoría física previa demostró la distribución del corpus por cada registro. Aplicando la clasificación ahora ejecutable:

```text
Físicos:     981
Aceptados:   851
Ignorados:   130
Errores:       0
Duplicados:   11
Persistidos: 840
```

Los duplicados se calculan después de la lectura y no alteran la identidad `851 aceptados + 130 ignorados = 981 físicos`.

## Decisión

```text
Migración nueva: NO
Tabla de ignorados: NO
Detalle histórico por código: NO
Contador histórico: SÍ, derivado
UI separada: SÍ
ID cero persistible como marcación: NO
Errores estructurales protegidos: SÍ
```

## Verificación final

```text
Baseline:
  Build Release: 0 errores / 0 warnings
  Tests: 937/937 verdes, 0 omitidos

Regresión dirigida Marcaciones:
  159/159 verdes

Final:
  Build Release: 0 errores / 0 warnings
  Tests: 946/946 verdes, 0 omitidos
    Domain: 419
    Infrastructure: 497
    Web: 30
```

Siguiente paso recomendado: ninguno dentro de este frente. Si se requiere detalle histórico agrupado por código, debe abrirse un contrato separado que justifique su persistencia y retención; no debe reutilizar `ErrorImportacion`.

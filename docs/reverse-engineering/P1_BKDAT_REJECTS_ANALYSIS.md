# P1-BKDAT-REJECTS — auditoría de los 130 registros rechazados

> **SUPERADA funcionalmente el 15/10/2026 por `P1-BKDAT-IGNORED-EVENTS`:** la evidencia
> física de esta auditoría se conserva, pero los 130 registros ya no se presentan como
> errores. Ahora son `EventoEquipoIgnorado`: cuentan como ignorados, no se guardan en
> `ErrorImportacion` y nunca crean `MarcacionCruda`. Véase
> `P1_BKDAT_IGNORED_EVENTS.md`.

Fecha de auditoría: 15/10/2026. Alcance: lectura binaria local y generación de evidencia diagnóstica. No se modificó el parser, el flujo de importación, la base de datos ni producción.

## Resultado ejecutivo

La clasificación final es **LECTOR CORRECTO** para este hallazgo. Los 130 registros rechazados no contienen una marcación personal recuperable: todos tienen el campo físico de identificador en cero, el segundo campo de cuatro bytes también en cero, un timestamp real y códigos de evento que no aparecen en ninguna de las 851 marcaciones aceptadas. No hay registros corruptos, footer, layout alternativo ni marcaciones posibles dentro de esos 130.

Los 130 se clasifican como `EVENTO_NO_MARCACION`. Esta clasificación identifica su naturaleza estructural —eventos del equipo sin persona—, pero no asigna nombres funcionales como apertura, alarma, administración o sincronización: el corpus y el código no demuestran esos subtipos.

No se requiere cambiar `LectorRespaldoZkteco` para recuperar marcaciones de este grupo. Como mejora futura de nomenclatura, conviene separar en la salida de importación los eventos de equipo ignorados de los registros realmente malformados. Esta propuesta no se implementa en esta fase.

## Corpus y reproducibilidad

| Propiedad | Valor |
|---|---|
| Archivo | `BKtransaction(5).dat` |
| SHA-256 | `307A91B5AE252F2D3FCB9A0A33A5521981835C9D0291F250AC78C0EA558C97FB` |
| Tamaño | 27 531 bytes |
| Serie | `VGU6251600751` |
| Modelo/versión | `ZAM70-NF28VA-Ver3.0.15` |

La herramienta reproducible es `tools/diagnostico-bkdat-rejects.ps1`. Verifica opcionalmente el SHA-256, interpreta la cabecera, reproduce la selección de layout por timestamps plausibles, recorre solo registros físicos completos y exporta cada byte relevante. Los resultados están en:

- `artifacts/bktransaction-rejects/all-records.csv`;
- `artifacts/bktransaction-rejects/accepted.csv`;
- `artifacts/bktransaction-rejects/rejected.csv`;
- `artifacts/bktransaction-rejects/summary.json`.

El análisis ocurre antes de la deduplicación de `ServicioImportacion`. Por tanto, `981 = 851 + 130` describe exclusivamente la decodificación física; no se mezcla con las 11 dobles fichadas que una fase posterior colapsa.

## Lector y decisiones actuales

| Archivo | Clase / método | Responsabilidad observada |
|---|---|---|
| `src/ControlMarcacion.Infrastructure/Marcaciones/DetectorFormatoMarcador.cs` | `DetectorFormatoMarcador.Detectar` | Lee las dos cadenas de cabecera, tamaño de registro, cantidad y descriptores de campos; devuelve el comienzo del cuerpo. |
| `src/ControlMarcacion.Infrastructure/Marcaciones/LectorRespaldoZkteco.cs` | `LectorRespaldoZkteco.Leer` | Detecta la cabecera, calcula registros completos por división entera, exige timestamp declarado de cuatro bytes y selecciona el desplazamiento. |
| mismo | `DeducirDesplazamiento` / `ContarFechasPlausibles` | Prueba la holgura disponible; prioriza timestamp alineado a cuatro bytes y exige al menos 90 % de fechas entre 2015 y un año hacia el futuro. |
| mismo | `LeerCampos` / `LeerEntero` | Lee los campos declarados en little-endian. |
| mismo | `DecodificarTiempo` | Decodifica el entero ZKTeco, no Unix, y rechaza combinaciones calendáricas imposibles. |
| mismo | `Extraer` | Rechaza, en orden, fecha imposible, identificador cero y antigüedad excesiva; acepta el resto como `MarcacionLeida`. |

El lector no mantiene una tabla de layouts por modelo. Admite una cabecera estructural válida, exige que el último campo declarado mida cuatro bytes y deduce el comienzo del bloque de campos. En este corpus seleccionó inequívocamente el desplazamiento 8.

## Cabecera, cuerpo y layout probado

La cabecera termina en el offset absoluto 63:

| Offset absoluto | Contenido |
|---:|---|
| 0 | longitud de serie: 13 |
| 1–13 | `VGU6251600751` |
| 14 | longitud de modelo: 22 |
| 15–36 | `ZAM70-NF28VA-Ver3.0.15` |
| 37–40 | tamaño de registro `28`, `uint32` little-endian |
| 41 | 7 campos |
| 42–62 | siete descriptores de tres bytes |
| 63 | primer byte del primer registro |

Los anchos declarados son `4, 4, 1, 1, 1, 1, 4`: suman 16 bytes y dejan 12 bytes de holgura dentro de cada registro de 28 bytes. El bloque declarado comienza en el offset relativo 8:

| Offset relativo | Longitud | Interpretación demostrada o estado |
|---:|---:|---|
| 0–7 | 8 | relleno; cero en todo el corpus |
| 8–11 | 4 | identificador `uint32` little-endian |
| 12–15 | 4 | campo auxiliar no determinado; cero en los 981 registros |
| 16 | 1 | modo de verificación expuesto por el lector; semántica detallada no determinada |
| 17 | 1 | tipo expuesto por el lector; valor 1 en los 981 registros |
| 18 | 1 | byte bajo del código de evento diagnóstico |
| 19 | 1 | byte alto/reservado del código de evento diagnóstico |
| 20–23 | 4 | timestamp ZKTeco `uint32` little-endian |
| 24–27 | 4 | relleno; cero en todo el corpus |

`CodigoEvento` en los artefactos es el entero diagnóstico little-endian formado por los bytes relativos 18–19. El lector productivo no usa ese código para decidir la aceptación ni le asigna un nombre de negocio.

## Por qué hay 981 registros y no 980

El conteo queda cerrado aritmética y físicamente:

```text
tamaño del archivo       = 27 531
cabecera                 =     63
cuerpo                   = 27 468
tamaño de cada registro  =     28
27 468 / 28              =    981 exactos
remanente/footer         =      0 bytes
```

El registro 981 comienza en el offset absoluto 27 503 y ocupa hasta el final del archivo. Es una marcación completa y válida:

```text
ID:        147
FechaHora: 2026-10-06 08:49:05
RawHex:    00000000000000009300000000000000010103000112463300000000
```

La extracción independiente anterior tenía 980 filas porque conservó los timestamps físicos 1–980, desplazó el identificador una fila hacia adelante y omitió el último timestamp. No descubrió un footer: perdió un registro real por su propio emparejamiento. En el archivo no existe footer, checksum ni metadato posterior a los 981 registros.

## Resultado de los 130 rechazados

### Motivo del lector

| Motivo de rechazo | Cantidad |
|---|---:|
| `El registro no tiene identificador de marcador: no corresponde a ninguna persona y el equipo lo escribió para sí mismo.` | 130 |
| **Total** | **130** |

No hay rechazos por timestamp imposible ni por antigüedad en este corpus.

### Clasificación exhaustiva

| Clasificación permitida | Cantidad |
|---|---:|
| `EVENTO_NO_MARCACION` | 130 |
| `METADATO` | 0 |
| `CONTROL` | 0 |
| `FOOTER` | 0 |
| `CORRUPTO` | 0 |
| `TIMESTAMP_INVALIDO` | 0 |
| `ID_INVALIDO` | 0 |
| `LAYOUT_ALTERNATIVO` | 0 |
| `MARCACION_POSIBLE` | 0 |
| `NO_DETERMINADO` | 0 |
| **Total** | **130** |

La ausencia de subtipo semántico para los códigos no convierte la clasificación estructural en `NO_DETERMINADO`: sí está demostrado que no son marcaciones personales; lo no determinado es el nombre concreto de cada evento del equipo.

### Tres patrones estructurales

| Patrón | Cantidad |
|---|---:|
| ID `00000000`, auxiliar 0, modo 1, tipo 1, evento 27, padding final cero | 126 |
| ID `00000000`, auxiliar 0, modo 200, tipo 1, evento 751, padding final cero | 2 |
| ID `00000000`, auxiliar 0, modo 200, tipo 1, evento 752, padding final cero | 2 |

Los 130 registros miden 28 bytes, empiezan con ocho bytes cero y terminan con cuatro bytes cero. Sus 130 registros raw completos son distintos por timestamp o código. No aparece firma de otro layout.

### Identificadores, PIN, Card y Badge

En los 130 rechazados:

```text
campo ID (offset 8–11):        00000000 en 130/130
campo auxiliar (offset 12–15): 00000000 en 130/130
```

El único campo probado por el lector y el corpus como identificador personal es el primero. No hay evidencia para reinterpretar el segundo campo como PIN, Card o Badge, y en todo caso también vale cero. Los otros valores no nulos pertenecen al modo, tipo, evento y timestamp; usarlos como identidad inventaría una persona. La salida inservible del SDK para este corpus no aporta una identidad alternativa autoritativa.

### Timestamps

| Métrica | Resultado |
|---|---:|
| Válidos | 130 |
| Fuera de rango | 0 |
| Imposibles | 0 |
| Cero | 0 |
| Valores raw distintos | 129 |

El rango de los rechazados es `2026-10-01 06:44:33` a `2026-10-06 08:43:42`. La única repetición exacta de timestamp raw es `BE104633` en los registros 976 y 977: forman el par de eventos 751/752 en el mismo segundo. Un timestamp real solo demuestra que el equipo fechó el evento; no aporta una identidad ni lo convierte en marcación.

### Códigos: aceptados frente a rechazados

| Código | Aceptados | Rechazados |
|---:|---:|---:|
| 3 | 851 | 0 |
| 27 | 0 | 126 |
| 751 (`0x02EF`) | 0 | 2 |
| 752 (`0x02F0`) | 0 | 2 |

La separación es completa en 981/981 filas: código 3 implica ID distinto de cero; 27, 751 y 752 implican ID cero. No se asignan etiquetas como apertura, alarma, acceso, administración, enrolamiento o sincronización porque ni el código ni el corpus las demuestran.

## Comparación temporal y registros adyacentes

Los 130 eventos aparecen en 70 secuencias intercaladas entre marcaciones válidas:

| Longitud de secuencia rechazada | Cantidad de secuencias |
|---:|---:|
| 1 | 46 |
| 2 | 16 |
| 3 | 3 |
| 4 | 1 |
| 6 | 1 |
| 7 | 1 |
| 8 | 1 |
| 18 | 1 |

Las 70 secuencias tienen una marcación aceptada antes y otra después. No hay inversiones temporales en los 981 registros. Esto descarta que los rechazados sean un bloque de footer o una lectura accidental fuera del cuerpo y demuestra que pertenecen al mismo flujo cronológico de eventos del equipo.

Ejemplos representativos:

| Registros | Secuencia |
|---|---|
| 3–5 | aceptado ID 140/evento 3 a `06:44:27` → rechazado evento 27 a `06:44:33` → rechazado evento 27 a `06:45:39`; el siguiente aceptado es el 6, ID 13, a `06:45:42` |
| 666–669 | aceptado ID 88/evento 3 a `06:51:04` → eventos 751 y 752 a `06:52:01`/`06:52:04` → aceptado ID 5/evento 3 a `06:52:07` |
| 975–978 | aceptado ID 77365312/evento 3 a `07:30:50` → eventos 751 y 752 a `08:43:42` → aceptado ID 143/evento 3 a `08:45:10` |

Cada fila de `rejected.csv` incluye además los índices, IDs, códigos y fechas de `N-1` y `N+1`, por lo que la comparación no depende solo de estos ejemplos.

## Validación de los 851 aceptados

Se verificaron 30 aceptados en su registro físico completo:

- primeros diez aceptados: índices `1, 2, 3, 6, 7, 8, 9, 11, 12, 13`;
- diez centrales: posiciones aceptadas 421–430, índices físicos `466–475`;
- últimos diez aceptados: índices `968, 969, 970, 971, 973, 975, 978, 979, 980, 981`.

En los 30, el ID decodificado coincide con los cuatro bytes little-endian del offset 8, la fecha coincide con los cuatro bytes ZKTeco del offset 20 y el raw completo tiene 28 bytes. Los objetos completos de las tres muestras, incluidos `RawHex`, ID y fecha, están conservados en `summary.json` bajo `MuestraAceptados`.

Los extremos verificados son:

| Índice | ID | Fecha/hora | RawHex |
|---:|---:|---|---|
| 1 | 141 | `2026-10-01 03:26:09` | `00000000000000008D0000000000000001010300D12E3F3300000000` |
| 981 | 147 | `2026-10-06 08:49:05` | `00000000000000009300000000000000010103000112463300000000` |

El total aceptado conserva 127 IDs distintos y el rango `2026-10-01 03:26:09`–`2026-10-06 08:49:05`.

## Corpus adicional acotado

Se usó únicamente el fixture real ya presente en pruebas: `tests/ControlMarcacion.Infrastructure.Tests/Recursos/BKtransaction.dat`, SHA-256 `78E00355C2471CFB0FC5518F723A165DA0EFA76683AC70D5AF7A43922216E8C9`, serie `VGU6251600671`, mismo modelo.

Contiene nueve registros completos, sin footer: siete tienen ID distinto de cero y evento 3; dos tienen ID cero y eventos 614/718. El patrón de separación `evento 3 + identidad` frente a `otro evento + identidad cero` se repite, aunque los números concretos de eventos del equipo varían. No se usaron archivos truncados, mutados ni un barrido masivo como corpus adicional.

## Decisión y límites

| Pregunta | Respuesta basada en evidencia |
|---|---|
| ¿Hay marcaciones válidas entre los 130? | **No** |
| Cantidad potencialmente perdida | **0** |
| ¿Existe layout alternativo? | **No observado** |
| ¿Son footer o bytes fuera del cuerpo? | **No**; ocupan registros completos dentro del flujo y el remanente es cero |
| ¿Debe cambiarse `LectorRespaldoZkteco` para este hallazgo? | **No** |
| Clasificación final | **LECTOR CORRECTO** |

Esta decisión se limita a los 130 registros de este corpus. No cierra ni altera deudas previamente documentadas sobre archivos truncados o timestamps futuros. Tampoco valida nombres semánticos de los códigos sin una referencia externa fiable.

Siguiente paso recomendado: abrir una mejora separada, no bloqueante y con contrato propio, para exponer estos casos como `IgnoradosPorTipo` o `EventosDeEquipoIgnorados` en vez de mezclarlos con errores de formato. No cambiar la condición de seguridad que impide atribuir un registro con ID cero a una persona.

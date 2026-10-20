# BKtransaction.dat — validación del descifrado

Fecha de auditoría: 13/10/2026. Alcance: lectura local, copias de diagnóstico y SQLite temporal. No se tocó producción ni se modificó `LectorRespaldoZkteco`.

## Resultado ejecutivo

El archivo se detecta por contenido como respaldo binario y el lector actual interpreta correctamente la cabecera, la estructura de 28 bytes y los timestamps ZKTeco. Las 980 fechas de la referencia coinciden exactamente, con tolerancia de 0 segundos, con los primeros 980 registros físicos del archivo.

La comparación directa `IdMarcador + FechaHora` contra el CSV proporcionado no cierra porque ese CSV tiene un desfase de una fila: combina el timestamp del registro `N` con el identificador del registro `N+1`. La prueba byte a byte lo demuestra en las 979 filas interiores: al realinear el ID una posición hacia atrás, las 979 coinciden exactamente. El primer ID físico y el último timestamp físico quedaron fuera del CSV de referencia.

Se encontraron dos defectos reales, reproducibles y separados de ese problema de referencia:

1. Un archivo cortado en un byte pierde silenciosamente el último registro parcial; no genera un rechazo explícito.
2. Un timestamp alterado a `FF FF FF FF` se acepta como `18/08/2133 06:28:15`. La deducción del layout limita fechas futuras, pero `Extraer` no aplica ese mismo máximo al registro individual.

Por ello, la validación global queda **PARCIAL** y requiere una remediación posterior acotada. Esta fase no cambia el lector.

## Archivos analizados

| Archivo | SHA-256 | Tamaño |
|---|---|---:|
| `BKtransaction(5).dat` | `307A91B5AE252F2D3FCB9A0A33A5521981835C9D0291F250AC78C0EA558C97FB` | 27 531 bytes |
| CSV de decodificación independiente | `CD6C952557718BC679A04E76F0052A943E56DD567828C8525464190465EF8E55` | 105 080 bytes |

Los hashes de las copias de trabajo coincidieron con los originales. Los originales no se modificaron.

## Flujo real auditado

1. `src/ControlMarcacion.Web/Controllers/MarcacionesController.cs` — `MarcacionesController.Cargar`: valida archivo, planta, equipo y tamaño; copia a memoria; consulta recarga previa; llama `IServicioImportacion.ImportarAsync`; redirige a `Resultado`.
2. `src/ControlMarcacion.Infrastructure/Marcaciones/ServicioImportacion.cs` — `ImportarAsync`: calcula SHA-256, rebobina el stream, llama `IFuenteDeMarcaciones.Leer`, verifica serie/origen, deduplica, registra errores e inserta la importación y las marcaciones dentro de una transacción SQLite.
3. `src/ControlMarcacion.Infrastructure/Marcaciones/LectorMarcadorAutomatico.cs` — `Leer` y `DetectarRespaldo`: delega al lector binario cuando `DetectorFormatoMarcador.Detectar` devuelve cabecera; de lo contrario delega al lector TXT.
4. `src/ControlMarcacion.Infrastructure/Marcaciones/DetectorFormatoMarcador.cs` — `Detectar(Stream)` y `Detectar(ReadOnlySpan<byte>)`: inspecciona hasta 512 bytes y valida dos cadenas con longitud, tamaño de registro, número de campos y descriptores. No usa nombre ni extensión.
5. `src/ControlMarcacion.Infrastructure/Marcaciones/LectorRespaldoZkteco.cs` — `Leer`, `DeducirDesplazamiento`, `Extraer` y `DecodificarTiempo`: deduce el desplazamiento, extrae campos y produce `ResultadoLectura`.
6. `src/ControlMarcacion.Application/Marcaciones/Dto/ImportacionDtos.cs` — `ResultadoLectura.TotalLineas`: suma marcaciones aceptadas y registros rechazados.
7. `ServicioImportacion.ColapsarDobleFichada`: ordena por ID y fecha, y colapsa marcas del mismo ID separadas por hasta 60 segundos.
8. `ServicioImportacion.ContarYaExistentesAsync`: descarta claves ya persistidas por `(EquipoId, IdMarcador, FechaHora)`.
9. `ServicioImportacion.ImportarAsync`: crea `MarcacionCruda` para cada marcación nueva.

El ensayo invocó directamente `MarcacionesController.Cargar` con `IFormFile`, contenedor real y SQLite temporal. Terminó en `RedirectToAction("Resultado")` y persistió los contadores descritos más adelante.

## Detección de formato

El archivo real fue detectado como `respaldo binario`:

- serie: `VGU6251600751`;
- modelo: `ZAM70-NF28VA-Ver3.0.15`;
- tamaño de registro: 28 bytes;
- campos declarados: `4, 4, 1, 1, 1, 1, 4`;
- comienzo del cuerpo: offset 63;
- cuerpo: 27 468 bytes = 981 registros completos, sin remanente.

La extensión no participa. Las pruebas existentes envían contenido binario y contenido TXT tabulado a `LectorMarcadorAutomatico` y confirman lectores diferentes aunque ambos puedan llamarse `.dat`.

## Layout binario observado

### Cabecera

| Elemento | Representación |
|---|---|
| Serie | longitud `byte` + ASCII |
| Modelo | longitud `byte` + ASCII |
| Tamaño de registro | `uint32`, little-endian en Windows |
| Número de campos | `byte` |
| Descriptor | tres bytes: tipo `0x69`, ancho, reservado |

El detector admite tamaños declarados entre 8 y 1024 bytes, hasta 64 campos y exige que la suma de anchos quepa en el registro. El lector solo admite el esquema cuando el último campo mide 4 bytes, porque lo interpreta como timestamp.

### Registro real de 28 bytes

El bloque declarado empieza en el desplazamiento 8 del registro:

| Offset relativo | Ancho | Lectura actual |
|---:|---:|---|
| 0–7 | 8 | relleno |
| 8–11 | 4 | ID `uint32` little-endian |
| 12–15 | 4 | campo no usado |
| 16 | 1 | modo de verificación |
| 17 | 1 | tipo |
| 18 | 1 | parte baja del código de evento, no usada |
| 19 | 1 | parte alta/reservada, no usada |
| 20–23 | 4 | timestamp ZKTeco `uint32` little-endian |
| 24–27 | 4 | relleno |

En este corpus existe una invariancia completa: los 851 registros con código de evento `3` tienen ID distinto de cero; los 126 con evento `27`, los 2 con `751` y los 2 con `752` tienen ID cero. Son 981/981 registros coherentes con el layout actual.

### Fecha y hora

El timestamp no es Unix. Se descompone en segundos, minutos, horas, día de 31 posiciones, mes y año desde 2000. El corpus va de `01/10/2026 03:26:09` a `06/10/2026 08:49:05` y está físicamente ordenado de forma cronológica.

Se agregó cobertura ejecutable para `00:00:00`, `23:59:59`, fin de mes y cambio de año. La codificación y decodificación conservan el segundo exacto.

### Identificador y ceros a la izquierda

`BKtransaction.dat` almacena el ID físicamente como un entero sin signo de 32 bits. No existe ancho textual ni carácter `0` inicial que pueda recuperarse. Por tanto:

- `0014` y `14` se almacenan como el mismo valor numérico 14;
- `0007` y `7` se almacenan como 7;
- el lector actual normaliza mediante `uint32.ToString(...)` y devuelve `"14"` o `"7"`;
- no se deben inventar ceros. Si son significativos, se debe preferir el `attlog` de texto.

## Heurística de layout

`DeducirDesplazamiento` calcula la holgura `TamanoRegistro - BytesDeclarados` y prueba cada desplazamiento posible. Puntúa cada candidato por cantidad de timestamps que:

- decodifican a una fecha real;
- tienen año igual o posterior a 2015;
- no superan `DateTime.Now + 1 año`.

Exige al menos 90 % de registros válidos. Primero prueba candidatos con el timestamp alineado a 4 bytes; si ninguno pasa, repite sin alineación. Gana el candidato con más fechas válidas. En empate conserva el primer desplazamiento, es decir, el menor. No existe rechazo específico por ambigüedad entre dos layouts con el mismo puntaje. Ese empate sigue siendo un riesgo de falso positivo en formatos desconocidos.

## Comparación contra la referencia

### Comparación literal del resultado aceptado

| Métrica | Resultado |
|---|---:|
| Filas de referencia | 980 |
| Marcaciones aceptadas por el lector | 851 |
| Coincidencias exactas `ID + FechaHora` | 11 |
| Faltantes respecto de la referencia | 969 |
| Sobrantes del lector | 840 |

Estos números literales no prueban que el lector esté desplazado: la referencia incluye 130 registros de sistema con ID 0 que el lector rechaza y, además, empareja el ID de la fila siguiente.

### Diagnóstico byte a byte de la referencia

- Las 980 fechas de la referencia son exactamente los timestamps físicos 1–980 del DAT: 980/980 iguales, tolerancia 0 segundos.
- El ID de la fila de referencia `N` está físicamente en el registro `N+1`.
- Al mover ese ID una fila hacia atrás, las 979 filas interiores comparables coinciden: 979/979 en ID y fecha.
- El ID físico inicial, `141`, no aparece asociado a la primera fecha en la referencia.
- El timestamp físico final, `06/10/2026 08:49:05`, no aparece en la referencia; su ID `147` quedó asociado al timestamp anterior.

Ejemplo del borde inicial:

| Fuente | ID | FechaHora |
|---|---:|---|
| Registro físico 1 / lector | 141 | 2026-10-01 03:26:09 |
| CSV fila 1 | 85, tomado del registro físico 2 | 2026-10-01 03:26:09 |

La referencia independiente tiene un defecto de fase de una fila en el ID. No se la reemplazó por una salida fabricada desde el lector; el defecto se demostró ubicando cada campo en los bytes originales.

## Registros, orden y duplicados

| Nivel | Conteo |
|---|---:|
| Registros físicos según cabecera | 981 |
| Salida aceptada del lector | 851 |
| Rechazados por ID 0/evento de sistema | 130 |
| Duplicados/segundas fichadas dentro de 60 s | 11 |
| `MarcacionCruda` persistidas | 840 |

No hay claves físicas exactas duplicadas en el CSV independiente. Los 11 descartes del servicio son dobles fichadas dentro de la ventana configurada, no una pérdida durante el descifrado. El lector conserva el orden físico; `ColapsarDobleFichada` agrupa por ID y ordena cronológicamente, por lo que el orden de persistencia puede diferir deliberadamente del archivo.

## Archivo truncado

Se creó una copia con un byte menos, 27 530 bytes. El lector calculó 980 registros completos por división entera, omitió los 27 bytes restantes y devolvió 980 líneas sin un error específico de truncamiento. Se perdieron la última marcación y el fragmento final sin aviso.

Resultado: **BKDAT-03 incumplido**.

## Registro corrupto

En una copia se reemplazaron por `FF FF FF FF` los cuatro bytes del timestamp del registro 101. El lector mantuvo 851 aceptadas y 130 rechazadas. El registro alterado se aceptó así:

```text
ID: 80
FechaHora: 2133-08-18 06:28:15
Estado: aceptado
```

La heurística global siguió eligiendo el layout correcto porque más del 90 % de fechas era plausible, pero `Extraer` solo rechazó fechas calendariamente imposibles y no reutilizó el límite futuro de la heurística.

Resultado: **BKDAT-04 incumplido**.

## Flujo MARCACIONES sobre SQLite temporal

El archivo se ejecutó mediante `MarcacionesController.Cargar`. Resultado del controlador: redirección a `Resultado`.

| Contador | Valor |
|---|---:|
| Leídas | 981 |
| Aceptadas por lector | 851 |
| Rechazadas | 130 |
| Duplicadas/colapsadas | 11 |
| Persistidas | 840 |
| `ImportacionMarcador` | 1 |
| `ErrorImportacion` | 257 |

Los 257 errores persistidos son 130 rechazos físicos más 127 resúmenes de IDs sin asignación en la base temporal. La falta de trabajador no altera el juicio sobre el descifrado.

## Contratos BKDAT

| Contrato | Estado | Evidencia |
|---|---|---|
| BKDAT-01 — binario a `LectorRespaldoZkteco` | CUMPLE | Cabecera real detectada; pruebas de reparto binario/TXT por contenido. |
| BKDAT-02 — semántica de ID | CUMPLE / LIMITACIÓN DOCUMENTADA | `uint32`; ceros iniciales no existen físicamente y no son recuperables. |
| BKDAT-03 — truncado con error explícito | NO CUMPLE | Un byte menos elimina silenciosamente el registro parcial. |
| BKDAT-04 — corrupción identificable | NO CUMPLE | `FFFFFFFF` se acepta como fecha de 2133. |
| BKDAT-05 — fecha/hora exacta | CUMPLE | 980/980 timestamps de referencia exactos; fixtures de límites al segundo. |
| BKDAT-06 — comparación independiente | PARCIAL | La referencia tiene ID desplazado; realineadas, 979/979 filas interiores exactas. Falta una segunda referencia externa sin ese defecto. |
| BKDAT-07 — flujo completo MARCACIONES | CUMPLE | Controlador real + SQLite temporal: 981/851/130/11/840. |

## Conclusión y siguiente paso

El layout y la fecha/hora del lector están respaldados por la cabecera, los bytes y las invariantes del corpus. La referencia aportada no puede certificar literalmente el emparejamiento ID-fecha porque su propio algoritmo desplaza el ID una fila. Aun así, la realineación demuestra que ambos decodifican los mismos campos físicos.

La respuesta global es **PARCIAL** por BKDAT-03, BKDAT-04 y por la referencia defectuosa. El siguiente paso debe ser una intervención separada y mínima: rechazar cualquier remanente del cuerpo, aplicar a cada registro el mismo máximo futuro usado por la heurística, crear pruebas de regresión para ambos defectos y validar de nuevo contra un `attlog` o exportación del mismo reloj y periodo.

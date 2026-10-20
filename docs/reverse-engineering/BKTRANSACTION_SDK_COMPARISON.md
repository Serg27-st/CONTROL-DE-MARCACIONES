# BKtransaction — comparación lector interno vs PULL SDK

Fecha: 2026-10-13.

## Corpus

```text
Archivo: BKtransaction(5).dat
SHA-256: 307A91B5AE252F2D3FCB9A0A33A5521981835C9D0291F250AC78C0EA558C97FB
Tamaño: 27531 bytes
Serie: VGU6251600751
Modelo/firmware: ZAM70-NF28VA-Ver3.0.15
Registros estructurales: 981
```

El archivo se leyó tres veces en memoria: `Interno`, `Sdk` y `Comparar`. No se escribió en
base de datos.

## Resultado

| Métrica | Interno | SDK 2.2.0.220 |
|---|---:|---:|
| Registros estructurales | 981 | 981 |
| Registros normalizados válidos | 851 | 0 |
| IDs únicos válidos | 127 | 0 |
| Fecha mínima válida | 2026-10-01 03:26:09 | N/A |
| Fecha máxima válida | 2026-10-06 08:49:05 | N/A |
| Rechazados | 130 | 981 |

```text
Coincidencias exactas: 0
Solo interno: 851
Solo SDK: 0
Diferencias: 851
```

Los 130 rechazos internos corresponden a registros físicos sin identidad laboral útil
(incluidos eventos de sistema/ID cero); se conservan como rechazo y no se hardcodean.

## Hallazgo SDK

`ProcessBackupDataExt` reconoce la cabecera y devuelve una tabla `transaction` con 981
filas. Sin embargo, para este archivo los campos `Cardno`, `Pin` y `Time_second` no
representan marcaciones válidas. El adaptador rechaza esas filas en lugar de inventar IDs o
fechas. `ProcessBackupData` y la edición 2.2.1.4 dieron el mismo resultado.

En consecuencia:

```text
¿Puede abrir y procesar offline BKtransaction.dat? SÍ
¿Interpreta correctamente este BKtransaction.dat? NO
```

## Ceros a la izquierda

El esquema SDK separa `Pin` y `Cardno`; el adaptador prioriza `Pin` y lo conserva como
texto, de modo que un valor emitido como `0014` no se normaliza a `14`. En este corpus el
SDK no devuelve un identificador útil, por lo que no aporta evidencia de recuperación de
ceros físicos.

En el formato binario, el lector interno obtiene el identificador desde un entero físico.
Si el archivo solo almacenó el valor numérico, los ceros a la izquierda no son
recuperables. No se intenta reconstruirlos artificialmente.

## Evidencia generada

Directorio: `artifacts/bktransaction-validation/`.

```text
307A...97FB-interno.csv
307A...97FB-sdk.csv
307A...97FB-diff.csv
307A...97FB-summary.json
```

El CSV SDK contiene solo su encabezado porque no hubo filas normalizables. El diff marca
851 filas `SoloInterno`. Los archivos contienen únicamente los campos necesarios para la
comparación y no contienen credenciales.

## Decisión

Fuente autoritativa: `Interno`. El modo `Comparar` es diagnóstico y nunca mezcla resultados.
El SDK no debe agregarse al instalador ni reemplazar el lector actual.


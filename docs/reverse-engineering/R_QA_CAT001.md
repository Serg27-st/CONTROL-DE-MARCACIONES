# R-QA-CAT-001 — Identificador devuelto al crear catálogos

Fecha: 2026-10-28.

Estado: **CERRADO**.

## Alcance

Remediación exclusiva del defecto `QA-CAT-001` detectado por `QA-E2E-002`. No se reejecutó
el piloto Hotel Equo y no se modificaron sus artefactos históricos.

## Reproducción

Se agregó primero el contrato `QACAT-001` y se ejecutó contra SQLite real antes de cambiar
el servicio. El alta persistió una planta válida, pero la prueba falló porque el identificador
devuelto fue `0`. La expectativa no fijó el identificador de la base: comparó el valor devuelto
con la clave realmente persistida y exigió que fuera mayor que cero.

Resultado previo:

- ID devuelto: `0`.
- Entidad persistida: sí, con una clave generada por SQLite.
- Contrato: 0/1 verde; fallo reproducido en la aserción de ID positivo.

## Causa raíz

`ServicioCatalogos.GuardarAsync` pedía a cada auxiliar un `long`. En un alta, el auxiliar
construía la entidad, ejecutaba `AddAsync` y devolvía inmediatamente `entidad.Id`. En ese punto
SQLite todavía no había ejecutado la inserción, por lo que el valor era `0`.

El llamador guardaba ese cero por valor en una variable local, después ejecutaba el único
`SaveChangesAsync` y finalmente devolvía la copia anterior. EF Core sí materializaba la clave
en la entidad seguida, pero ya no podía modificar el `long` capturado.

Orden anterior:

1. Construir entidad.
2. `AddAsync`.
3. Copiar `entidad.Id` todavía igual a cero.
4. `SaveChangesAsync`; SQLite genera y EF materializa la clave en la entidad.
5. Devolver la copia cero.

## Corrección

Los cinco auxiliares admitidos por el servicio ahora devuelven la `EntidadBase` seguida por EF.
`GuardarAsync` conserva el mismo y único `SaveChangesAsync` y lee `entidad.Id` después de que
el guardado terminó correctamente.

No se agregó otro guardado, no se consulta por código o nombre y no se alteraron transacción,
validaciones, unicidad, autorización, auditoría ni actualizaciones. Si la validación o el
guardado fallan, la excepción se propaga y no se devuelve ningún identificador.

## Catálogos revisados

| Catálogo | Estado | Evidencia |
|---|---|---|
| Planta | AFECTADO | Reproducción directa `QACAT-001` |
| Puesto | AFECTADO | Mismo retorno previo al guardado; variante `QACAT-002` |
| Área | AFECTADO | Mismo retorno previo al guardado; variante `QACAT-002` |
| Sección | AFECTADO | Mismo retorno previo al guardado; variante `QACAT-002` |
| Tipo de documento | AFECTADO | Mismo retorno previo al guardado; variante `QACAT-002` |
| Empresa | NO APLICA | Solo se ofrece como padre de planta; no se guarda por `ServicioCatalogos` |
| Centro | NO APLICA | No está admitido por `CatalogosDisponibles` ni por el despacho del servicio |
| Centro de costo | NO APLICA | No está admitido por `CatalogosDisponibles` ni por el despacho del servicio |
| Turnos, periodos, ausencias, equipos y marcadores | NO APLICA | Tienen servicios y reglas propios; no se modificaron |

## Contratos

Se agregaron seis contratos lógicos con nueve ejecuciones SQLite:

- `QACAT-001`: crear planta devuelve la clave persistida y distinta de cero.
- `QACAT-002`: puesto, área, sección y tipo de documento devuelven su clave persistida.
- `QACAT-003`: actualizar conserva la clave y no crea otra fila.
- `QACAT-004`: una validación duplicada falla sin devolver un ID de éxito ni insertar otra fila.
- `QACAT-005`: la corrección no genera duplicados.
- `QACAT-006`: el alta conserva actor, clave y operación de auditoría.

Resultado de contratos: 9/9 ejecuciones verdes.

Regresión dirigida de `ServicioCatalogos`, personal y creación automática de puestos:
112/112 verdes, 0 fallos y 0 omitidos.

## Validación final

- Build Release: 0 errores, 0 advertencias.
- Suite Domain: 442/442.
- Suite Infrastructure: 554/554.
- Suite Web: 41/41.
- Suite completa: 1,037/1,037, 0 fallos y 0 omitidos.
- Diferencia frente a baseline 1,028: +9 ejecuciones, todas verdes.

## Archivos

Productivo modificado:

- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCatalogos.cs`.

Prueba agregada:

- `tests/ControlMarcacion.Infrastructure.Tests/Catalogos/ServicioCatalogosQACAT001Tests.cs`.

Documentación:

- `docs/reverse-engineering/R_QA_CAT001.md`.
- Estado reconciliado en `PROJECT_STATE.md` y `REMEDIATION_PLAN.md`.

No se creó migración. Producción no fue tocada. OP-03 no se ejecutó. No se creó commit.

## Resultado formal

`QA-CAT-001 = CERRADO`.

El informe `artifacts/QA-E2E-HOTEL-EQUO/00_Informe_Final_QA.md` permanece como evidencia
histórica del fallo observado y no fue reescrito.

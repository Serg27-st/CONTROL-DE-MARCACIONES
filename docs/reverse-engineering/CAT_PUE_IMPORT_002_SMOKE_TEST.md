# CAT-PUE-IMPORT-002 — Smoke test funcional de importación de puestos

## Estado

APROBADO. Los siete casos fueron ejecutados con evidencia de interfaz y consultas de solo
lectura sobre la base SQLite aislada.

## Ambiente preparado

- Instancia local de desarrollo: `http://127.0.0.1:5102`.
- Base aislada: `artifacts/CAT-PUE-IMPORT-002/ControlMarcacion-smoke.db`.
- Entorno: `Development`; inicio sin perfil de lanzamiento y con URL explícita de prueba.
- Migraciones existentes: base aislada actualizada; no se creó ninguna migración nueva.
- Producción y `C:\ProgramData\ControlMarcacion`: no tocadas.
- Empresa de prueba sembrada: `CONSULTING ADVISERS S.A.C` (`empresa.id = 1`).
- Planta de prueba sembrada: `PLANTA DETERGENTES LURÍN` (`planta.id = 1`, `empresa_id = 1`).

## Archivos

- `artifacts/CAT-PUE-IMPORT-002/CAT_PUE_IMPORT_002_PERSONAL.csv`
- `artifacts/CAT-PUE-IMPORT-002/CAT_PUE_IMPORT_002_CANCELACION.csv`

## Casos preparados

| Caso | Documento | Puesto | Resultado esperado |
|---|---:|---|---|
| SMK-01 | 99002001 | OPERARIO | Reutiliza el puesto universal existente. |
| SMK-02 | 99002002 | CONSERJE DE PRUEBA CAT002 | Propone y crea un puesto de empresa. |
| SMK-03 | 99002003 | variante con mayúsculas/espacios | Reutiliza el mismo puesto de SMK-02. |
| SMK-04 | 99002004 | ANFITRIÓN DE PRUEBA CAT002 | Propone y crea otro puesto de empresa. |
| SMK-05 | 99002005 | vacío | Rechaza la fila; no crea puesto huérfano. |
| SMK-06 | 99002001–05 | segunda importación | No duplica puestos ni trabajadores. |
| SMK-07 | 99002006 | PORTERO DE PRUEBA CANCELAR CAT002 | Previsualiza y cancela; no persiste. |

## Evidencia inicial

Registrada antes de abrir el flujo de carga:

- `puesto`: 37 registros.
- `trabajador`: 0 registros.
- `asp_net_users`: 0 registros.
- `OPERARIO`: existe como puesto universal activo (`id = 1`, `empresa_id = NULL`).
- `CONSERJE DE PRUEBA CAT002`: ausente.
- `ANFITRIÓN DE PRUEBA CAT002`: ausente.
- `PORTERO DE PRUEBA CANCELAR CAT002`: ausente.
- Documentos `99002001` a `99002006`: ausentes.
- `GET /vivo`: HTTP 200, `ControlMarcaciones-OK`.
- `GET /estado`: HTTP 503, `CONFIGURACION_INICIAL_REQUERIDA`, resultado esperado porque
  la base aislada todavía no tiene usuario inicial.
- Usuario temporal de prueba: creado manualmente en `/setup`; su contraseña no se registró
  en esta evidencia.

Tras la configuración manual:

- `asp_net_users`: 1 usuario activo con rol `COORDINADOR`.
- `GET /estado`: HTTP 200, `ControlMarcaciones-OK;LISTO`.

## Previsualización principal

Evidencia de interfaz:

- Archivo: `CAT_PUE_IMPORT_002_PERSONAL.csv`.
- Filas leídas: 5.
- Filas que pasarían: 4.
- Filas rechazadas: 1.
- Puestos diferentes: 3.
- Puestos existentes: 1.
- Puestos nuevos propuestos: 2.
- Puestos ambiguos: 0.
- `ANFITRIÓN DE PRUEBA CAT002`: nuevo, 1 trabajador.
- `CONSERJE DE PRUEBA CAT002`: nuevo, 2 trabajadores; la variante de mayúsculas y espacios
  fue normalizada al mismo puesto.
- Fila 6, documento `99002005`: rechazada por falta del campo `puesto`.
- La interfaz declaró expresamente: `Validación: no se guardó nada`.

Verificación SQLite inmediatamente después de la previsualización:

- `puesto`: permanece en 37 registros.
- `trabajador`: permanece en 0 registros.
- Ninguno de los tres puestos ficticios existe todavía.
- Ninguno de los documentos `99002001` a `99002006` existe todavía.

Captura: `artifacts/CAT-PUE-IMPORT-002/01-previsualizacion-principal.png`.

## Confirmación y verificación posterior

Evidencia de interfaz tras confirmar:

- Filas registradas: 4 de 5.
- Filas rechazadas: 1.
- Motivo del rechazo: fila 6, documento `99002005`, falta el campo `puesto`.
- Puestos detectados: 3; 1 existente, 2 nuevos propuestos y 0 ambiguos.

Evidencia SQLite de solo lectura:

- `puesto`: 39 registros, incremento exacto de 2.
- `trabajador`: 4 registros, incremento exacto de 4.
- `OPERARIO` universal (`id = 1`) fue reutilizado por `99002001`.
- `CONSERJE DE PRUEBA CAT002` fue creado una sola vez para la empresa 1 y vinculado a
  `99002002` y `99002003`.
- `ANFITRIÓN DE PRUEBA CAT002` fue creado una sola vez para la empresa 1 y vinculado a
  `99002004`.
- Los cuatro trabajadores quedaron en `CONSULTING ADVISERS S.A.C` y
  `PLANTA DETERGENTES LURÍN`.
- `99002005` no existe.
- No hay nombres CAT002 duplicados por empresa.
- No hay puestos CAT002 creados sin trabajadores.
- Auditoría: 2 inserciones en `puesto`, 4 en `trabajador`, 4 en `periodo_laboral` y 4 en
  `asignacion_planta`.

Captura: `artifacts/CAT-PUE-IMPORT-002/02-confirmacion-principal.png`.

## Segunda importación

Se reutilizó la plantilla con `Actualizar los trabajadores que ya existan` desmarcado:

- Filas leídas: 5.
- Filas que pasarían: 0.
- Filas rechazadas: 5.
- Documentos `99002001` a `99002004`: rechazados por trabajador ya registrado.
- Documento `99002005`: rechazado nuevamente por puesto vacío.
- Puestos diferentes: 3; los 3 existentes, 0 nuevos y 0 ambiguos.
- La interfaz no ofreció el botón `Confirmar importación`.
- SQLite permaneció en 39 puestos y 4 trabajadores.
- Cada puesto CAT002 existe una sola vez y cada documento importado existe una sola vez.

Captura: `artifacts/CAT-PUE-IMPORT-002/03-reimportacion-segura.png`.

## Cancelación

Se analizó `CAT_PUE_IMPORT_002_CANCELACION.csv` sin confirmar:

- Filas leídas y válidas: 1.
- Puestos: 0 existentes, 1 nuevo propuesto y 0 ambiguos.
- Puesto propuesto: `PORTERO DE PRUEBA CANCELAR CAT002`.
- Antes de cancelar, SQLite confirmó 0 puestos con ese nombre y 0 trabajadores con documento
  `99002006`.
- El usuario pulsó `Cancelar` y la interfaz informó: `La importación fue cancelada. No se
  guardó ningún cambio.`
- Después de cancelar: 0 puestos, 0 trabajadores y 0 registros de auditoría asociados a la
  propuesta; los totales permanecieron en 39 puestos y 4 trabajadores.

Capturas:

- `artifacts/CAT-PUE-IMPORT-002/04-previsualizacion-cancelacion.png`.
- `artifacts/CAT-PUE-IMPORT-002/05-cancelacion-confirmada.png`.

## Consulta posterior en la interfaz

El catálogo de puestos mostró:

- `ANFITRIÓN DE PRUEBA CAT002`, activo, 1 registro en uso.
- `CONSERJE DE PRUEBA CAT002`, activo, 2 registros en uso.
- `OPERARIO`, activo, 1 registro en uso.

El listado de personal mostró los cuatro trabajadores importados con la empresa, planta y
puesto esperados. La ficha del documento `99002002` confirmó:

- Empresa: `CONSULTING ADVISERS S.A.C`.
- Planta: `PLANTA DETERGENTES LURÍN`.
- Puesto: `CONSERJE DE PRUEBA CAT002`.
- Fecha de ingreso: `01/10/2026`.
- Situación: vigente.

Capturas:

- `artifacts/CAT-PUE-IMPORT-002/06-catalogo-puestos.png`.
- `artifacts/CAT-PUE-IMPORT-002/07-listado-trabajadores.png`.
- `artifacts/CAT-PUE-IMPORT-002/08-ficha-trabajador.png`.

## Integridad final

- `PRAGMA integrity_check`: `ok`.
- Violaciones de claves foráneas: 0.
- Documentos duplicados: 0.
- Puestos CAT002 duplicados por empresa y nombre normalizado: 0.
- Puestos CAT002 huérfanos: 0.
- Puesto de la prueba cancelada presente: 0.
- Trabajador de la prueba cancelada presente: 0.
- Auditoría final: 2 inserciones de puesto, 4 de trabajador, 4 de periodo laboral y 4 de
  asignación de planta.

## Resultados

| Caso | Estado | Evidencia |
|---|---|---|
| SMK-01 | PASS | Reutilizó `OPERARIO` universal, `puesto_id = 1`. |
| SMK-02 | PASS | Creó una vez `CONSERJE DE PRUEBA CAT002` en empresa 1. |
| SMK-03 | PASS | Variante normalizada y vinculada al mismo `puesto_id` de SMK-02. |
| SMK-04 | PASS | Creó una vez `ANFITRIÓN DE PRUEBA CAT002` en empresa 1. |
| SMK-05 | PASS | Fila 6 rechazada; no creó trabajador ni puesto huérfano. |
| SMK-06 | PASS | Reimportación rechazada de forma segura; 0 duplicados y 0 cambios. |
| SMK-07 | PASS | Previsualización cancelada; 0 persistencia y 0 auditoría. |

## Resultado formal

```text
=== CAT-PUE-IMPORT-002 — RESULTADO ===

Ambiente utilizado:
Instancia local Development en 127.0.0.1:5102 con SQLite aislada

Plantilla generada:
CAT_PUE_IMPORT_002_PERSONAL.csv
CAT_PUE_IMPORT_002_CANCELACION.csv

Total de trabajadores:
5 filas principales; 4 importadas y 1 rechazada

Puestos existentes:
1 en la primera previsualización; 3 en la reimportación

Puestos nuevos propuestos:
2 confirmados y 1 cancelado

Previsualización:
PASS

Creación de puestos:
PASS

Vinculación al trabajador:
PASS

Deduplicación:
PASS

Reimportación:
PASS

Cancelación:
PASS

Auditoría:
PASS

Casos ejecutados:
7

Casos aprobados:
7

Casos fallidos:
0

Observaciones:
La fila sin puesto fue rechazada sin generar trabajador ni puesto huérfano. La variante de
mayúsculas y espacios reutilizó el mismo puesto. La segunda importación no ofreció confirmación
porque todas las filas fueron rechazadas conforme a la política vigente.

Cambios productivos:
NINGUNO

Migraciones:
NINGUNA; no se creó ni modificó ninguna migración. La base aislada se inicializó con el esquema vigente

Producción:
NO TOCADA

Commit:
NO

CAT-PUE-IMPORT-002:
APROBADO

Siguiente paso recomendado:
Conservar este expediente como evidencia y continuar con el siguiente ítem priorizado del backlog.
```

# CAT-DYN-004 — Importación inteligente de turnos y descansos

Fecha: 2026-11-02.

## Estado

Implementación terminada sobre el modelo vigente, sin migración y sin modificar el motor
económico. Contratos dirigidos: 11/11. Suite completa: 1.113/1.113, 0 fallos y 0 omitidos.
Build Release: 0 errores y 0 advertencias. La prueba usa SQLite real y Excel `.xlsx`.

## Modelo reutilizado

- `AsignacionTurno` conserva la vigencia por trabajador.
- `Cuadrilla` sirve como contenedor técnico individual para una secuencia finita declarada.
- `ProgramacionCuadrilla` delimita el intervalo autorizado.
- `TramoRotacion` representa cada fecha como `Trabajo` o `Descanso`.
- `Turno` y `VersionTurno` conservan definición e historia del horario.
- `PeriodoLaboral` y `AsignacionPlanta` resuelven empresa y unidad en la fecha importada.

No existe repetición automática. Cada fecha procede del Excel autorizado. La jornada nocturna
pertenece al día en que empieza.

## Flujo

1. Descargar la plantilla desde `Programación laboral → Importar turnos y descansos`.
2. Cargar un `.xlsx` de hasta 5 MB.
3. Expandir intervalos finitos y resolver trabajador, vínculo, empresa y unidad por fecha.
4. Conciliar el código de turno. Un código existente solo se reutiliza si su definición es
   compatible. Un turno nuevo exige nombre, inicio, fin y refrigerio.
5. Mostrar calendario por trabajador y fecha, incluidos descansos, nocturnos y conflictos.
6. Conservar el archivo en memoria durante 20 minutos con un token opaco de un solo uso.
7. Releer al confirmar y comparar la huella con turnos, asignaciones y periodos actuales.
8. Guardar en una transacción y reevaluar `IValidadorPreparacionTareo`.

## Invariantes

- Fecha específica e intervalo son alternativas.
- No se crean rangos indefinidos.
- Un descanso no declara turno ni se infiere por falta de marcaciones.
- Debe existir un único vínculo y una única unidad histórica en la fecha.
- El alcance vacío y la unidad ajena bloquean la operación.
- Un periodo cerrado bloquea la fecha.
- Una programación previa diferente genera conflicto y no se reemplaza silenciosamente.
- Una programación idéntica es idempotente.
- `MarcacionCruda`, `TareoDiario` y PMV no participan.

## Concurrencia y auditoría

La confirmación se serializa en el proceso, repite la conciliación dentro de la transacción y
rechaza una huella obsoleta. Los índices y restricciones SQLite siguen siendo la defensa final.
Las entidades llevan `CreadoPor`; el interceptor transversal genera la auditoría en Web.

## Evidencia

- Contratos: `ImportadorProgramacionLaboralTests`.
- Libro integral: `artifacts/CAT-DYN-004/programacion-integral.xlsx`.
- Render verificado: `artifacts/CAT-DYN-004/programacion-integral.png`.
- CATTURNN-01..30 agrupados en 10 pruebas SQLite y una secuencia integral con cinco trabajadores.

## Límites explícitos

- Se lee la primera hoja del libro.
- El historial se consulta mediante la auditoría transversal; no se añadió otra tabla.
- La referencia de aprobación se conserva en el archivo y no equivale a aprobación automática.
- No se modificaron FER-005, ASIS-001, REP-004, reportes oficiales ni PMV.

Producción no tocada. OP-03 no ejecutado. Sin commit.

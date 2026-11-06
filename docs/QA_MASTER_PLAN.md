# Plan maestro QA — Control de Marcaciones

## QA-E2E-007 — Validación exhaustiva del tareo

Estado: **EJECUTADO — FAIL por QA-E2E-007-DEF-01**  
Población: exactamente 5 trabajadores ficticios  
Ambiente: SQLite temporal, aplicación local y Chrome/Playwright

### Objetivo

Detectar errores de cálculo, atribución de marcaciones, turnos, descansos, incidencias, regularizaciones y reportes mediante el flujo real desde Excel y DAT hasta el tareo y sus exportaciones.

### Cobertura obligatoria

Jornada normal, tardanzas, salida anticipada, HE25/HE35, nocturnos, cruces de medianoche, sábados, domingos, descansos móviles, feriados, faltas, marcaciones incompletas, cambios de turno y regularizaciones.

### Criterios de aceptación

- Matriz trabajador-fecha con programación, marcas, oráculo independiente y resultado real.
- Comparación por minuto para ordinarios, extras, nocturnos, tardanzas, salida anticipada, ausencias e incidencias.
- Solo reglas económicas aprobadas; las no confirmadas quedan BLOQUEADAS.
- Flujo real de importación Excel/DAT, cálculo y reportes mediante Chrome/Playwright.
- Seguridad y aislamiento por unidad verificados en interfaz y servidor.
- Reportes conciliados con SQLite y matriz independiente.
- Auditoría, `integrity_check` y `foreign_key_check` satisfactorios.
- Cualquier diferencia reproducible se registra como defecto antes de proponer corrección.

### Riesgos

- Atribución incorrecta en cruces de medianoche o marcas reclamadas.
- Doble conteo físico/económico en domingos, descansos o feriados.
- Aplicación errónea de HE25/HE35/HE100 y nocturnidad.
- Regularizaciones que alteren marcaciones o jornadas no afectadas.
- Filtración de datos entre unidades o reportes no conciliados.

### Restricciones

Sin producción, OP-03, despliegue, commit, cambios productivos, migraciones ni reglas nuevas durante QA.

### Resultado de ejecución — 04/11/2026

- Matriz: 70 filas; 68 PASS y 2 FAIL.
- Pruebas dirigidas: 106/106 PASS.
- Chrome/Playwright: flujo completo ejecutado; aserción final FAIL por cruce `NOC-QA` → `DIU-QA`.
- Seguridad por unidad, integridad SQLite, auditoría y conciliación de reportes: PASS.
- Evidencia: `artifacts/QA-E2E-007/`.

# AI_CHECKPOINT — Control de Marcaciones

Actualizado: 2026-11-04  
Fuente: únicamente informes finales recientes de CAT-DYN-004, QA-E2E-004-A, R-QA-E2E-004-A, QA-E2E-005 y R-QA-E2E-005.

## Estado ejecutivo

- Build Release validado: 0 errores / 0 warnings.
- Baseline vigente: **1,142 / 1,142 pruebas verdes**.
- Producción: no tocada.
- OP-03: no ejecutado.
- Despliegue y commit: no realizados.

## Arquitectura vigente

- ASP.NET Core MVC organizado en capas Domain, Application, Infrastructure y Web.
- EF Core con SQLite; migraciones y restricciones físicas protegen integridad e invariantes.
- Servicios de aplicación concentran cálculo, solicitudes, justificaciones, importación y reportes.
- Autenticación y autorización por roles con alcance histórico por unidad/planta.
- Tareo conserva marcaciones crudas como evidencia y recalcula resultados derivados.
- Chrome/Playwright y bases SQLite temporales sostienen la aceptación funcional real.

## Funcionalidades validadas

- Importación dinámica de personal, catálogos, turnos y descansos desde Excel.
- Interpretación correcta de fechas Excel y programación laboral por vigencia.
- Piloto de cinco trabajadores: importación biométrica, 25 jornadas, cálculo y reportes oficiales.
- Reglas económicas explícitas y exportación de excepciones conciliada.
- Regularizaciones de ingreso/salida sin alterar marcaciones originales.
- Justificaciones manuales con estado solicitado, aprobación explícita y segregación de funciones.
- Cambio puntual de turno con motivo, vigencias, historial, autorización por unidad y recálculo localizado.
- Rechazo, prevención de doble resolución y protección contra acceso a otra unidad.
- Integridad final: `integrity_check = ok` y 0 errores de claves foráneas.

## Defectos pendientes

- Ningún defecto abierto en los cierres finales consultados.
- Las tres brechas de QA-E2E-005 quedaron cerradas por R-QA-E2E-005.
- Los defectos de cálculo económico y exportación de QA-E2E-004-A quedaron cerrados por regresión.
- Cualquier limitación ajena a estos informes debe tratarse como no evaluada, no como aprobada.

## Siguiente fase — QA-E2E-006

- Ejecutar una regresión integral nueva sobre SQLite aislada y datos ficticios.
- Integrar carga, programación, marcaciones, regularizaciones, recálculo, seguridad por unidad y reportes.
- Verificar aislamiento PMV, vigencias históricas, auditoría, idempotencia e integridad física.
- Registrar evidencia real de navegador y defectos reproducibles sin corregirlos dentro de la fase QA.
- Mantener producción, OP-03, despliegue y commit fuera de alcance salvo autorización expresa.

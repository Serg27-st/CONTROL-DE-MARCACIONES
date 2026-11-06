# R-QA-E2E-004-A — Remediación de cálculo y exportación

Estado al 3 de noviembre de 2026:

```text
R-E2E-HE-001  = CERRADO
R-E2E-EXP-001 = CERRADO
QA-E2E-004-A  = APROBADO POR REGRESIÓN
```

La evidencia fallida original se conserva en `artifacts/QA-E2E-004-A/`. La remediación y su nueva ejecución están en `artifacts/R-QA-E2E-004-A/`.

Resumen:

- El tope `Math.Min(480, DuracionEfectiva)` en turnos creados por importación no era una regla empresarial. Se reemplazó por la duración efectiva declarada y se persiste en `Turno` y `VersionTurno`.
- CATREADY exige versión económica vigente mediante `CATREADY-11`.
- El recálculo no usa la configuración actual como fallback histórico cuando falta la versión.
- El reporte de excepciones ya no incrementa dos veces la columna después de Fecha.
- Build Release: 0 errores, 0 advertencias.
- Suite: 1,140/1,140 verde.
- Chrome/Playwright: 1/1 recorrido y 14/14 evidencias PASS.
- Piloto: 47 marcaciones, 25 tareos, 14,296 ordinarios, 23 HE25 y 0 HE35.
- Sin migración, despliegue, OP-03 ni cambios en producción.

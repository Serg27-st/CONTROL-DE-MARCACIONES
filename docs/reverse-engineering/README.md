# Ingeniería inversa — índice operativo

Catálogo autoritativo vigente: [`BUSINESS_RULES_FINAL.md`](BUSINESS_RULES_FINAL.md).
`BUSINESS_RULES.md` queda como corte histórico de Fase 6.1 y conserva sus IDs legados.

La vista consolidada de Fase 6 sustituye los informes de fase como contexto cotidiano. Las Fases 1–5 se preservan para trazabilidad.

## EMPIEZA AQUÍ

- [PROJECT_STATE.md](PROJECT_STATE.md): estado compacto para iniciar una sesión.

## ENTENDER EL SISTEMA

- [MASTER_DOSSIER.md](MASTER_DOSSIER.md): explicación canónica en 28 secciones.

## ARQUITECTURA

- [ARCHITECTURE.md](ARCHITECTURE.md): arquitectura estable, límites y decisiones.

## REGLAS

- [ACCEPTANCE_RULES.md](ACCEPTANCE_RULES.md): negocio vigente y contratos verificables.
- [BUSINESS_RULES_FINAL.md](BUSINESS_RULES_FINAL.md): catálogo autoritativo vigente.
- [BUSINESS_RULES.md](BUSINESS_RULES.md): corte histórico Fase 6.1 e IDs legados.

## BASE DE DATOS

- [DATABASE.md](DATABASE.md): esquema físico SQLite e integridad observada.

## DEUDA / BUGS

- [TECHNICAL_DEBT.md](TECHNICAL_DEBT.md): categorías separadas y prioridades.
- [REMEDIATION_PLAN.md](REMEDIATION_PLAN.md): orden, aceptación y reversibilidad.

## CONTEXTO PARA IA

- [AI_CONTEXT_MAP.md](AI_CONTEXT_MAP.md): ruta mínima por tipo de tarea.
- [CONTEXT_REGISTER.md](CONTEXT_REGISTER.md): evolución de comprensión y jerarquía documental.

## MÓDULOS

- [Personal](modules/PERSONNEL.md)
- [Marcaciones](modules/MARKINGS.md)
- [Turnos y rotaciones](modules/SHIFTS_ROTATIONS.md)
- [Asistencia y tareo](modules/ATTENDANCE_TAREO.md)
- [Solicitudes y justificaciones](modules/REQUESTS_JUSTIFICATIONS.md)
- [Reportes](modules/REPORTS.md)
- [Seguridad](modules/SECURITY.md)
- [Marcaciones provisionales PMV](modules/PROVISIONAL_MARKINGS.md)

## DIAGRAMAS

Los Mermaid D01–D14 de [diagrams/](diagrams/) son la fuente visual primaria. [CONTROL_MARCACION_ARCHITECTURE.drawio](diagrams/CONTROL_MARCACION_ARCHITECTURE.drawio) es una vista editable adicional y no los sustituye.

## HISTORIA DE INGENIERÍA INVERSA

- `PHASE_01_DISCOVERY.md`
- `PHASE_02_DOMAIN_ARCHITECTURE.md`
- `PHASE_03A_PERSISTENCE_SCHEMA.md`
- `PHASE_03B_DATABASE_VALIDATION.md`
- `PHASE_04_FLOWS_RULES_LINEAGE.md`
- `PHASE_04_INDEPENDENT_REVIEW.md`
- `PHASE_05_QUALITY_TESTING.md`
- `INDEPENDENT_ARCHITECTURE_REVIEW.md`

Son evidencia histórica, no contexto obligatorio diario.

## Regla para futuras IA

No vuelvas a realizar ingeniería inversa completa del repositorio salvo que exista evidencia de que la documentación quedó obsoleta. Sigue `PROJECT_STATE` → `MASTER_DOSSIER` → `AI_CONTEXT_MAP` → módulo → 2–6 archivos → tests.

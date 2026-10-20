# Módulo Personal

## Propósito

Mantener la ficha del trabajador, sus asignaciones vigentes, vínculo laboral, número de marcador, cese/reingreso y carga masiva de padrones.

## Responsabilidades

- Alta, edición, baja e historial de trabajadores.
- Validación de identidad y estado laboral.
- Asignaciones temporales de área, turno, marcador y sueldo.
- Registro de `PeriodoLaboral` para vínculos y reingresos.
- Carga CSV/Excel con resolución de catálogos.

## Entidades

`Trabajador`, `PeriodoLaboral`, `AsignacionArea`, `AsignacionTurno`, `AsignacionMarcador`, `AsignacionSueldo` y enumeraciones personales.

## Servicios

- `Application/Personal/ServicioTrabajadores.cs`.
- `Infrastructure/Personal/CargaMasivaTrabajadores.cs`.
- `Infrastructure/Persistencia/Consultas/ConsultaTrabajadores.cs`.
- `TrabajadorRepository` para persistencia del agregado.

## Reglas

- Documento de identidad válido y restricciones de unicidad.
- Las vigencias no deben solaparse según su tipo.
- El cese cierra asignaciones activas.
- Un trabajador debería conservar un único vínculo laboral abierto.
- El motor actual usa campos planos de `Trabajador`, no `PeriodoLaboral`.

## Flujos

Alta/edición → validar → sincronizar ficha/asignaciones → persistir → historial/auditoría.

Carga masiva → leer CSV/Excel → mapear columnas → resolver catálogos → validar filas → crear/actualizar.

## Fuentes de verdad

- Ficha operativa actual: campos planos y asignaciones vigentes.
- Historia de vínculo: `PeriodoLaboral`, todavía no consumida por cálculo/reportes.
- Reglas aceptadas: `ACCEPTANCE_RULES.md`, casos PL-01/02.

## Archivos críticos

- `src/ControlMarcacion.Domain/Personal/Trabajador.cs`
- `src/ControlMarcacion.Domain/Personal/PeriodoLaboral.cs`
- `src/ControlMarcacion.Application/Personal/ServicioTrabajadores.cs`
- `src/ControlMarcacion.Infrastructure/Personal/CargaMasivaTrabajadores.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Configuraciones/ConfiguracionesPersonal.cs`

## Pruebas

`PeriodoLaboralAcceptanceTests`, `BajaTrabajadorTests`, `CambioDeTurnoTests`, `CargaMasivaPadronesDePlantaTests`, `FiltrosListadoPersonalTests` y lectores de carga.

## Problemas conocidos

- RESUELTO EN CÓDIGO/R-P1-02: crear y editar conserva un solo vínculo abierto y el cese lo cierra, incluso entre `DbContext` distintos.
- DATOS/P1: 85 grupos duplicados, 170 filas.
- CONCURRENCIA/P1: la unicidad física del vínculo abierto debe agregarse después del saneamiento `DATA-PERIODOLABORAL`; hoy dos escrituras concurrentes aún no tienen constraint final.
- RIESGO: adoptar `PeriodoLaboral` como fuente sin sanear divergencia.
- RIESGO: historia de maestros actuales puede alterar reportes reemitidos.

## Límite de cambio

La escritura futura quedó corregida en R-P1-02. Reparar datos e imponer la unicidad física en una operación separada, reversible y con bitácora.

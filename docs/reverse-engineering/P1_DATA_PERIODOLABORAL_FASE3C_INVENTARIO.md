# P1-DATA-PERIODOLABORAL — Fase 3C / Inventario previo

Fecha: 2026-10-13  
Estado del documento: **INVENTARIO PREVIO A CAMBIOS 3C**  
Baseline: build Release 0 errores/0 advertencias; 874/874 pruebas, 0 fallos, 0 omitidas

## Criterio de clasificación

- **C0**: ficha, proyección o dato actual; no es una consulta histórica.
- **C1**: vigencia laboral por fecha.
- **C2**: empresa o planta histórica por fecha.
- **C3**: reporte histórico o por rango.
- **C4**: tareo/asistencia.
- **C5**: seguridad o autorización.
- **C6**: importación/atribución de marcaciones.
- **C7**: otros consumidores.

`Trabajador.FechaIngreso`, `FechaCese`, `EmpresaId` y `PlantaId` se conservan. Un uso C0 no debe
migrarse por reflejo: la ficha necesita una proyección actual. Un C1–C4 sí debe dejar de usarla
cuando la decisión depende de una fecha histórica.

## Matriz de consumidores encontrada

| Clase | Archivo | Método/área | Legacy actual | ¿Histórico? | Fecha de referencia | Origen aplicable | Riesgo | Fase |
|---|---|---|---|---|---|---|---|---|
| C0 | `Application/Personal/ServicioTrabajadores.cs` | alta/edición/validación de ficha | cuatro planos | No; comando de estado actual | fecha efectiva del comando | planos como proyección + escritores canónicos | Medio | Revisar escritores 3C-2 |
| C0 | `Infrastructure/Consultas/ConsultaTrabajadores.cs` | listado, ficha, edición | cuatro planos | No; pantalla actual | hoy/ninguna | proyección actual | Bajo | NO_APLICA |
| C0 | `ConsultaTrabajadores.cs` | historial | cabecera plana; lista ya usa `PeriodoLaboral` | Mixto | cada fila del historial | periodo por fila | Bajo | Parcialmente migrado |
| C0 | `ServicioJustificaciones.cs` | desplegable y autorización de comando | `FechaCese`, `PlantaId` | No; operación actual | hoy | proyección actual | Bajo | NO_APLICA |
| C0 | `ServicioSolicitudes.cs` | desplegable y alcance actual | `FechaCese`, `PlantaId` | No; operación actual | hoy | proyección actual | Bajo | NO_APLICA |
| C0 | `ServicioReintegrosTareo.cs` | desplegable actual | `FechaCese`, `PlantaId` | No; selección operativa | hoy | proyección actual | Bajo | NO_APLICA |
| C0 | `CargaMasivaTrabajadores.cs` / `CatalogosResueltos.cs` | importación del padrón | cuatro planos | No; escritor | fecha del archivo | escritor canónico | Alto | Revisar escritores 3C-2 |
| C1 | `Domain/Personal/Trabajador.cs` | `TieneVinculoEn`, `EmpresaEn`, `PlantaEn`, `PeriodoEn` | ya no usa planos | Sí | parámetro `fecha` | `PeriodoLaboral` | Medio: método central privado | 3C-1 |
| C1 | `ServicioCalculoTareo.cs` | población y `EntradaCalculo.TieneVinculo` | `FechaIngreso/FechaCese` | Sí | jornada | `PeriodoLaboral` | Crítico | 3C-1/3C-4 |
| C2 | `ServicioCalculoTareo.cs` | feriado y turnos candidatos por planta | `Trabajador.PlantaId` | Sí | jornada | `AsignacionPlanta` del periodo | Crítico | 3C-2/3C-4 |
| C2 | `ServicioReportes.cs` | empresa/planta mostradas y filtradas | `EmpresaId/PlantaId` | Sí | rango/fila | periodo y asignación de planta | Alto | 3C-2/3C-3 |
| C3 | `ServicioReportes.cs` | `ResolverTrabajadoresAsync` | cuatro planos | Sí | rango del reporte | periodos que solapan | Crítico | 3C-3 |
| C3 | `ServicioReportes.cs` | resumen/excepciones | contexto plano resuelto una vez | Sí | cada tareo diario | contexto por fecha; representación explícita de transiciones | Alto | 3C-3 |
| C3 | `ServicioReportes.cs` | tareo final y anexos | fechas/empresa/planta planas | Sí | periodo de planilla | contexto del periodo | Crítico | 3C-3 |
| C3 | `ServicioReportes.cs` | `EvaluarVigenciaDelCalculoAsync` | alta/cese/planta planos y auditoría | Sí | rango | periodos/asignaciones que solapan | Alto | 3C-3 |
| C4 | `ServicioCalculoTareo.cs` | `RecalcularPeriodoAsync` | población plana | Sí | rango y jornada | precarga de historia | Crítico | 3C-4 |
| C4 | `ServicioCalculoTareo.cs` | feriados/candidatos/días sin vínculo | planta y fechas planas | Sí | jornada por inicio | contexto laboral diario | Crítico | 3C-4 |
| C4 | `ServicioCalculoTareo.cs` | bandeja, resumen y liquidación ya calculada | planta actual para alcance | Mixto; el tareo ya tiene fecha | fecha del tareo | planta histórica para alcance, si se migra autorización | Alto | Pendiente posterior explícito |
| C5 | `AlcancePlantasConsultas.cs` | `PuedeVerTrabajadorAsync` | planta actual | No; autorización de ficha/comando | hoy | proyección actual | Bajo | NO_APLICA |
| C5 | `ServicioReportes.cs` | restricción de padrón por planta | planta actual | Sí | rango/fila | planta histórica | Alto | 3C-3 |
| C5 | `ServicioCalculoTareo.cs` | alcance del recálculo | planta actual | Sí | jornadas del periodo | plantas históricas en rango | Alto | 3C-4 |
| C6 | `Marcaciones/ServicioImportacion.cs` y `AtribucionMarcaciones` | atribución física | no usa vínculo laboral | Sí, pero identidad física | instante de marca | equipo + código + `AsignacionMarcador` | Crítico si se cambia | NO_APLICA; preservar |
| C6 | `MarcacionesProvisionales/*` | importación/consulta PMV | planta de equipo o ficha actual | Provisional | instante/carga | reglas PMV existentes | Alto | NO_APLICA sin requisito |
| C7 | `Inicio/ServicioPanelInicio.cs` | contadores actuales | estado/proyección actual | No | hoy | proyección actual | Bajo | NO_APLICA |
| C7 | vistas/DTO de Personal | presentación de ficha | cuatro planos | No | hoy/ninguna | proyección actual | Bajo | NO_APLICA |
| C7 | auditoría de Reportes | nombres de campos planos | Sí, detecta cambios | rango | ampliar a tablas canónicas sin borrar legado | Medio | 3C-3 |

## Conteo inicial

| Clase | Consumidores/grupos inventariados |
|---|---:|
| C0 | 7 |
| C1 | 2 |
| C2 | 2 |
| C3 | 4 |
| C4 | 3 |
| C5 | 3 |
| C6 | 2 |
| C7 | 3 |
| **Total** | **26** |

## Decisiones antes de implementar

1. Consolidar una sola resolución de contexto laboral con cuatro resultados observables:
   `PeriodoLaboral`, `SinPeriodo`, `LegacyFallback` y `ErrorConfiguracion`.
2. La ruta migrada no usará `LegacyFallback`; el valor existe únicamente para diagnosticar un
   consumidor transitorio que lo pida explícitamente.
3. Precargar periodos y asignaciones de planta por trabajadores/rango; queda prohibida una
   consulta por trabajador/día.
4. Un solapamiento produce error controlado; nunca se elige el último ni el ID menor.
5. Un reporte agregado que abarque más de una empresa o planta representará la transición en
   orden (`A → B`) en la misma celda. Las filas diarias resuelven por su propia fecha.
6. La jornada nocturna conserva la fecha de inicio como fecha laboral. `PeriodoLaboral` no cambia
   esa regla ni las de rotación, feriados o atribución de marcas.
7. La importación principal y PMV no cambian en 3C.

## Índices disponibles

- `periodo_laboral(trabajador_id, fecha_ingreso)`;
- único filtrado de periodo abierto por trabajador;
- `asignacion_planta(periodo_laboral_id, fecha_inicio, fecha_fin)`;
- índice de FK por `planta_id`;
- único filtrado de asignación de planta abierta.

La consulta de 3C filtrará primero por `trabajador_id` y solapamiento de rango. No se propone una
migración de índices hasta medir una necesidad real.


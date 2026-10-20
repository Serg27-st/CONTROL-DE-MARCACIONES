# P1-DATA-PERIODOLABORAL — Fase 3C / Matriz de migración

Fecha: 2026-10-13  
Estado: **CERRADA**

## Convención

- `MIGRADO`: la decisión histórica se obtiene de `PeriodoLaboral`/`AsignacionPlanta`.
- `NO_APLICA`: la operación trata el estado actual, un catálogo o una identidad física.
- `FALLBACK_DOCUMENTADO`: usa campos planos temporalmente. No quedó ninguno en la ruta histórica crítica.

| Consumidor | Archivo | Método/área | Fecha de referencia | Legacy anterior | Nuevo origen | Estado | Tests | Riesgo residual / notas |
|---|---|---|---|---|---|---|---|---|
| Vigencia de dominio | `Domain/Personal/Trabajador.cs` | `TieneVinculoEn`, `EmpresaEn`, `PlantaEn` | día consultado | ingreso/cese planos | `ObtenerPeriodoVigente` | MIGRADO | PL3C-VIG-01..07 | 2+ periodos producen error controlado |
| Resolución agrupada | `Infrastructure/Persistencia/Consultas/ConsultaContextoLaboral.cs` | `CargarRangoAsync`, `Resolver` | rango y día | no existía | periodos + asignaciones precargados | MIGRADO | PL3C-VIG, PL3C-ORG | una consulta por rango; no N+1 |
| Empresa histórica | mismo | `Resolver` | día | `Trabajador.EmpresaId` | `PeriodoLaboral.EmpresaId` | MIGRADO | PL3C-ORG-01,03 | sin periodo no inventa empresa |
| Planta histórica | mismo | `Resolver` | día | `Trabajador.PlantaId` | `AsignacionPlanta` vigente | MIGRADO | PL3C-ORG-02,03,06 | ambigüedad produce error |
| Alta/cese/reingreso | `Application/Personal/ServicioTrabajadores.cs` | alta, baja y sincronización | fecha efectiva | solo planos | escritor canónico existente | MIGRADO | contratos `PeriodoLaboral` existentes | se conservan planos como proyección |
| Cambio de planta | mismo | `ActualizarAsync` | fecha efectiva | asignación plana directa | `CambiarPlanta` sobre vínculo abierto | MIGRADO | PL3C-ORG-03 | no abre nuevo vínculo ni cambia empresa |
| Listado/ficha actual | `ConsultaTrabajadores.cs` | listado/detalle/panel | estado actual | planos | planos-proyección | NO_APLICA | regresión general | no es consulta histórica |
| Solicitudes/justificaciones actuales | servicios respectivos | comandos y desplegables | hoy | planta/cese actuales | planos-proyección | NO_APLICA | regresión general | autorización de ficha actual |
| Importación de padrón | `CargaMasivaTrabajadores.cs` | alta/actualización | fecha del padrón | DTO plano | servicio escritor canónico | NO_APLICA | regresión de padrones | consumidor de comandos, no lector histórico |
| Resumen de reportes | `ServicioReportes.cs` | `ResumenTareoAsync` | cada tareo | empresa/planta actuales | contexto por fecha | MIGRADO | PL3C-REP-01..06 | transición agregada se muestra `A → B` |
| Excepciones | mismo | `ExcepcionesInternoAsync` | fecha de incidencia | población plana | contexto por fecha antes de paginar | MIGRADO | PL3C-REP-03..06 | total y página se calculan tras vigencia |
| Tareo final/anexos | mismo | `ArmarFilasTareoAsync` | fecha de fila | ficha actual | contexto por fecha | MIGRADO | regresión end-to-end | no cambia minutos físicos |
| Frescura de reportes | mismo | `EvaluarVigenciaDelCalculoAsync` | rango | ingreso/cese/planta planos | periodos/asignaciones que solapan | MIGRADO | `VigenciaTareoAcceptanceTests` | cambios canónicos invalidan el cálculo |
| Seguridad de reportes | mismo | `ResolverTrabajadoresAsync` | rango/día | planta actual | plantas históricas visibles | MIGRADO | reportes + aislamiento | no concede acceso retroactivo por traslado |
| Población de recálculo | `ServicioCalculoTareo.cs` | `RecalcularPeriodoAsync` | periodo | activo/ingreso/cese planos | historias que solapan el rango | MIGRADO | PL3C-TAREO-01..03 | días sin vínculo quedan `SinVinculo` |
| Feriados/candidatos | mismo | entrada diaria al motor | inicio de jornada | planta actual | planta histórica diaria | MIGRADO | PL3C-TAREO-04 | reglas FER-NAC no cambiaron |
| Justificación fuera de vínculo | mismo | entrada diaria al motor | día justificado | podía afectar el día plano | se conserva, no se aplica sin vínculo | MIGRADO | PL3C-TAREO-05 | el registro histórico no se borra |
| Jornada nocturna/cese | mismo | selección de jornada | día de inicio | regla existente | vínculo del día de inicio | MIGRADO | regresión nocturna existente | no se inventó una nueva regla |
| Alcance del recálculo | mismo | selección y bucle diario | rango/día | planta actual | plantas históricas por día | MIGRADO | aislamiento + suite completa | días ajenos no se reescriben |
| Bandeja/listado/resumen | mismo | `ListarIncidenciasAsync`, `Filtrar` | fecha de tareo | planta actual | `AplicarAlcancePlantas` | MIGRADO | PL3C-TAREO-06 + aislamiento | predicado SQL centralizado y probado en SQLite |
| Liquidación | mismo | `LiquidarPeriodoAsync` | fecha de tareo | planta actual | `AplicarAlcancePlantas` | MIGRADO | liquidación + suite completa | valor económico no fue alterado |
| Atribución de marca | `Marcaciones/ServicioImportacion.cs`, `AtribucionMarcaciones` | atribución física | instante de marca | no aplica | equipo + ID + vigencia de marcador | NO_APLICA | atribución/colisiones | preservada expresamente |
| PMV | `MarcacionesProvisionales/*` | staging provisional | instante/carga | reglas PMV | reglas PMV | NO_APLICA | PMV/seguridad existentes | sigue aislado; no escribe tareo oficial |
| Panel actual | `Application/Inicio/ServicioPanelInicio.cs` | contadores | hoy | proyección actual | proyección actual | NO_APLICA | regresión general | no es reporte histórico |
| Catálogos y organización | servicios de catálogo | mantenimiento | estado actual | IDs organizativos | entidades propias | NO_APLICA | regresión general | no resuelven vínculo laboral |
| Auditoría de frescura | `ServicioReportes.cs` | marcas de modificación | rango | tablas legacy | periodos + asignaciones y legacy | MIGRADO | vigencia de reportes | legacy se vigila mientras exista |

## Resultado

- Consumidores/grupos inventariados: 26.
- Consumidores históricos críticos migrados: 14 filas de esta matriz.
- Fallback legacy en rutas históricas críticas: 0.
- Consumidores bloqueados: 0.
- Campos planos eliminados: 0; permanecen como proyección vigente y compatibilidad.
- Índices nuevos: 0. Los índices de 3A cubren trabajador/fecha y periodo/planta/fecha; no se demostró necesidad de otra migración.


# Registro de contexto de ingeniería inversa

Fecha de corte: 2026-10-09. Este registro evita releer archivos completos sin una razón concreta.

## Archivos ya analizados suficientemente para la fase 1

| Archivo | Alcance leído | Información retenida |
|---|---|---|
| `ControlMarcacion.sln` | completo | seis proyectos y agrupación src/tests |
| `Directory.Build.props` | completo | net10.0, C# latest, nullable, warnings como errores, es-PE |
| `src/*/*.csproj` | completos | referencias y paquetes productivos |
| `tests/*/*.csproj` | completos | stack y límites de pruebas |
| `src/ControlMarcacion.Infrastructure/InyeccionDependencias.cs` | completo | mapa inicial de DI e integraciones |
| `src/ControlMarcacion.Infrastructure/Persistencia/MarcacionDbContext.cs` | completo | DbSets, Identity, auditoría temporal y naming |
| `src/ControlMarcacion.Application/Marcaciones/IServicioImportacion.cs` | completo | contrato de importación/cobertura/anulación |
| `src/ControlMarcacion.Application/Marcaciones/IFuenteDeMarcaciones.cs` | completo | frontera del lector |
| `src/ControlMarcacion.Web/Controllers/MarcacionesController.cs` | completo | entrada HTTP, seguridad, límites y delegación |
| `src/ControlMarcacion.Infrastructure/Marcaciones/ServicioImportacion.cs` | núcleo 1–245 + índice de métodos | validación, lectura, dedupe, persistencia; resto queda pendiente |
| `src/ControlMarcacion.Infrastructure/Marcaciones/LectorMarcadorAutomatico.cs` | completo | detección por contenido y despacho |
| `src/ControlMarcacion.Application/Asistencia/IServicioCalculoTareo.cs` | completo | contrato, DTO y semántica de recálculo/listado |
| `src/ControlMarcacion.Web/Controllers/AsistenciaController.cs` | completo | entrada de consulta y recálculo |
| `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs` | 1–380 + índice de métodos | precargas, bucle central, cálculo y persistencia; consultas posteriores pendientes |
| `src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs` | índice de métodos | superficie de reglas; cuerpo pendiente |
| `src/ControlMarcacion.Web/Controllers/ReportesController.cs` | 1–230 + resto indexado | entrada de consultas/exportación; helpers finales pendientes |
| `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioReportes.cs` | índice de métodos/métricas | responsabilidades y tamaño; lectura detallada pendiente |
| `src/ControlMarcacion.Application/Seguridad/RolesSistema.cs` | completo | roles y matriz declarada |
| `src/ControlMarcacion.Web/Seguridad/ProveedorAlcancePlantasHttp.cs` | completo | aislamiento por planta y acceso Web→DbContext |
| `src/ControlMarcacion.Application/Personal/ServicioTrabajadores.cs` | cabecera/constructor y primeros casos | dependencias y rol; cuerpo pendiente |

También se analizaron estructuralmente nombres, tamaños, extensiones, controladores, entidades Domain, migraciones, activos web, configuración (solo claves, sin exponer valores sensibles), grupos de pruebas y estado Git.

## Archivos críticos

| Prioridad | Archivo/grupo | Motivo |
|---:|---|---|
| 1 | `Domain/Asistencia/CalculoTareo.cs` | reglas puras que determinan el resultado diario |
| 2 | `Infrastructure/.../ServicioCalculoTareo.cs` | linaje y orquestación del cálculo |
| 3 | `Infrastructure/Marcaciones/ServicioImportacion.cs` | integridad de la entrada cruda |
| 4 | `Domain/Catalogos/Turno.cs`, `VersionTurno.cs`, `ProgramacionCuadrilla.cs` | horario histórico y rotación |
| 5 | configuraciones EF + snapshot/SQLite objetivo | modelo físico e invariantes |
| 6 | `ServicioReportes.cs` y DTO de tareo final | linaje del resultado consumido por planillas |
| 7 | solicitudes/justificaciones/reintegros | alteran o complementan el resultado pagable |
| 8 | pruebas de Asistencia, Marcaciones y Reportes | comportamiento existente y oráculo parcial |

## Archivos pendientes por fase

### Próxima lectura: dominio y arquitectura

- Entidades y value objects de Domain, empezando por turnos, rotación, tareo y vigencias.
- Interfaces Application y sus implementaciones concretas para construir la matriz de dependencias.
- Controladores restantes solo hasta identificar autorización, caso de uso y lógica local.
- ADR 0001/0002 y documentos existentes, contrastados contra código; no se asumirán vigentes por sí solos.

### Base de datos

- `Persistencia/Configuraciones/*.cs`.
- Migraciones manuales (`*.cs` sin `.Designer.cs`) y snapshot solo para resolver diferencias.
- Esquema real de la base que el usuario identifique como canónica.

### Reglas y linaje

- Cuerpo completo de `CalculoTareo.cs`.
- Resto de `ServicioCalculoTareo.cs` e importación.
- `ServicioReportes.cs`, `ExportadorReportesExcel.cs` y DTO finales.
- Servicios de solicitudes, justificaciones, periodos, cuadrillas y reintegros.

### Testing/operación

- Pruebas por regla, configuración de coverlet y ejecución controlada.
- Interceptor/consulta de auditoría, manejo global de errores y logs.
- Scripts de publicación/instalación y configuración de producción sin secretos.

## Archivos irrelevantes para lectura habitual

No son inútiles; están fuera del contexto normal y se abrirán solo con una pregunta concreta:

- `bin/`, `obj/`, `.git/`.
- `despliegue/exe/`, `ControlMarcaciones.zip`, `exe.rar`.
- respaldos `.db`, `.db-wal`, `.db-shm` hasta seleccionar uno explícitamente.
- `Migraciones/*.Designer.cs`.
- `MarcacionDbContextModelSnapshot.cs` salvo auditoría de esquema.
- documentos `.docx`/`.pdf` cuando exista su fuente Markdown o no se audite el manual de usuario.
- archivos CSS/JS minificados o comprimidos publicados; usar las fuentes de `wwwroot/`.

## Regla de relectura

Antes de releer un archivo marcado como analizado, registrar:

```text
Archivo:
Motivo de relectura:
Información buscada:
```

La lectura parcial no impide abrir la sección pendiente del mismo archivo; no debe repetirse la sección ya registrada salvo contradicción o cambio en Git.

---

## Actualización de contexto — Fase 2

Fecha: 2026-09-29  
Resultado: docs/reverse-engineering/PHASE_02_DOMAIN_ARCHITECTURE.md

Esta actualización conserva el registro de Fase 1. Si hubiera diferencia sobre el grado de lectura, prevalece la anotación más reciente.

### Relecturas realizadas

Archivo: docs/reverse-engineering/CONTEXT_REGISTER.md  
Motivo: localizar el punto de inserción sin sobrescribir el estado anterior.  
Información buscada: últimas 35 líneas, pendientes y regla de relectura. No se reanalizó la arquitectura.

No se releyó PHASE_01_DISCOVERY.md; se utilizó el estado ya resumido.

### Archivos analizados completamente o con suficiencia de dominio

#### Personal, organización y vigencias

- Domain/Personal: Trabajador, AsignacionMarcador, AsignacionArea, AsignacionTurno, AsignacionSueldo y PeriodoLaboral.
- Domain/Comun: IVigenciaPorFecha y ValidadorVigencias.
- Domain/Organizacion: Empresa, Planta, Area y Seccion.

#### Turnos, cuadrillas y catálogos

- Domain/Catalogos: Turno, VersionTurno, TurnoPlanta, Cuadrilla, ProgramacionCuadrilla, EquipoMarcador, Marcador, Periodo, TipoAusencia y CatalogosSimples.

#### Marcaciones, asistencia, solicitudes y auditoría

- Domain/Marcaciones: MarcacionCruda, ImportacionMarcador, AtribucionMarcaciones y CoberturaDeCarga.
- Domain/Asistencia: TareoDiario, TipoIncidencia y ReintegroTareo.
- Domain/Solicitudes: SolicitudAusencia y Justificacion.
- Domain/Parametros: ParametroLegal y TasaSobretasa.
- Domain/Auditoria/RegistroAuditoria.cs.
- Domain/Reportes/RangoReporte.cs.

#### Seguridad y configuración relacional

- Infrastructure/Seguridad/ApplicationUser.cs y UsuarioPlanta.cs.
- Configuraciones EF de personal, catálogos, organización, marcaciones, asistencia, solicitudes, seguridad y auditoría: analizadas para relaciones, índices, restricciones y borrado. No deben releerse completas salvo auditoría física de Fase 3.

#### Servicios y reglas

- Application/Asistencia/CalculoTareo.cs: entradas, salida, cálculo de jornada, justificaciones e incidencias.
- Infrastructure/Servicios/ServicioCalculoTareo.cs: resolución temporal, ventana, selección de marcas y volcado a TareoDiario.
- Application/Personal/ServicioTrabajadores.cs: reasignaciones, cambios organizativos, cese y validación de vigencias.
- Infrastructure/Servicios/ServicioTurnos.cs: mantenimiento de Turno y VersionTurno.
- Infrastructure/Servicios/ServicioCuadrillas.cs: programaciones, tramos, ancla y protección de periodos cerrados.
- Infrastructure/Servicios/ServicioSolicitudes.cs: aprobación y generación diaria de justificaciones.
- Infrastructure/Servicios/ServicioReportes.cs: población, fuentes de enriquecimiento, historia de marcador/área y reintegros. La composición exhaustiva de cada hoja queda fuera de Fase 2.
- Contratos Application de catálogos, trabajadores, turnos, cuadrillas, equipos, marcadores, periodos, feriados, solicitudes, justificaciones, reportes, reintegros, auditoría, usuarios y alcance de plantas.

### Archivos explorados parcialmente con propósito cerrado

- Controladores y vistas Razor: rutas funcionales, dependencias y patrón HTML/JSON/fetch.
- JavaScript de wwwroot: paneles, parciales, selectores y filtros.
- Program.cs y registro DI: composición de capas, Identity y servicios.
- Configuraciones EF: algunas salidas agrupadas fueron truncadas, pero las relaciones usadas en Fase 2 quedaron confirmadas. El detalle físico pasa a Fase 3.
- ServicioJustificaciones y servicios de periodos/reintegros: contratos, operaciones y dependencias suficientes para el mapa; reglas secundarias no necesarias no se profundizaron.

### Conclusiones fijadas para no repetir análisis

- MarcacionCruda es el hecho original y no contiene trabajador; se atribuye temporalmente mediante AsignacionMarcador.
- AsignacionTurno contiene exactamente TurnoId o CuadrillaId.
- La resolución por cuadrilla encadena AsignacionTurno → ProgramacionCuadrilla → TramoRotacion → Turno → VersionTurno.
- Turno conserva estado actual y VersionTurno horario histórico; el cálculo prefiere la versión vigente.
- La rotación utiliza fecha ancla y módulo positivo sobre la suma de días.
- No existe tramo explícito de descanso: cada tramo exige un turno.
- Jornada no es entidad persistida; se distribuye entre ServicioCalculoTareo, EntradaCalculo, CalculadoraTareo, ResultadoCalculo y TareoDiario.
- Una jornada nocturna pertenece a la fecha de inicio; se usa primera y última marca de la ventana y se evita reutilizar marcas de la jornada anterior.
- TareoDiario es una proyección persistida recalculable y única por trabajador/fecha.
- Aprobar SolicitudAusencia genera Justificacion por día; solo la aprobada modifica el cálculo.
- La incidencia no tiene tabla propia; vive en TareoDiario.
- ReintegroTareo es un ajuste por trabajador/periodo separado del cálculo diario.
- El reporte es una proyección multi-fuente, no una entidad.
- PeriodoLaboral existe, pero cálculo y reportes observados usan principalmente campos actuales de Trabajador.
- No existe historial de planta del trabajador; sí existe historial de área.
- El alcance de plantas sí se aplica aunque comentarios antiguos indiquen lo contrario.
- Los proyectos no forman ciclos; el alto acoplamiento funcional se concentra en cálculo y reportes.

### Artefactos creados en Fase 2

- docs/reverse-engineering/PHASE_02_DOMAIN_ARCHITECTURE.md
- docs/reverse-engineering/diagrams/D01_SYSTEM_CONTEXT.md
- docs/reverse-engineering/diagrams/D02_CONTAINERS_LAYERS.md
- docs/reverse-engineering/diagrams/D03_BACKEND_COMPONENTS.md
- docs/reverse-engineering/diagrams/D04_FRONTEND_NAVIGATION.md
- docs/reverse-engineering/diagrams/D05_DOMAIN_MODEL.md
- docs/reverse-engineering/diagrams/D13_MODULE_DEPENDENCIES.md

### Pendientes transferidos a fases posteriores

- Contrastar el modelo EF con migraciones, snapshot y una base seleccionada.
- Verificar integridad real de vigencias, duplicados y datos históricos.
- Auditar concurrencia, transacciones e idempotencia con escenarios ejecutables.
- Revisar autorización acción por acción y cobertura completa del alcance de plantas.
- Auditar composición y rendimiento de cada reporte/exportación.
- Ejecutar y clasificar pruebas por regla de negocio.
- Resolver con negocio las preguntas sobre descanso rotativo, historia organizativa, recontrataciones, pareo de marcas y trazabilidad.

No releer las fuentes de Fase 2 para repetir el mapa. Solo abrir una sección concreta si una fase posterior necesita verificar esquema, seguridad, rendimiento, una contradicción o un cambio de Git, registrando el nuevo propósito.

---

## Actualización de contexto — Fase 3A

Fecha: 2026-09-29  
Resultado: docs/reverse-engineering/PHASE_03A_PERSISTENCE_SCHEMA.md

### Relecturas realizadas

Archivo: docs/reverse-engineering/PHASE_02_DOMAIN_ARCHITECTURE.md  
Motivo: recuperar el estado inicial y los riesgos transferidos a persistencia.  
Información buscada: resumen, nivel de certeza y conceptos físicos prioritarios; solo primeras 45 líneas.

Archivo: docs/reverse-engineering/CONTEXT_REGISTER.md  
Motivo: respetar exclusiones y propósitos de lectura.  
Información buscada: últimas 150 líneas con el cierre de Fase 2.

Archivo: src/ControlMarcacion.Infrastructure/Persistencia/MarcacionDbContext.cs  
Motivo: reconstruir el modelo físico.  
Información buscada: DbSet, convenciones, configuración de Identity, auditoría y snake_case.

Archivos: Persistencia/Configuraciones/ConfiguracionesPersonal.cs, ConfiguracionesCatalogos.cs, ConfiguracionesOrganizacion.cs, ConfiguracionesMarcaciones.cs, ConfiguracionesAsistencia.cs, ConfiguracionesSolicitudes.cs, ConfiguracionSeguridad.cs y ConfiguracionesAuditoria.cs.  
Motivo: Fase 3A exige verificar el estado físico que se había leído solo parcialmente.  
Información buscada: PK, FK, unique, índices, filtros, nulabilidad, defaults, conversiones, checks, delete behavior y concurrencia.

Archivos: Domain/Personal/Trabajador.cs, Domain/Catalogos/CatalogosSimples.cs, Domain/Asistencia/TareoDiario.cs y Domain/Asistencia/ReintegroTareo.cs.  
Motivo: resolver nulabilidad, IDs escalares y sincronización de fuentes duplicadas.  
Información buscada: declaraciones de propiedades y métodos AbrirVinculo, DarDeBaja y CambiarEmpresa; no se repitió el análisis funcional.

Archivos: ServicioTrabajadores.cs, ServicioTurnos.cs, ServicioCuadrillas.cs, ServicioMarcadores.cs y ServicioEquipos.cs.  
Motivo: determinar escritores, protección temporal y sincronización.  
Información buscada: secciones concretas de alta/edición/cese, vigencias, programación y copiado de planta.

Archivos: ServicioCalculoTareo.cs, ServicioReportes.cs, ServicioImportacion.cs, ConsultaTrabajadores.cs, ServicioSolicitudes.cs y ServicioJustificaciones.cs.  
Motivo: inventario estático de consultas y CRUD.  
Información buscada: materializaciones, consultas dentro de loops, AsNoTracking, Includes, secuencias de guardado y predicados; no se hizo tuning.

### Archivos nuevos analizados

- Las 13 migraciones manuales desde 20260817165631_EsquemaInicial hasta 20260925150116_VigenciaDelAreaDelTrabajador.
- MarcacionDbContextModelSnapshot.cs, únicamente como estado final del modelo y relaciones.
- ServicioUsuarios.cs, solo para CRUD de Identity y UsuarioPlanta.
- TrabajadorRepository.cs y servicios persistentes, solo mediante inventario de operaciones Add/Remove/estado.

No se abrió ningún archivo Designer.cs.

### Verificaciones ejecutadas

- Compilación de ControlMarcacion.Web sin restaurar paquetes: correcta, 0 errores y 0 advertencias.
- dotnet ef migrations has-pending-model-changes después de compilar: no hay cambios pendientes entre modelo y snapshot.
- Conteo estático del snapshot: 44 tablas, 41 FKs, 31 índices únicos, un check y cuatro tokens de concurrencia, dos de negocio.

Una comprobación EF previa a recompilar usó binarios antiguos y señaló cambios pendientes. Se descartó como evidencia después de la compilación y repetición satisfactoria. No representa drift del estado fuente actual.

### Conclusiones fijadas para no repetir análisis

- El modelo físico actual tiene 44 tablas y 41 FKs.
- Trabajador no tiene FKs físicas para tipo de documento, empresa, planta, área, sección, puesto ni centro de costo.
- TareoDiario no tiene FKs a periodo, trabajador, turno o justificación.
- ReintegroTareo no tiene FKs a periodo/trabajador.
- SolicitudAusencia no tiene FK a trabajador; Justificacion no tiene FKs a trabajador, periodo o tipo de ausencia.
- No existe relación física TareoDiario ↔ MarcacionCruda.
- Las asignaciones de marcador, área, turno y sueldo, y VersionTurno, impiden más de una fila abierta mediante índices filtrados.
- Ninguna de esas restricciones excluye solapamientos entre filas históricas cerradas.
- PeriodoLaboral y ProgramacionCuadrilla no tienen unicidad física de fila abierta.
- AsignacionTurno tiene el único check de negocio: exactamente turno o cuadrilla.
- Periodo y Justificacion tienen tokens de concurrencia propios.
- MarcacionCruda es única por equipo, código e instante.
- ImportacionMarcador.HashContenido está indexado pero no es único.
- Importación conserva marcas al anular y los consumidores deben excluir la cabecera anulada.
- ServicioImportacion guarda cabecera y marcas en dos SaveChanges separados.
- Marcador.PlantaId y EquipoMarcador.PlantaId son dos FKs independientes sin constraint de igualdad.
- Modelo EF actual y snapshot son consistentes; la aplicación real de migraciones en una BD es desconocida.
- Las siete líneas temporales quedaron clasificadas: cinco con protección parcial y dos solo aplicación.
- La asignación masiva de cuadrilla realiza consultas dentro del loop de trabajadores; es candidato de medición, no se declaró N+1.
- Solicitudes precarga periodos/justificaciones antes del loop diario; el loop no consulta por día.
- No se confirmó ningún problema de performance.

### Decisión de negocio incorporada

La rotación requerida es 14 días diurno, 1 día de descanso móvil, 7 días nocturno y repetir. El descanso forma parte del ciclo de 22 días. El esquema actual no lo representa porque TramoRotacion exige TurnoId. No resolver en Fase 3A.

### Documentación creada

- docs/reverse-engineering/PHASE_03A_PERSISTENCE_SCHEMA.md
- docs/reverse-engineering/DATABASE.md
- docs/reverse-engineering/diagrams/D06_DATABASE_ERD.md

### Pendientes exclusivos de Fase 3B

- Identificar una copia canónica controlada.
- Consultar __EFMigrationsHistory y PRAGMA.
- Contar huérfanos, duplicados, divergencias y solapamientos.
- Verificar los índices físicamente presentes.
- Obtener SQL y planes SQLite de consultas críticas.
- Medir tiempos, memoria, viajes SQL y cardinalidades.
- Comprobar atomicidad y fallos parciales de importación con pruebas controladas.

No abrir archivos de base, WAL o SHM sin que el usuario identifique la copia canónica. No repetir el catálogo ni el análisis de configuraciones completas.

---

## Actualización de contexto — Fase 3B

Fecha: 2026-10-05  
Resultados:

- `docs/reverse-engineering/PHASE_03B_DATABASE_VALIDATION.md`
- `docs/reverse-engineering/modules/PROVISIONAL_MARKINGS.md`
- `docs/reverse-engineering/diagrams/D14_PMV_DATA_FLOW.md`

### Relecturas realizadas

Archivo: documentación de Fases 1, 2 y 3A, `DATABASE.md`, revisión independiente y este registro.  
Motivo: continuar desde el estado fijado y recuperar riesgos que requerían evidencia física.  
Información buscada: conclusiones, pendientes exclusivos de 3B, criterios de certeza y contradicciones; no se reconstruyó el repositorio.

Archivos: D02, D03, D04, D05, D06 y D13.  
Motivo: incorporar PMV solo en diagramas afectados.  
Información buscada: nodos y fronteras existentes para cambios mínimos.

Archivo: `ServicioImportacion.cs`, sección de carga y dos guardados.  
Motivo: responder A–G del riesgo transaccional.  
Información buscada: estado tras cada `SaveChanges`, deduplicación por hash y posibilidad de reintento.

Archivos: lector automático, detector y lector de respaldo ZKTeco.  
Motivo: comprobar soporte `.dat` real del PMV.  
Información buscada: detección por contenido, layout binario, timestamps y limitaciones.

Archivo: interceptor de auditoría.  
Motivo: comprobar si una carga PMV escribe indirectamente fuera de `prov_*`.  
Información buscada: exclusiones; las tres entidades PMV están excluidas.

Archivo: snapshot.  
Motivo: comparación física con `sqlite_master`.  
Información buscada: nombres de 47 tablas, 87 índices y relaciones PMV; no se leyó ningún `Designer.cs`.

### Archivos PMV analizados

- tres entidades Domain;
- tres contratos Application y sus DTO;
- servicios de importación/consulta/exportación Infrastructure;
- configuración EF y migración manual PMV;
- controller, view model y vista Index;
- 35 pruebas declaradas en los tres archivos PMV;
- `docs/07-marcaciones-provisionales.md` y `docs/08-prueba-final-pmv.md`.

### Base canónica y protocolo

- Ruta: `src/ControlMarcacion.Web/ControlMarcacion.db`.
- Hash inicial/final del archivo principal: `d4bcdab88d74668b74de123346b941f17fb86d103bb07ce1a81fb119b9a3effa`.
- Conexiones: URI `mode=ro` y `PRAGMA query_only=ON`.
- No se inició la aplicación, no se ejecutaron migraciones, semillas ni pruebas que escribieran la base.
- SQLite creó un WAL vacío y SHM de coordinación al abrir la base WAL; el archivo principal permaneció idéntico. Su eventual retiro requiere autorización explícita.

### Conclusiones fijadas

- Snapshot y base coinciden: 47 tablas de aplicación, 45 FK y 87 índices explícitos; con dos tablas técnicas EF hay 49 tablas físicas.
- Las 14 migraciones presentes están aplicadas; PMV corresponde a una sola migración.
- `integrity_check=ok`; `foreign_key_check` no devuelve filas.
- No se encontraron huérfanos críticos.
- **OBSOLETO para estado actual — evidencia del corte original:** `periodo_laboral` contenía
  85 grupos abiertos duplicados en sus campos de negocio (170 filas); los campos planos coincidían.
- Área actual/histórica y Turno/VersionTurno no divergen en la muestra.
- La base no tiene cuadrillas, rotaciones, asignaciones de turno, importaciones principales, marcas crudas ni tareos; esos flujos no son medibles con datos actuales.
- PMV es una rama separada y escribe únicamente tres tablas `prov_*`.
- PMV no aplica alcance por planta: Administrador puede listar, cargar y consultar cualquier equipo activo.
- PMV reutiliza los lectores principales; no duplica parser.
- PMV usa SHA-256 pero no unique físico de hash; la protección de archivo es de aplicación.
- La importación PMV es transaccional; la principal conserva el riesgo de cabecera sin marcas.
- PMV tiene 2 importaciones, 731 marcas y 761 errores; contadores y relaciones cuadran.
- 275 marcas/165 jornadas PMV no tienen asignación resoluble; no hay atribución ambigua.
- PMV usa atribución histórica de marcador, pero organización actual; una cuadrilla se muestra como turno sin resolver su rotación.
- Rango >40 bloquea exportación, no consulta.
- Excel PMV tiene 13 columnas y no incluye Unidad.
- Los planes principales observables usan índices; el rango PMV ordena con B-tree temporal, sin impacto confirmado al volumen actual.

### Documentación actualizada

- `DATABASE.md`
- `diagrams/D02_CONTAINERS_LAYERS.md`
- `diagrams/D03_BACKEND_COMPONENTS.md`
- `diagrams/D04_FRONTEND_NAVIGATION.md`
- `diagrams/D05_DOMAIN_MODEL.md`
- `diagrams/D06_DATABASE_ERD.md`
- `diagrams/D13_MODULE_DEPENDENCIES.md`

`TECHNICAL_DEBT.md` no existe y no se creó, conforme al alcance.

### Pendientes transferidos

- Resolver con negocio los duplicados de periodo y las jornadas PMV sin dueño.
- Definir organización histórica y semántica de turno/cuadrilla en PMV.
- Probar fallo entre commits de importación principal en SQLite descartable si se requiere evidencia ejecutable.
- Obtener dataset representativo de marcaciones/tareo/rotaciones para planes y tiempos.
- Continuar con Fase 4 solo por solicitud explícita.

---

## Actualización de contexto — Fase 4

Fecha: 2026-10-05  
Resultados:

- `docs/reverse-engineering/PHASE_04_FLOWS_RULES_LINEAGE.md`
- `docs/reverse-engineering/BUSINESS_RULES.md`
- diagramas D07–D12

### Relecturas y lecturas dirigidas

Archivo: `docs/reverse-engineering/CONTEXT_REGISTER.md` y cierres de Fases 2–3B.  
Motivo: continuar desde 83 % sin reconstruir el repositorio.  
Información buscada: hechos fijados, exclusiones y pendientes de reglas/linaje.

Archivos: `CalculoTareo.cs`, `TareoDiario.cs`, `TipoIncidencia.cs`, `DeteccionTurnoDiscrepante.cs` y bono nocturno.  
Motivo: reconstruir fórmulas, estados, prioridad de incidencias y efectos de justificación.  
Información buscada: comportamiento ejecutable, constantes y campos de salida.

Archivos: `ServicioCalculoTareo.cs`, atribución, cobertura, `ProgramacionCuadrilla` y `VersionTurno`.  
Motivo: cerrar selección histórica, ventanas, rotación y persistencia.  
Información buscada: entradas reales de la calculadora y linaje de `TareoDiario`.

Archivo: `ServicioImportacion.cs` y lectores ZKTeco, únicamente secciones del flujo principal.  
Motivo: construir D07 y enlazar la cadena completa.  
Información buscada: validación de origen, dedupe, persistencia y borde transaccional.

Archivos: servicios/entidades de solicitudes, justificaciones y reintegros.  
Motivo: documentar alteraciones posteriores al dato crudo.  
Información buscada: estados, autorizaciones funcionales, efecto diario y restricciones de periodo.

Archivos: `ServicioReportes.cs`, DTO y `ExportadorReportesExcel.cs`.  
Motivo: linaje por reporte, hoja y campo Excel.  
Información buscada: fuente histórica/actual/calculada/manual, fórmulas, posiciones y vigencia.

Archivos: pruebas declaradas de Asistencia, Marcaciones, Reportes, Solicitudes/Justificaciones y PMV.  
Motivo: relacionar reglas con evidencia de test sin ejecutar suite.  
Información buscada: nombres de escenarios y brechas de cobertura directa.

Archivo: `modules/PROVISIONAL_MARKINGS.md` y D14.  
Motivo: incorporar PMV al catálogo sin reauditar su base.  
Información buscada: reglas ya confirmadas y si existía comportamiento adicional. D14 no requirió cambio.

### Conclusiones fijadas

- Comprensión final estimada: 92 %.
- El flujo principal completo y sus seis subflujos quedaron documentados en D07–D12.
- `BUSINESS_RULES.md` contiene 96 reglas principales y 12 PMV con fuente, configuración, cobertura y riesgo.
- La jornada usa primera/última marca, margen final fijo de seis horas y consolidación de rebote menor a dos minutos.
- Ordinario depende de `MinutosJornadaOrdinaria`; extras se parten en los primeros 120 minutos al 25 % y resto al 35 %.
- La franja nocturna 22:00–06:00 no descuenta refrigerio de forma específica; extras nocturnos se modelan como cola de jornada.
- Solo una justificación y una incidencia pueden quedar vigentes por trabajador/día.
- El recálculo conserva `EntradaReal/SalidaReal` crudas incluso cuando una regularización aporta horas usadas para calcular.
- El tareo no congela IDs de marcas, versión de turno ni parámetros usados.
- El tareo final vigente tiene 57 columnas A:BE y cuatro hojas, aunque sobreviven comentarios de tres hojas y bloque AO:BF.
- El reporte mezcla marcador/área históricos con datos actuales de ficha y CECO.
- La alerta de vigencia observa siete tipos de cambio, omite otros insumos y usa el máximo `CalculadoEn`; puede ocultar recálculos parciales.
- `TasaSobretasa` admite empresa, pero los consumidores observados no filtran por ella.
- El estado aprobado de una solicitud puede coexistir con fechas omitidas durante la generación de justificaciones.
- PMV sigue aislado; no se descubrió comportamiento adicional que exigiera modificar D14.

### Artefactos creados

- `PHASE_04_FLOWS_RULES_LINEAGE.md`
- `BUSINESS_RULES.md`
- `diagrams/D07_ZKTECO_IMPORT_FLOW.md`
- `diagrams/D08_MARKING_TO_WORKDAY.md`
- `diagrams/D09_WORKDAY_TO_TAREO.md`
- `diagrams/D10_SHIFT_ROTATION.md`
- `diagrams/D11_REQUEST_JUSTIFICATION_REINTEGRO.md`
- `diagrams/D12_REPORT_GENERATION.md`

### Pendientes transferidos

- Confirmar con negocio las 15 preguntas de Fase 4, especialmente refrigerio/nocturnidad, bono nocturno, aprobación parcial, tasas por empresa y congelamiento histórico.
- Ejecutar pruebas dirigidas por regla solo en una fase posterior autorizada; no se ejecutaron en Fase 4.
- Obtener datos representativos del flujo principal para validar cálculos y alertas de vigencia.
- No iniciar Fase 5 ni crear `MASTER_DOSSIER.md` sin solicitud explícita.

---

## Actualización de contexto — Fase 5

Fecha: 2026-10-05  
Comprensión consolidada: 96 %  
Alcance: especificación verificable, ejecución de pruebas y calidad operativa; sin cambios de producción ni de base canónica.

### Artefactos creados

- `PHASE_05_QUALITY_TESTING.md`
- `ACCEPTANCE_RULES.md`
- `REMEDIATION_PLAN.md`
- cinco archivos de pruebas de aceptación y un baseline sintético en Domain/Infrastructure

### Ejecución

- Línea base: Domain 289/289; Infrastructure 388/388; global 677/677; 0 omitidas.
- Corte final: Domain 290 pasan, 10 fallan, total 300 (458 ms/5.75 s); Infrastructure 397 pasan, 4 fallan, total 401 (18 s/23.19 s).
- Global: 687 pasan, 14 fallan, 0 omitidas, total 701; Domain 624 ms + Infrastructure 18 s; 28.10 s de pared.
- Los catorce fallos son contratos deliberados: HE-01..05, TAR-02/03, LIQ-03/04, PL-01/02 e INC-01..03.

### Conclusiones fijadas

- NH-07 reproducido: el motor asigna 480 ordinarios y no respeta corte 17:22, sábado íntegramente extra ni domingo sin doble reparto.
- Tardanza reproducida: 07:00 devuelve 0; 07:01 y 07:05 devuelven 0 por tolerancia configurada de 5, contra 1 y 5 exigidos por planilla.
- NH-01 reproducido: nocturnidad 480 da 18.80 h equivalentes en vez de 10.80; descanso 480 da 24.00 en vez de 16.00.
- NH-02 reproducido al limpiar tracking entre peticiones: crear + dos ediciones produce tres vínculos; la baja no cierra el vínculo persistido.
- NH-03 reproducido en tres combinaciones: permiso parcial, tardanza justificada y regularización parcial con salida faltante dejan `Incompleto`, pero incidencia nula y sin validación.
- PMV sin alcance por planta continúa como P0 confirmado por inspección; no existe proyecto de pruebas Web.
- Alerta de vigencia sigue parcial: no observa eliminación física, feriado/parámetro y puede ocultar recálculo parcial mediante `MAX(CalculadoEn)`.
- El conteo 79/18/11 era una lectura defectuosa que confundía `Config.=Parcial` con cobertura. La sexta columna daba 83/14/11. Tras reclasificar RN-055/RN-088 y cubrir RN-090: 82 directo, 16 parcial, 10 ninguno.
- Baseline sintético SQLite PMV: 1k=295.56 ms/6.33 MB; 20k=5187.13 ms/65.66 MB; consultas 1/30/31/40/41 días=127.17/1.65/6.72/2.00/4.57 ms (1 día incluye arranque frío). Tareo principal y exportaciones siguen pendientes; la base canónica no se alteró.
- 14+1+7 se conserva como patrón operativo observado, no como ciclo universal; el requisito es flexibilidad de secuencia/duración y el gap es el descanso sin turno.

### Prioridad transferida

- P0: horas extra/regla 17:22, tardanza de planilla 0 y aislamiento PMV.
- P1: liquidación, periodo laboral, incidencias, vigencia, descanso móvil e histórico mutable.
- P2: atomicidad principal, aprobación parcial, borrado físico, DOM1/DOM2, refrigerio/nocturnidad y tasas por empresa.
- P3: hardcodes, observabilidad, documentación, pruebas Web y criterio de retiro PMV.

### Exclusiones respetadas

- No se corrigió ni refactorizó código de producción.
- No se modificó `ControlMarcacion.db`.
- No se reescribieron documentos de fases cerradas ni se creó `MASTER_DOSSIER.md`.

---

## Actualización de contexto — Fase 6

Fecha: 2026-10-05  
Alcance: consolidación documental; sin cambios de producción, base de datos ni pruebas.

### Jerarquía documental vigente

- Para tareas normales, `PROJECT_STATE.md`, `MASTER_DOSSIER.md`, `AI_CONTEXT_MAP.md` y las fichas de `modules/` sustituyen a los informes de fase como contexto de entrada.
- Los informes de Fases 1–5 y las revisiones independientes se preservan sin reescritura como evidencia histórica y fuente de auditoría.
- `BUSINESS_RULES.md`, `ACCEPTANCE_RULES.md`, `DATABASE.md` y `REMEDIATION_PLAN.md` siguen siendo fuentes especializadas vigentes.
- Los diagramas Mermaid D01–D14 continúan siendo la fuente visual primaria; el archivo draw.io es solo una vista editable de alto nivel.

### Regla operativa para futuras IA

No vuelvas a realizar ingeniería inversa completa del repositorio salvo que exista evidencia de que la documentación quedó obsoleta. Inicia en `PROJECT_STATE.md`, continúa con `MASTER_DOSSIER.md`, enruta con `AI_CONTEXT_MAP.md`, abre la ficha del módulo y finalmente solo los archivos específicos allí indicados.

### Obsolescencias bloqueadas

- Cinco minutos no es la tolerancia vigente de tardanza de planilla; la regla confirmada es 0.
- 14+1+7 es un patrón observado, no un ciclo universal o infinito.
- 480 minutos describe comportamiento defectuoso actual en los casos de aceptación, no la regla vigente de jornada diurna.

### Estado de consolidación

- Documentación consolidada creada: dossier, arquitectura, deuda clasificada, mapa IA, estado de proyecto, índice y ocho fichas modulares.
- Resultado de pruebas preservado: 701 totales, 687 pasan, 14 fallos deliberados de aceptación, 0 omitidas.
- Prioridades preservadas: P0 horas extra 17:22, tardanza 0 y aislamiento PMV; P1 liquidación, periodo laboral, incidencias, frescura, descanso móvil e histórico.

---

## Actualización de contexto — Fase 6.1

Fecha: 2026-10-05  
Alcance: reconciliación maestra de requisitos; sin cambios de producción, BD, datos o tests.

### Autoridad vigente

1. Requisito actual confirmado por negocio.
2. `ACCEPTANCE_RULES.md` actualizado.
3. `BUSINESS_RULES.md` reconciliado.
4. Código y tests como evidencia de comportamiento, no como requisito cuando contradicen lo anterior.

### Reglas nuevas o precisadas

- Domingo es descanso semanal; si se trabaja, todo minuto es hora extra al 100 %. La frase previa “como feriado” queda reemplazada por insuficiente.
- Salida anticipada de planilla tiene tolerancia 0: 17:22=0, 17:21=1 y 17:17=5. El valor actual/sembrado de cinco minutos es un bug.
- Rotaciones admiten tramos/duraciones/cambios variables; el descanso dentro de la rotación dura un día. 14+1+7 sigue siendo solo patrón.
- La regularización debe conservar la marca cruda y registrar separadamente original, corrección, motivo, usuario, fecha, validación y auditoría. El sistema cumple parcialmente.
- PMV mantiene su frontera: no calcula conceptos oficiales ni modifica marcaciones, tareo, trabajadores o asignaciones oficiales.

### Artefactos

- Creados: `PHASE_06_1_REQUIREMENTS_RECONCILIATION.md` y `REQUIREMENTS_TRACEABILITY.md`.
- Actualizados: aceptación, catálogo de reglas, dossier, estado, mapa IA, remediación y deuda clasificada.
- Las 108 reglas se clasificaron exactamente una vez: 58 siguen vigentes, 15 requirieron actualización y 35 son comportamiento actual, no requisito. Ninguna fila completa quedó obsoleta; sí fragmentos históricos.
- La cobertura documental cambia de 82/16/10 en Fase 5 a 77 directa/21 parcial/10 ninguna porque cinco requisitos se ampliaron; no se ejecutaron pruebas nuevas.

### Prioridad resultante

- P0: jornada/HE, sábado, domingo 100 %, tardanza 0, salida anticipada 0 y aislamiento PMV.
- P1: liquidación, periodo laboral, incidencias, frescura, rotación, historia, importación no atómica y regularización completa.
- P2: aprobación parcial, borrado físico, refrigerio/nocturnidad y tasas.
- P3: documentación, constantes, observabilidad y retiro PMV.

### Regla de sesión

Para cambios laborales leer primero `ACCEPTANCE_RULES.md`, `REQUIREMENTS_TRACEABILITY.md` y `BUSINESS_RULES.md`. No inferir el requisito desde el código.

---

## Actualización de contexto — Fase 6.2

Fecha: 2026-10-05  
Alcance: contratos de aceptación; tests/fixtures únicamente, sin producción, migraciones, base canónica ni reparación de datos.

### Contratos añadidos

- DOM-01/02/03: domingo diurno corto, jornada completa y nocturno; los tres fallan porque generan ordinarios.
- SA-01/02/03: SA-01 pasa; SA-02/03 fallan por tolerancia configurada de cinco minutos.
- REG-01/02/03/04: los cuatro pasan; confirman inmutabilidad, corrección separada, actores/fechas/validación, bitácora e historia.
- ROT-01/02/03: pasan para 7/7, 10/5 y programaciones sucesivas sin alterar pasado.
- ROT-DESC-01: falla y demuestra que `TurnoId` no admite descanso explícito sin turno.
- SEC-01/02/03: fallan y demuestran ausencia de alcance por planta en listado, importación y consulta/exportación PMV.

### Ejecución completa

- Domain: 309 totales; 293 verdes; 16 rojos deliberados; 0 omitidos; 768 ms reportados.
- Infrastructure: 409 totales; 402 verdes; 7 rojos deliberados; 0 omitidos; 30 s reportados.
- Global: 718 totales; 695 verdes; 23 rojos deliberados; 0 inesperados; 0 omitidos; 35.3 s de pared.

### Cobertura y prioridad

- Cobertura: 84 directa, 16 parcial, 8 sin test.
- P0-HE, P0-TARD, P0-SA y P0-SEC tienen contrato suficiente.
- El flujo estándar de regularización satisface RQ-007 en los escenarios caracterizados; no requiere un P1 separado. Composición/borrado siguen en hallazgos específicos.

### Artefactos

- Creado `ACCEPTANCE_FAILURES.md` con los veintitrés contratos rojos.
- Creados contratos de dominio para domingo, salida anticipada, rotaciones variables y descanso.
- Ampliadas pruebas SQLite de regularización y programación sucesiva.

---

## Actualización de contexto — R-P0-01 Aislamiento PMV por planta

Fecha: 2026-10-05  
Alcance: remediación exclusiva P0-SEC; sin migración, reparación de datos ni cambios en el motor principal.

### Implementación

- Se reutilizó `IProveedorAlcancePlantas`; no se creó un sistema de autorización paralelo.
- `ConsultaCatalogos` filtra equipos activos antes de materializarlos.
- `ServicioImportacionProvisional` valida el equipo solicitado, filtra historial y no revela detalles de una carga duplicada fuera de alcance.
- `ServicioConsultaMarcacionProvisional` valida el equipo dentro de la consulta; la exportación queda protegida al reutilizar este servicio.
- Alcance `null` mantiene acceso global para roles/procesos que ya lo tenían; colección vacía continúa fail-closed.

### Verificación

- SEC-01/02/03 pasaron de rojo a verde.
- SEC-04 nuevo pasa y demuestra consulta/importación dentro de la planta autorizada.
- Pruebas PMV/seguridad dirigidas: 39/39 verdes; aislamiento combinado: 13/13 verdes.
- Suite: Domain 309/293/16; Infrastructure 410/406/4.
- Global: 719 totales; 699 verdes; 20 rojos deliberados; 0 inesperados; 0 omitidos; 27.3 s de pared.

### Estado

P0-SEC: **RESUELTO**. Permanece como deuda no bloqueante una prueba de controlador Web extremo a extremo.

---

## Actualización de contexto — R-P0-02 Tardanza y salida anticipada

Fecha: 2026-10-05  
Alcance: remediación exclusiva P0-TARD + P0-SA; sin cambios de HE, rotación, incidencias, esquema o datos.

### Implementación

- Antes: `CalcularTardanza` comparaba con inicio + `MinutosTolerancia`; `CalcularSalidaAnticipada` comparaba con fin - `MinutosAnticipacionSalida`.
- Ahora: planilla calcula diferencias exactas contra inicio y fin teóricos; `FinTurno` conserva el cruce de medianoche.
- Ambos campos siguen en BD, versiones, DTO, UI y semillas, pero no tienen consumidores en el cálculo de planilla. Se clasifican como configuración obsoleta candidata, sin eliminarlos en esta remediación.
- `ServicioCalculoTareo.Volcar` conserva el mapeo directo de ambos resultados a `TareoDiario`.

### Verificación

- Antes: TAR/SA dirigidos 6 totales, 2 verdes y 4 rojos esperados.
- Después: 77/77 pruebas dirigidas del motor y 10/10 pruebas dirigidas de persistencia/justificación verdes.
- Se añadieron TAR-00, TAR-04, SA-00, SA-04 y TAR-SA-NOC-01.
- Suite: Domain 314/302/12; Infrastructure 410/406/4.
- Global: 724 totales; 708 verdes; 16 rojos deliberados; 0 inesperados; 0 omitidos; 24.7 s de pared.

### Estado

P0-TARD: **RESUELTO**. P0-SA: **RESUELTO**. Siguiente P0: HE/jornada, sin iniciarlo automáticamente.

## Actualización de contexto — R-P0-03 Jornada ordinaria y horas extra

Fecha: 2026-10-05  
Alcance: remediación exclusiva P0-HE; sin cambios en liquidación, refrigerio, nocturnidad económica, rotaciones, PMV, `PeriodoLaboral`, solicitudes, seguridad, esquema ni datos.

### Lecturas

`PROJECT_STATE.md`, `ACCEPTANCE_RULES.md` (§1 y matriz VIG-HE/VIG-DOM), `ACCEPTANCE_FAILURES.md`, `REQUIREMENTS_TRACEABILITY.md`, `modules/ATTENDANCE_TAREO.md` y `AI_CONTEXT_MAP.md`. Código: `CalculoTareo.cs` (`CalcularSinJustificar`, `Jornada`, `Describir`), semilla de turnos y consumidores de `MinutosJornadaOrdinaria`. Pruebas: HE, DOM, TAR/SA y las seis históricas afectadas.

### Defecto caracterizado

- Método: `CalculadoraTareo.Jornada`.
- Regla anterior: `ordinarios = min(trabajados, MinutosJornadaOrdinaria)`, contados desde `max(entrada, inicio)`; extras = resto.
- 480: el tope de duración sembrado en los cinco turnos; el corte se corría con la tardanza.
- Sábado: laborable con 480 ordinarios primero.
- Domingo: con DIU/NOC, `DescansoSemanal` pero repartido en ordinario/25/35; con DOM1/DOM2, laborable y sin 100 %.

### Implementación

- Nuevo `Repartir` en `CalculadoraTareo`: domingo 0/0 (todo 100 %); feriado o descanso de otro día con el comportamiento vigente; sábado laborable 0 ordinarios y todo extra; L–V con turno diurno de 07:00, corte de reloj 17:22; otros turnos con el tope vigente.
- `Jornada` convierte un domingo laborable (DOM1/DOM2, o la jornada armada por una regularización) en `DescansoSemanal`, para que el 100 % llegue al reporte.
- `Describir` agrega la frase de reparto en la observación (invariante 8).
- Sin cambios de `TareoDiario` ni de `Volcar`: los campos existentes reciben los valores nuevos. `FlujoDeRegularizacionTests` lo verifica sobre el tareo persistido con SQLite (622 ordinarios).

### Verificación

- Dirigidos antes: 19 totales, 11 verdes, 8 rojos (HE-01..05, DOM-01..03).
- Dirigidos después: 30/30 verdes (se agregaron 11 casos: HE-LV-01..06, HE-SAB ×4, DOM-04).
- Seis tests históricos fijaban 480 ordinarios en un día diurno de 07:00 y se actualizaron tras demostrar la contradicción con ACCEPTANCE_RULES §1.1: `CalculadoraTareoTests` ×4, `RegularizacionDeMarcacionTests` ×1 y `FlujoDeRegularizacionTests` ×1.
- Suite: Domain 325/321/4; Infrastructure 410/406/4. Global: 735 totales, 727 verdes, 8 rojos deliberados (LIQ-03/04, PL-01/02, INC-01..03, ROT-DESC-01), 0 inesperados, 0 omitidos.

### Estado

P0-HE: **RESUELTO**. No quedan P0. Ambigüedades y deuda registradas en `modules/ATTENDANCE_TAREO.md` y `TECHNICAL_DEBT.md` (DT-07). No se inició P1.

---

## Actualización de contexto — R-P0-03.1 Reconciliación DIU/NOC y umbral HE

Fecha: 2026-10-05  
Alcance: corte en el fin teórico, umbral HE de 15 min, reconciliación de configuración DIU/NOC y documentación; sin cambios en liquidación, refrigerio, nocturnidad económica, rotaciones, PMV, `PeriodoLaboral`, incidencias, ausencia pagada, esquema ni datos.

### Requisitos incorporados

DIU 07:00–17:22; NOC 19:00–05:22 (+1). Tardanza y salida anticipada con tolerancia 0 contra los extremos teóricos. L–V: exceso menor a 15 min sobre el fin teórico = 0 HE; desde 15 min, HE completa desde el fin teórico. Sábado y domingo sin umbral. Feriados sin cambios.

### Implementación (`CalculoTareo.cs`)

- Se retiró la constante 17:22 / 07:00 de R-P0-03. `Repartir` corta el ordinario L–V en `FinTurno`, por reloj y con cruce de medianoche; `MinutosJornadaOrdinaria` deja de decidir el corte L–V.
- `Jornada`: si en L–V laborable el exceso truncado sobre `FinTurno` es menor a `UmbralSobretiempoMinutos` (15), el cómputo se cierra en `FinTurno`; la observación declara los minutos no computados. Se evalúa después de la gracia configurada (0 en todos los turnos).
- Tardanza y salida anticipada: sin cambios (ya usaban inicio y `FinTurno` teóricos).

### Configuración

Semilla `Turno` y `VersionTurno` (desde 2000-01-01, abierta): DIU 07:00–19:00 y NOC 19:00–07:00. No se modificó: la aplicación usa `MigrateAsync`, así que cambiar `HasData` exige migración, y esa migración reescribiría la versión histórica. Clasificado como **CONFIGURACIÓN DE DATOS REQUIERE PLAN DE ACTUALIZACIÓN** (OP-01 / DATA-04 / VIG-TURNO-01): nueva versión en `/Turnos` con «Rige desde» y recálculo de periodos abiertos.

### Tests

- Línea base: 735 / 727 / 8 deliberados / 0 inesperados.
- `JornadaOrdinariaYHorasExtraAcceptanceTests` se reescribió con fixtures DIU 07:00–17:22 y NOC 19:00–05:22 (`MinutosJornadaOrdinaria` = 480 a propósito, para demostrar que no interviene): HE-DIU-00..07, HE-NOC-00..05, DIU/NOC-CANONICO, tardanza-corte DIU/NOC, llegada temprana DIU/NOC, TAR-NOC, SA-NOC, HE-SAB (incluido 10 min sin umbral), DOM-04, DOM-05 (domingo de 10 min al 100 %).
- Actualizados por requisito posterior (TEST DE ACEPTACIÓN ACTUALIZADO POR REQUISITO POSTERIOR); ninguno era una regresión real:
  - `ReglasConfirmadasHorasExtraAcceptanceTests`: fixture DIU 07:00–19:00 → 07:00–17:22; expectativas HE-01..05 sin cambio.
  - Contratos R-P0-03 HE-LV-01..06: «17:23 → 1 HE» y «07:10–17:32 → 10 HE» pasan a 0 HE por el umbral (reemplazados en el archivo reescrito).
  - `CalculadoraTareoTests`: Reparte_la_jornada…, Salir_despues_del_turno…, Con_el_refrigerio_pagado…, La_gracia_no_toca… (fin 17:22 vía `ConFin`); En_el_turno_nocturno_el_veinticinco… y La_nocturnidad_ordinaria… (NOC GCA con fin 05:22; nuevos valores 120/38 HE y 38 nocturnos extra; 442 nocturnos ordinarios); Sin_gracia_salir_diez_minutos… renombrado a …_no_alcanza_el_umbral (480 trabajados, 0 HE).
  - `RegularizacionDeMarcacionTests`: La_jornada_regularizada_conserva_su_sobretiempo (DIU regular) y La_entrada_verificada_completa_la_jornada (543 → 540; ordinarios 540 hasta el fin del turno 08:00–17:00).
  - `FlujoDeRegularizacionTests`: 723 → 720 trabajados y 720 ordinarios (DIU sembrado 07:00–19:00; salida 19:03 bajo el umbral).
- Resultado: Domain 348/344/4; Infrastructure 410/406/4. Global: 758 totales, 750 verdes, 8 rojos deliberados (LIQ-03/04 con los mismos valores 18.80/24.00, PL-01/02, INC-01..03, ROT-DESC-01), 0 inesperados.

### Documentación

ACCEPTANCE_RULES (§1 reglas 1, 1a, 2, 3, 6 y 7; VIG-HE-01/02/03, VIG-DOM-01, VIG-TURNO-01, VIG-TAR/SAL; se corrigieron además los estados obsoletos de VIG-PMV-01/SEC), ACCEPTANCE_FAILURES, REQUIREMENTS_TRACEABILITY, BUSINESS_RULES (RN-046..049 y reglas confirmadas), REMEDIATION_PLAN (P0-01, OP-01, P1-08), PROJECT_STATE, ATTENDANCE_TAREO, MASTER_DOSSIER (describía el motor con 480/5 min y no tenía el NOC) y TECHNICAL_DEBT (DT-07 reclasificada, DATA-04, GAP-06).

### Estado

P0-HE: resuelto en código. Pendiente operativo OP-01. No se inició P1.

---

## Actualización de contexto — OP-01 Versiones DIU/NOC y recálculo piloto

Fecha: 2026-10-05  
Alcance: operativo y de validación sobre una copia descartable. Sin cambios de código, semillas, migraciones ni base canónica.

- Base canónica: hash `d4bcdab8…effa` antes y después; sin `-wal`/`-shm` al copiar.
- Copia en el scratchpad de la sesión (`op01/op01_copia.db`), con `integrity_check` = ok.
- Situación inicial: DIU 07:00–19:00 y NOC 19:00–07:00 (versión única desde 2000-01-01; jornada 480); periodos 2026-07 y 2026-09 abiertos; 0 asignaciones de turno, 0 tareos, 0 marcas.
- Piloto: asignaciones de turno insertadas en la copia (trabajador 51 DIU, 52 NOC); TXT sintético importado con `IServicioImportacion` (28/28); recálculo de 2026-09; `IServicioTurnos.GuardarAsync` con «Rige desde» 2026-09-15; foto sin recalcular; recálculo.
- Resultado: versiones 1 y 2 cerradas el 2026-09-14 con su horario original; versiones 6 y 7 desde el 2026-09-15; auditoría registrada. El 14 conserva el cálculo antiguo y desde el 15 todos los contratos se cumplen (detalle en `OP_01_SHIFT_VERSION_UPDATE.md`). Crear la versión no recalcula el tareo.
- Producción: impacto inmediato nulo (0 asignaciones de turno / 0 tareos). PENDIENTE OPERATIVO: FechaRigeDesde. Nuevo ticket DEPLOY-01 (semilla obsoleta en instalaciones nuevas).
- No se inició P1.

### OP-01 — complemento (2026-10-06)

- Rollback verificado con el servicio de `/Turnos` sobre dos copias adicionales (`op01_rollback.db` y `op01_riesgo.db`):
  - R1: mismo horario con fecha anterior → no hace nada, sin error;
  - R2: deshacer con la misma fecha → corrección en sitio de la versión nueva;
  - R3: frontera posterior → nueva versión;
  - R4: corrección de una versión equivocada → en sitio;
  - R5: fecha ≤ origen con versión abierta nueva → solo corrige la abierta;
  - R6: primera edición con fecha ≤ 2000-01-01 → **reescribe la versión histórica**;
  - R7: vacío = hoy.
- Creado `OP_01_PRODUCTION_PROCEDURE.md` (precondiciones, pasos 1–8, rollback). No se ejecutó en producción.
- Actualizados `OP_01_SHIFT_VERSION_UPDATE.md` (rollback y riesgos), `SHIFTS_ROTATIONS.md`, `PROJECT_STATE.md` y `REMEDIATION_PLAN.md` (UX-01).
- Base canónica: hash `d4bcdab8…` sin cambios.

---

## Actualización de contexto — R-P0-HE.2 Cruces nocturnos de fin de semana

Fecha: 2026-10-06  
Alcance: verificación y corrección exclusiva de NOC viernes→sábado y sábado→domingo; sin cambios de liquidación, feriados, rotaciones, OP-01, esquema o datos.

### Comportamiento reproducido

- `Repartir` clasificaba la jornada completa con `FechaJornada.DayOfWeek`.
- NOC-WE-01/02/04 pasaban antes del cambio.
- NOC-WE-03/05/06 fallaban: el tramo del domingo continuaba acumulándose como HE35 (502, 190 y 181 min, respectivamente) en vez de 180 HE35 + 322/10/1 HE100.

### Corrección

- La fecha y la entidad de jornada siguen siendo las del sábado.
- En una jornada iniciada el sábado, HE25/35 se limita al intervalo anterior a domingo 00:00; el remanente físico desde esa frontera se expone como `MinutosExtra100`.
- Viernes→sábado conserva el corte en `FinTurno` y el umbral de 15 min para el sobretiempo posterior.
- La nocturnidad 22:00–06:00 sigue calculándose sobre el intervalo físico y los tramos HE25/35 usan sus intervalos reales.
- No se creó migración: total trabajado, ordinario, HE25 y HE35 ya persisten; HE100 es derivable sin partir `TareoDiario`.

### Evidencia ejecutada

- Reproducción previa NOC-WE-01..06: 3 verdes y 3 rojos esperados.
- Después del cambio NOC-WE-01..06: 6/6 verdes.
- P0 vecinos HE/DOM/NOC/TAR/SA y motor histórico: 125/125 verdes.
- Suite completa: 764 totales; 756 verdes; 8 rojos deliberados P1; 0 inesperados; 0 omitidos.

P0-HE: **CERRADO**. P1-01 puede iniciarse como iteración independiente; no se inició en R-P0-HE.2.

---

## Actualización de contexto — R-P1-01 Doble conteo en liquidación

Fecha: 2026-10-06  
Alcance: corrección exclusiva de liquidación/agregación; sin cambios de `CalculoTareo`, resultados diarios, OP-01, feriados, rotaciones, incidencias, `PeriodoLaboral`, esquema o datos.

### Causa y semántica

- `LiquidacionPeriodo.TotalHorasEquivalentes` suma ordinarias y el aporte de cada `ConceptoLiquidado`.
- Antes, todos los conceptos aplicaban `minutos × (1 + tasa)`, aunque nocturnidad ya estaba contenida en ordinario/HE/HE100.
- LIQ-03: 8.00 h ordinarias + 8 × 1.35 = 18.80; correcto: 8.00 + prima 2.80 = 10.80.
- LIQ-04: 8.00 h ordinarias históricas + 8 × 2.00 de descanso = 24.00; correcto: excluir la base ordinaria superpuesta y conservar 16.00 del concepto al 100 %.
- `TotalHorasEquivalentes` es un total valorizado en equivalentes de jornada ordinaria, no una duración física pura.

### Implementación

- `ConceptoLiquidado`: nocturnidad aporta `minutos × tasa`; HE25/HE35/descanso/feriado conservan `minutos × (1 + tasa)`.
- `LiquidarPeriodoAsync`: excluye ordinarios superpuestos de filas no laborables trabajadas; descanso usa `MinutosTrabajados` y también deriva el HE100 residual de jornadas sábado→domingo. La ausencia pagada queda intacta para P1-08.
- No se definió porcentaje nuevo: se siguen usando las tasas vigentes persistidas y el redondeo existente.

### Evidencia

- Antes: LIQ-01/02 verdes; LIQ-03 = 18.80 y LIQ-04 = 24.00, ambos rojos.
- Después: LIQ-01..04 y LIQ-HE-NOC-01, 5/5 verdes sobre SQLite en memoria.
- P0: motor HE/DOM/NOC/TAR/SA/NOC-WE 125/125; SEC 4/4.
- Suite: 765 totales; 759 verdes; 6 rojos deliberados (PL-01/02, INC-01..03, ROT-DESC-01); 0 inesperados; 0 omitidos.

P1-01: **RESUELTO**. Siguiente candidato: P1-02, sin iniciarlo automáticamente.

---

## Actualización de contexto — R-P1-03 Incidencias ocultas por justificaciones parciales

Fecha: 2026-10-06  
Alcance: corrección exclusiva de la composición incidencia/justificación; sin cambios de tardanza base, regularización/auditoría, `MarcacionCruda`, rotaciones, esquema o datos.

### Causa

`CalculadoraTareo.Calcular` aplicaba primero la justificación y luego llamaba a
`ConIncidencia`, pero este método retornaba inmediatamente ante cualquier
`JustificacionId`. INC-01..03 quedaban `Incompleto` y con la justificación asociada,
pero sin `TipoIncidencia` ni `RequiereValidacion`; la bandeja filtra por este último campo.

### Corrección

- Se retiró la supresión global por `JustificacionId`.
- La incidencia se deriva del resultado posterior a aplicar la justificación.
- Permiso parcial, tardanza justificada y regularización incompleta conservan la
  anomalía independiente.
- `MarcacionIncompleta` completamente explicada y regularización completa continúan
  sin incidencia pendiente.
- No se cambió la regla de una justificación vigente por trabajador/día; su composición
  general permanece como decisión P2.

### Evidencia

- Antes: INC-01/02/03, 0/3 verdes; estado `Incompleto`, incidencia nula.
- Después: INC-01/02/03, 3/3 verdes sin cambiar expectativas.
- Nuevo INC-PARCIAL-01: la regularización incompleta se persiste como
  `SalidaNoRegistrada` y aparece en `ListarIncidenciasAsync`; una regularización completa
  devuelve cero pendientes.
- Vecinos: 37/37 dominio (incidencias, permisos, tardanza y regularización) y 48/48
  infraestructura (justificaciones, regularización y auditoría).
- Suite: 766 totales; 765 verdes; 1 rojo deliberado (`ROT-DESC-01`);
  0 inesperados; 0 omitidos. Domain 354/353/1; Infrastructure 412/412.

P1-03: **RESUELTO**. No se inició ROT-DESC ni otro P1.

---

## Actualización de contexto — R-P1-04 Vigencia del tareo

Fecha: 2026-10-06  
Alcance: detector de vigencia, contratos VIG y documentación; sin modificar cálculo,
liquidación, periodos laborales, incidencias, PMV, esquema ni datos.

### Mecanismo anterior

- `ServicioReportes.EvaluarVigenciaDelCalculoAsync` comparaba siete máximos de fuentes
  contra `MAX(TareoDiario.CalculadoEn)` del rango completo.
- No respetaba plantas visibles ni el filtro explícito de planta.
- Omitía `VersionTurno`, `TramoRotacion`, feriados, vigencia laboral, tipos de ausencia
  consumidos y eliminaciones físicas.
- Una recalculación reciente de A podía ocultar las filas antiguas de B.

### Fuentes dirigidas

| Fuente | Puede cambiar tareo | Timestamp/evidencia | Invalidaba antes | Invalida ahora |
|---|---:|---|---:|---:|
| `MarcacionCruda` | Sí | `CreadoEn` | Sí | Sí, por rango y asignación de marcador de los trabajadores del alcance |
| Importación anulada/reactivada | Sí | `ModificadoEn` | Sí | Sí, por marcas del alcance |
| Justificación/regularización aprobada | Sí | `CreadoEn`/`ModificadoEn` | Sí, salvo borrado | Sí |
| Eliminación/anulación de justificación aprobada | Sí | `registro_auditoria` | No | Sí |
| `AsignacionMarcador` | Sí | `CreadoEn`/`ModificadoEn` | Sí global | Sí por trabajadores del alcance |
| `AsignacionTurno` | Sí | `CreadoEn`/`ModificadoEn` | Sí global | Sí por trabajadores del alcance |
| `VersionTurno` aplicable | Sí | `CreadoEn`/`ModificadoEn` | No | Sí |
| `ProgramacionCuadrilla` aplicable | Sí | `CreadoEn`/`ModificadoEn` | Parcial/global | Sí |
| `TramoRotacion` aplicable | Sí | `CreadoEn`/`ModificadoEn` | No | Sí |
| Feriado nacional/de planta | Sí | timestamp; auditoría al borrar | No | Sí |
| `ParametroLegal` | No en tareo diario | Existe, pero solo lo consumen reportes/liquidación | No | No aplica |
| `Trabajador` | Sí: alta, activo, ingreso, cese, planta | alta + auditoría selectiva | No | Sí |
| Equipo (descripción/estado) | No lo consume el cálculo diario | timestamp disponible | No | No |
| `TipoAusencia` | Sí: comportamiento y cómputo de tipos usados | auditoría selectiva | No | Sí |

### Corrección

- Referencia: `MIN(CalculadoEn)` del periodo + plantas visibles. Una planta explícita
  estrecha el alcance; una planta recalculada no enmascara otra.
- Estrategia combinada y localizada: timestamps en filas sobrevivientes + auditoría
  existente para eliminación/anulación y cambios selectivos.
- La auditoría se consulta una vez por eventos posteriores al cálculo; luego se filtra
  en memoria. No hay N+1.
- `EXPLAIN QUERY PLAN` confirma uso de
  `ix_registro_auditoria_fecha_hora` para ese recorte temporal.
- No existe parámetro legal usado por `ServicioCalculoTareo`; VIG-06 es no aplicable.

### Contratos y evidencia

- Antes del fix: 8 VIG aplicables, 3 verdes (VIG-01/02/09) y 5 rojos
  (VIG-03/04/05/07/08).
- Después: 8/8 VIG aplicables verdes; el contrato de plan de consulta también pasa.
- Suite: 775 totales; 774 verdes; 1 rojo deliberado (`ROT-DESC-01`);
  0 inesperados; 0 omitidos. Domain 354/353/1; Infrastructure 421/421.

### Límites

- Un periodo cerrado puede quedar correctamente señalado como desactualizado, pero no se
  recalcula: la política de reliquidación continúa pendiente de negocio.
- La auditoría histórica ilegible se ignora para no derribar reportes; no se inventa su
  alcance. Las filas nuevas usan el formato estable del interceptor actual.
- No se agregó persistencia ni migración.

P1-04: **RESUELTO**. No se inició ROT-DESC ni otra remediación.

---

## Actualización de contexto — R-P1-08 Crédito de licencias y ausencias pagadas

Fecha: 2026-10-06  
Alcance: confirmar el crédito de jornada completa pagada, protegerlo con contratos y
reconciliar documentación; sin cambios productivos, de esquema, datos, liquidación,
P1-07 ni ROT-DESC-01.

### Regla confirmada y clasificación

- Una licencia o ausencia pagada de jornada completa acredita **480 minutos = 8 horas
  efectivas sin almuerzo**, aunque DIU 07:00–17:22 y NOC 19:00–05:22 abarquen 622 minutos.
- El crédito no genera HE25, HE35, HE100 ni nocturnidad por sí solo.
- `PermisoParcial` conserva el tiempo realmente trabajado y solo cubre su ventana; no se
  aplana a 480.
- P1-08 deja de ser GAP-06: era un requisito vigente ya implementado y documentado como
  duda funcional.

### Evidencia de implementación

- `CalculadoraTareo.Aplicar` asigna `Turno.MinutosJornadaOrdinaria` a
  `MinutosTrabajados` y `MinutosOrdinarios` solo cuando el tipo paga la jornada.
- VAC y LCG son categorías distintas con comportamiento `JustificadaRemunerada`; los
  tipos `Subsidiada` conservan crédito, `JustificadaNoRemunerada` queda en cero y
  `PermisoParcial` usa una rama separada.
- Los turnos y versiones DIU/NOC conservan `MinutosJornadaOrdinaria = 480`.
- Reportes y liquidación suman los minutos persistidos y convierten a horas dividiendo
  entre 60; no recalculan el crédito desde `FinTurno - InicioTurno` ni lo elevan a 622.

### Inventario de literales 480 / ocho horas en producción

| Uso | Contexto | Clasificación R-P1-08 | Acción |
|---|---|---|---|
| `Turno.MinutosJornadaOrdinaria` y `TurnoDto` | valor predeterminado del crédito/configuración | Correcto para ausencia pagada; no es corte de jornada trabajada | Conservar y proteger con tests |
| `DatosSemilla` (turnos y versiones) | DIU, NOC y turnos especiales con jornada ordinaria 480 | Correcto para el crédito confirmado; L–V trabajado corta por `FinTurno` | Sin cambio |
| `TurnosController` | valor inicial al crear turno | Correcto como predeterminado, aunque el campo conserva usos múltiples | Sin cambio; DT-07 sigue como claridad técnica |
| `TareoDiario` comentario “hasta 480” | descripción histórica del campo persistido | No representa todas las jornadas trabajadas; no altera el cálculo | Sigue registrado como C-01/DOC-01; fuera del código productivo de esta intervención |
| `Program` `TimeSpan.FromHours(8)` | expiración de cookie | No relacionado con ausencias | Ninguna |
| Reportes/liquidación | no contienen literal 480 para ausencias; suman `MinutosOrdinarios` y dividen entre 60 | Correcto: 480 permanece 480 / 8 h | Verificado sin cambio |

### Contratos y verificación

- Antes: 9/9 pruebas existentes de pago/justificación verdes; VAC y subsidio acreditaban
  480, licencia sin goce cero y permiso parcial conservaba su efecto propio.
- Se agregaron tres pruebas: DIU 480 y no 622; NOC 480 sin HE/nocturnidad; LCG persistida
  en SQLite con 480. Se reforzaron VAC y permiso parcial con invariantes explícitas.
- Dirigidos nuevos: dominio 7/7; infraestructura 5/5.
- Vecinos de reportes, liquidación y justificaciones: 110/110 verdes.
- Suite: 778 totales; 777 verdes; 1 rojo deliberado (`ROT-DESC-01`); 0 inesperados;
  0 omitidos. Domain 356/355/1; Infrastructure 422/422.

P1-08: **CERRADO COMO REQUISITO VIGENTE YA SATISFECHO**. No se inició P1-07 ni
ROT-DESC.

---

## Actualización de contexto — R-P1-07 Atomicidad de importación principal

Fecha: 2026-10-06  
Alcance: `ServicioImportacion.ImportarAsync`, contratos IMP-AT y documentación; sin
cambios en PMV, tareo, liquidación, `PeriodoLaboral`, rotaciones, esquema ni datos.

### Flujo y causa pre-fix

1. Archivo, hash, parser, origen, dedupe y consultas se resolvían antes de persistir.
2. Si correspondía, `ServicioEquipos.RegistrarSerieAsync` guardaba la serie aprendida.
3. `SaveChangesAsync` #1 confirmaba `ImportacionMarcador` y sus `ErrorImportacion`.
4. Se agregaban las `MarcacionCruda` y `SaveChangesAsync` #2 intentaba confirmarlas.
5. No existía `BeginTransaction`; los tres tipos de escritura usan el mismo
   `MarcacionDbContext`.

IMP-AT-01/06 reprodujeron el defecto antes del cambio mediante un `SaveChangesInterceptor`
que falla al detectar inserciones reales de `MarcacionCruda`: quedaron 1 cabecera, 2
errores y 1 auditoría nuevos, con 0 marcas. Los otros cinco contratos pasaron pre-fix.

### Corrección

- Lectura, hash, parsing, validación de origen, consultas y dedupe permanecen fuera de la
  transacción; no se mantiene un write lock durante procesamiento del archivo.
- Al quedar la unidad lista se abre `Database.BeginTransactionAsync` para el proveedor
  relacional (SQLite).
- Dentro quedan el aprendizaje condicional de serie, los dos `SaveChangesAsync` de la
  importación, la auditoría generada por su interceptor y el commit.
- Ante cualquier excepción se ejecuta `RollbackAsync` y se vuelve a lanzar la excepción.
- InMemory conserva su rol de doble de pruebas sin transacción ficticia; la garantía se
  verifica con SQLite real.

### Contratos y resultado

- IMP-AT-01: fallo entre etapas revierte cabecera, errores, marcas y auditoría.
- IMP-AT-02: éxito confirma una cabecera, dos marcas y los errores esperados.
- IMP-AT-03: una línea inválida convive con dos marcas válidas.
- IMP-AT-04: el hash detecta la recarga en el mismo equipo y no duplica marcas; se conserva
  la cabecera de trazabilidad vigente del servicio.
- IMP-AT-05: una marca existente se contabiliza como duplicada y solo entra la nueva.
- IMP-AT-06: fallo al insertar marcas revierte además la serie aprendida.
- IMP-AT-07: contadores de cabecera coinciden con las filas confirmadas.
- Dirigidos: IMP-AT 7/7; importación principal 103/103.
- Suite: 785 totales; 784 verdes; 1 rojo deliberado (`ROT-DESC-01`); 0 inesperados;
  0 omitidos. Domain 356/355/1; Infrastructure 429/429.

### Límites preservados

- No se modificaron hash, lectores TXT/DAT, origen, atribución, anulación/reactivación,
  inmutabilidad de `MarcacionCruda` ni PMV.
- Repetir el mismo archivo en el mismo equipo sigue creando una cabecera con cero marcas
  nuevas. Cambiar esa política de trazabilidad/dedupe no pertenece a R-P1-07.
- No se inventariaron ni repararon posibles huérfanos históricos.

P1-07: **RESUELTO**. No se inició ROT-DESC ni otro P1.

---

## Actualización de contexto — D-ROT-DESC Diseño del descanso explícito

Fecha: 2026-10-06  
Alcance: decisión arquitectónica y reconciliación documental; sin código productivo,
migración, datos ni cambios de pruebas.

### Evidencia del modelo actual

- `TramoRotacion.TurnoId` es `long` obligatorio y `ROT-DESC-01` falla exactamente por esa
  nulabilidad.
- `ProgramacionCuadrilla.TramoEn` suma los días y repite la secuencia con módulo alrededor
  de `FechaAncla`; la programación abierta no tiene límite de repeticiones.
- `ResolverTurno` devuelve `Turno?`; cuadrilla sin programación y error de referencia
  terminan como turno nulo con distinto o ningún texto, no como estados de dominio.
- Sin turno, el orquestador no atribuye marcas a una jornada y `CalculadoraTareo` devuelve
  `SinHorario`, cero minutos y no falta.
- La regla vigente más cercana para descanso trabajado es `DescansoSemanal`: todo el tiempo
  físico termina como HE100 para liquidación con la tasa persistida, pero el motor actual
  necesita un turno para ventana, cruce, refrigerio y emparejamiento.

### Decisión

- Se recomienda y adopta para implementación posterior la opción B:
  `TipoTramoRotacion = Trabajo | Descanso` + `TurnoId` nullable.
- Invariante física y de dominio: Trabajo exige turno; Descanso lo prohíbe. El descanso
  confirmado dura un día y cuenta en `DiasDelCiclo`/`FechaAncla`.
- Resolver futuro discriminado: Trabajo, DescansoProgramado, SinProgramacion y
  ErrorConfiguracion. No se usará un `Turno DESCANSO` ni `null` aislado como semántica.
- Datos existentes: backfill íntegro a Trabajo; no inferir descansos históricos. Las
  programaciones futuras conservan el versionado actual y no reescriben pasado.

### Pendientes no estructurales

- Definir si una programación abierta autoriza repetición indefinida.
- Confirmar atribución/ventana de marcas en un descanso sin turno teórico y la equivalencia
  económica con `DescansoSemanalTrabajado`.
- Confirmar precedencia feriado + descanso rotativo; el código actual prioriza feriado.

Documento: `ADR_ROTATION_REST_MODEL.md`. Estado: **ROT-DESC-01 = DISEÑO DEFINIDO /
IMPLEMENTACIÓN PENDIENTE**. Suite de referencia preservada: 785 totales, 784 verdes,
1 rojo deliberado (`ROT-DESC-01`), 0 inesperados.

---

## Actualización de contexto — BD-01 Delimitación temporal en descanso programado

Fecha: 2026-10-06  
Alcance: análisis y especificación; sin código productivo, migración, datos ni pruebas.

### Evidencia reconstruida

- `ServicioCalculoTareo.RecalcularPeriodoAsync` procesa por trabajador y fecha, mantiene un
  `HashSet<DateTime> reclamadas` y siembra el corte con la jornada anterior al periodo.
- `MarcacionesDeLaJornada` usa `InicioTurno - anticipación` hasta `FinTurno + 6 h`, admite
  cruce según el turno y excluye lo no posterior al fin de la jornada previa.
- `SinReclamar` conserva por fecha natural las marcas que ninguna jornada tomó. Es fuente
  de candidatos, no delimitador suficiente para un descanso sin turno.
- La importación consolida doble fichada dentro de 60 s configurables; el motor consolida
  rebotes menores de 2 min solo para contar eventos. La aritmética usa extremos brutos.
- Una marca produce `Incompleto` y cero minutos, pero la heurística actual que decide qué
  extremo falta necesita inicio/fin de turno y no aplica al descanso.
- El cruce actual deriva de `Turno.CruzaMedianoche`; no puede inferirse automáticamente de
  un día civil sin una regla nueva.

### Alternativas y recomendación

Se compararon: extremos del día civil; ventana global configurable; programación previa de
HE; confirmación manual de todo intervalo; e híbrida de propiedad por jornadas, residuo no
reclamado, inferencia conservadora y validación.

Se recomienda la híbrida. Requiere una selección en dos fases: primero reclamar todas las
jornadas con turno; después delimitar el descanso sobre el residuo. Los casos inequívocos
usan primera/última marca; una marca queda incompleta; las fronteras ambiguas requieren
confirmación auditada. No se descuenta refrigerio sin regla expresa.

### Pendiente funcional exacto

Negocio debe aceptar o ajustar tres reglas antes de declarar BD-01 cerrada:

1. el trabajo en descanso pertenece al día natural de su primera marca;
2. puede cerrar en D+1 antes de la ventana de la siguiente jornada laboral;
3. la ambigüedad no se calcula automáticamente y se resuelve mediante confirmación auditada.

Documento principal: `BD_01_REST_WORK_BOUNDARIES.md`. Casos futuros: BD01-01..14. Baseline
preservada: 785/784/1 deliberado/0 inesperados.

### Cierre posterior de BD-01

Negocio aprobó BD01-D01..D03 sin modificar la alternativa híbrida recomendada:

- la primera marca válida no reclamada de D atribuye la jornada al descanso D;
- la salida puede caer en D+1 únicamente antes de la siguiente jornada aplicable y sin
  reutilizar marcas reclamadas;
- toda frontera ambigua queda con cero minutos automáticos y validación auditada.

También confirmó extremos brutos para dos o más marcas inequívocas, una marca como
incompleta, conservación de las dos capas antirrebote, ausencia de refrigerio automático,
HE100 íntegra, domingo sin doble HE100 y separación entre delimitación física BD-01 y
valorización FER-NAC. La hoja de HE del coordinador puede aportar evidencia o autorización,
pero no constituye por sí sola una ventana temporal.

Estado actualizado: **BD-01 CERRADA FUNCIONALMENTE / IMPLEMENTACIÓN PENDIENTE**. BD-02
residual continúa separada.

---

## Actualización de contexto — BD-02 Feriado nacional + descanso programado

Fecha: 2026-10-06  
Alcance: cierre funcional y reconciliación documental; sin código, migración, datos ni
pruebas.

### Decisiones aprobadas

- Feriado ordinario + descanso sin trabajo: un solo día físico, cero minutos y cero HE100.
- Con trabajo: BD-01 determina N y la clasificación económica es `FeriadoTrabajado`; no se
  agrega `DescansoSemanalTrabajado` sobre N.
- Sin sustitutorio: remuneración ordinaria del feriado + base del trabajo + 100 %, sin
  duplicar tiempo físico.
- Con sustitutorio: no corresponde ese adicional; registrar la compensación sigue siendo
  un gap independiente.
- El 1 de mayo no usa la regla ordinaria: trabajado conserva cuatro componentes económicos;
  no trabajado conserva descanso semanal + feriado, siempre sin inventar minutos.
- Cruces de medianoche mantienen la clasificación por la fecha de inicio.

### Contratos

- ROT-FER-01: descanso + feriado ordinario trabajado, base física única y solo
  `FeriadoTrabajado`.
- ROT-FER-02: descanso + 1 de mayo trabajado, base física única y tratamiento especial.
- ROT-FER-03..06 completan ausencia de trabajo y cruces nocturnos.

Estado actualizado: **BD-01 CERRADA / BD-02 CERRADA / ROT-DESC LISTO PARA IMPLEMENTACIÓN**.
El descanso sustitutorio y el día no laborable compensable permanecen como gaps separados.
Documento: `BD_02_HOLIDAY_REST_COINCIDENCE.md`.

---

## Actualización de contexto — R-P1-05 / ROT-DESC implementado

Fecha: 2026-10-06  
Alcance: implementación completa del descanso programado, migración versionada y contratos
ejecutables; sin aplicar la migración a la base canónica.

### Modelo y persistencia

- `TramoRotacion` incorpora `Tipo = Trabajo | Descanso` y `TurnoId` nullable con las
  invariantes: Trabajo exige turno; Descanso lo prohíbe y dura exactamente un día.
- `ProgramacionCuadrilla` resuelve una secuencia finita. Fuera de ella devuelve
  `SinProgramacion`; ya no repite el patrón mediante módulo.
- La resolución diaria discrimina Trabajo, DescansoProgramado, SinProgramacion y
  ErrorConfiguracion. Las altas y ediciones rechazan combinaciones inválidas antes de
  persistirlas.
- La migración `DescansoProgramadoEnRotaciones` hace backfill de los tramos existentes a
  Trabajo, conserva sus turnos, agrega restricciones físicas y recrea correctamente tabla,
  índice y FK en SQLite. No se ejecutó contra `marcaciones.db` ni otra base operativa.

### Delimitación y cálculo

- El recálculo reclama primero todas las jornadas con turno de D-1 a D+1 y procesa después
  el residuo del descanso; una salida nocturna previa no puede convertirse en entrada del
  descanso.
- Dos o más extremos inequívocos se calculan de primera a última marca bruta, sin refrigerio
  automático, y todo minuto reconocido es HE100. Una sola marca queda incompleta y una
  frontera ambigua queda en validación con cero minutos automáticos.
- Domingo + descanso conserva una sola base física. Feriado ordinario + descanso prioriza
  `FeriadoTrabajado`, también sin duplicar minutos.
- La coincidencia con el 1 de mayo queda identificada estructuralmente, pero la liquidación
  rechaza expresamente valorizarla hasta contar con los conceptos económicos necesarios;
  no se inventó una fórmula incompleta.

### Contratos y resultado

- Se incorporaron 18 contratos nominales: ROT-DESC, ROT-MARK, ROT-FER, ROT-REP y validación
  de invariantes/migración.
- Suite final: **803/803 verdes**, 0 rojos, 0 omitidos. Dominio: 368/368; infraestructura:
  435/435.
- `ROT-DESC-01`, antes rojo deliberado, está verde. El incremento respecto del baseline es
  exactamente de 18 pruebas nominales.

### Despliegue pendiente

Antes de aplicar la migración en un entorno operativo se requiere respaldo verificable,
ejecución controlada, `PRAGMA integrity_check`, comprobación de backfill y plan de reversión.
El `Down` es seguro mientras no existan tramos Descanso; si ya se crearon, la reversión debe
restaurar el respaldo o convertirlos mediante una decisión funcional explícita.

Estado actualizado: **R-P1-05 / ROT-DESC IMPLEMENTADO Y VERIFICADO EN CÓDIGO; DESPLIEGUE DE
MIGRACIÓN PENDIENTE**.

---

## Actualización de contexto — OP-02 Ensayo de despliegue ROT-DESC

Fecha: 2026-10-07  
Alcance: copias verificables de la base canónica; sin migración productiva y sin cambios
funcionales.

### Evidencia de integridad

- SHA-256 canónico antes y después:
  `1B6E95CB2EDE734A7638E22D33DE6D0CB4DEA82230D372F0A6257421C3361FD1`.
- Tamaño y `LastWriteTimeUtc` canónicos también permanecieron iguales; no existían sidecars.
- La copia inicial tenía 14 migraciones, 0 programaciones y 0 tramos de rotación.
- Se creó un fixture pre-migración de dos tramos Trabajo solo en la copia para hacer
  observable el backfill. Los snapshots previo/posterior coinciden en ID, programación,
  orden, turno y días; ambos quedaron Trabajo y ninguno Descanso.
- `Up` terminó en 6,438 ms. History contiene la migración una vez, integridad `ok`, cero
  violaciones FK, FK/índices preservados y cuatro `CHECK` activos.
- Las cuatro pruebas de constraints dieron el resultado esperado.

### Evidencia funcional

- La rotación controlada previa siguió DIU, DIU, NOC, NOC.
- La nueva rotación resolvió DIU, DIU, Descanso, NOC, NOC, SinProgramacion.
- BD-01: descanso vacío en cero; 07:00–17:00 = 600 minutos HE100; una marca = incompleto
  y cero; NOC anterior no contamina; 20:00–02:00 = 360 minutos.
- Domingo y feriado ordinario preservaron una base física. El 1 de mayo conservó la
  clasificación especial y bloqueó la liquidación incompleta como estaba previsto.
- P1-04 marcó desactualizado después de cambiar programación y vigente después de
  recalcular. El piloto acotado pasó de 0 a 240 filas; la repetición dejó 240 filas.
- Reportes mostraron `DESCANSO PROGRAMADO`.
- Suite final del producto: 803/803; dominio 368/368, infraestructura 435/435, cero
  fallos y cero omitidos.

### Down, riesgo y bloqueantes

- En copia secundaria sin descansos: `Up` 6,014 ms y `Down` 6,205 ms, ambos con salida 0;
  el esquema anterior, las dos filas, la integridad y las FK quedaron conformes.
- EF advierte que `PRAGMA foreign_keys = 0` no se ejecuta dentro de transacción. El
  despliegue necesita parada de escrituras, respaldo probado y verificación inmediata.
- La aplicación Web arrancó contra la copia y la vista implementa las transiciones de
  Trabajo/Descanso, pero los navegadores de automatización no pudieron alcanzar localhost;
  el smoke UI interactivo no está cerrado.
- La base canónica no tiene rotaciones reales, por lo que no se validó una rotación
  histórica representativa.
- Rollback recomendado una vez creados descansos: restaurar backup, no ejecutar `Down`.

Documento: `OP_02_ROT_DESC_DEPLOYMENT_REHEARSAL.md`. Estado: **OP-02 NO VALIDADO PARA
PRODUCCIÓN; BASE CANÓNICA INTACTA; MIGRACIÓN PRODUCTIVA NO EJECUTADA**.

---

## Actualización de contexto — OP-02B Ensayo representativo ROT-DESC

Fecha: 2026-10-07  
Alcance: inventario de copias disponibles, dataset pre-migración controlado representativo,
comparación funcional, restore real y smoke Web autenticado. Sin producción ni cambios de
reglas.

### Datos y migración

- Se inventariaron 15 bases/copias. Ninguna contiene rotaciones históricas reales
  representativas: los backups antiguos no tienen el esquema y las copias actuales reales
  tienen cero programaciones/tramos. Las únicas bases con rotaciones previas eran fixtures
  de OP-02.
- Se creó evidencia **CONTROLADA REPRESENTATIVA** con cinco cuadrillas, siete
  programaciones, trece tramos Trabajo, vigencias cerradas y futura, cinco asignaciones,
  dos marcaciones y un tareo relacionado.
- Fuente y backup: SHA-256
  `3C38C81BCEBBB58C161D55E0CA65A5137EDD3B142C8FE5C1A825EB99A485BB2B`;
  integridad `ok`, cero violaciones FK.
- Snapshot pre/post: SHA-256
  `2FE173142900D3ED716C99520EAC2B4CE95D85C7498C98BE1CB7F69872BEC48B`.
  Las 34 resoluciones coinciden; cero diferencias; los 13 tramos quedaron Trabajo.
- La migración terminó con salida 0 en 5,752 ms. Persiste la advertencia operativa de EF:
  `PRAGMA foreign_keys = 0` no es transaccional.
- La nueva secuencia finita resolvió DIU, DIU, DescansoProgramado, NOC, NOC,
  SinProgramacion.

### Restore real

- Una copia post-migración se marcó como estado no confiable y luego fue reemplazada
  realmente por el backup pre-migración.
- SHA-256 restaurado idéntico al backup; integridad `ok`; FK 0; migración objetivo ausente;
  7 programaciones, 13 tramos y 1 tareo recuperados; marcador no confiable ausente.
- Duración aproximada, incluida validación: 57.667 ms. Es evidencia local, no SLA.

### Smoke Web autenticado

- Método: navegador IAB sobre ASP.NET Core real y copia migrada; rol `COORDINADOR`.
- UI-ROT-01 y UI-ROT-07 pasan.
- UI-ROT-02 falla: seleccionar Descanso deja Turno habilitado y Días vacío/editable. La
  vista usa script inline mientras la CSP exige `script-src 'self'`; se registra como causa
  probable.
- UI-ROT-05 falla: el POST autenticado válido `DIU ×2, DESC ×1, NOC ×2` vuelve al formulario
  con alerta genérica y no persiste.
- UI-ROT-03/04 no quedan validados porque el rechazo genérico también afecta el caso válido;
  UI-ROT-06 queda bloqueado por UI-ROT-05. El render auxiliar de Detalle sí mostró una
  secuencia equivalente cargada de forma controlada.

### Suite y decisión

- Suite final: 803/803 verdes; dominio 368/368; infraestructura 435/435; cero fallos y cero
  omitidos.
- SHA-256 final de la base canónica:
  `1B6E95CB2EDE734A7638E22D33DE6D0CB4DEA82230D372F0A6257421C3361FD1`.
- Los bloqueantes de datos representativos y restore quedaron cerrados; el smoke autenticado
  sigue abierto. Se requiere corregir el formulario Web, añadir cobertura MVC y repetir
  UI-ROT-01..07 antes de OP-03.

Documento: `OP_02B_ROT_DESC_REPRESENTATIVE_REHEARSAL.md`. Estado: **OP-02B NO VALIDADO;
BASE CANÓNICA INTACTA; MIGRACIÓN PRODUCTIVA NO EJECUTADA; OP-03 AÚN NO HABILITADO**.

---

## Actualización de contexto — R-WEB-ROT-DESC

Fecha: 2026-10-07  
Alcance: Web/MVC, binding, validación condicional, JavaScript/CSP y pruebas HTTP reales.

### Causas y corrección

- UI-ROT-02: `Programar.cshtml` contenía script inline mientras la CSP exigía
  `script-src 'self'`. La lógica se movió a `wwwroot/js/programacion-cuadrilla.js`; no se
  relajó la CSP y no se agregaron handlers inline, `eval` ni `new Function`.
- UI-ROT-05: MVC enlazaba directamente `ProgramarCuadrillaRequest`, cuya colección era
  `IReadOnlyList`; los campos hijos quedaban `Unvalidated` y `ModelState.IsValid=false` sin
  errores. Se añadió un ViewModel Web mutable con `List`, validación condicional y mapeo al
  DTO de Application después de validar.
- Descanso limpia/deshabilita Turno, envía Días=1 mediante `readonly` y el servidor rechaza
  tanto Descanso+TurnoId como Descanso+Días2 sin persistencia.

### Evidencia

- Cuatro pruebas MVC reales: login `COORDINADOR`, cookie, antiforgery, action auténtica,
  SQLite temporal verificada y autorización global intacta.
- POST válido: HTTP 302; GET Detalle: HTTP 200; tres tramos exactos DIU 2, Descanso 1,
  NOC 2.
- Smoke IAB sobre copia migrada: UI-ROT-01..07 pasan; reporte muestra
  `DESCANSO PROGRAMADO`; consola con cero errores CSP/JS.
- Suite: Domain 368/368, Infrastructure 435/435, Web 4/4; total **807/807**, cero fallos y
  cero omitidos.

### Trazabilidad operativa

- La base canónica local fue migrada accidentalmente durante el diagnóstico y recuperada
  lógicamente con `Down`: 14 migraciones, objetivo ausente, integridad `ok`, FK 0, cero
  programaciones/tramos. El rebuild cambió su SHA final a
  `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928`; ya no se afirma
  identidad byte a byte.
- Otro arranque abrió accidentalmente `C:\ProgramData`. Los cambios estaban solo en WAL;
  con autorización expresa se respaldaron y retiraron los sidecars. El archivo principal
  conservó SHA `0E91899A86D8534A1D3770056AC5D7B8BED5C8A6A242CCDB80E3BC552F3A0734`,
  8 migraciones, integridad `ok`, FK 0 y cero sidecars finales. No quedó migración
  productiva persistente.

Documento: `R_WEB_ROT_DESC_FORM_FIX.md`. Estado actualizado: **OP-02B VALIDADO; OP-03 LISTO
PARA PREPARACIÓN Y NO EJECUTADO**.

---

## Actualización de contexto — OP-03A Readiness productivo ROT-DESC

Fecha: 2026-10-09  
Alcance: release, historial read-only, cadena completa en copia, piloto, storage y alcance de
cuenta. Sin parada, migración, recálculo ni credenciales productivas.

### Release y baseline

- El ZIP del 24/09/2026 quedó rechazado y no se sobrescribió.
- Release frozen: `ControlMarcacion_ROT_DESC_20261009_op03a01.zip`; SHA-256
  `29B8F78BD7041A6C82F649799DD034EBBE29352447DC98E3C7712532DCA4C1DE`.
- EXE SHA-256: `0A5B17D93286A820AAFF710A06379A6F7A59FA31938FE5C5EA52542DEA2EEA2C`.
- Build Release: 0 errores/0 warnings. Suite: Domain 368, Infrastructure 435, Web 4;
  total 807/807, 0 fallos, 0 omitidos.
- El árbol Git es dirty; la identidad exacta queda fijada por `source-manifest.json` y SHA
  `93D90D9CD7DCFE2C7ACEA81D7430E81FCDCEC4FA033F2FAF0B626FC8B4B35391`, además del
  commit base `92d9177b1fad4a78f639d6ce7dc1dd77b08ef489`.
- El paquete se saneó para no incluir la credencial presente en la configuración de desarrollo.
  No contiene DB, tests, logs, obj ni artifacts anidados.

### Historial y cadena real

- El archivo candidato `C:\ProgramData\ControlMarcaciones\ControlMarcacion.db`, consultado con
  `sqlite3 -readonly`/`query_only`, conserva SHA
  `0E91899A86D8534A1D3770056AC5D7B8BED5C8A6A242CCDB80E3BC552F3A0734`, integridad
  `ok`, FK 0 y ocho migraciones; última: `20260923140604_RotacionPorCuadrilla`.
- No se observó tarea, proceso, directorio instalado ni override de máquina; por eso la ruta
  coincide con default/evidencia previa pero no está confirmada como efectiva de una instancia.
- Pendientes exactas: JustificacionUnica, Incidencias, VersionDelPeriodo,
  VigenciaDelHorario, VigenciaDelArea, MarcacionesProvisionales y ROT-DESC.
- La cadena completa sobre copia terminó exit 0, history 15, target una vez,
  `integrity_check=ok`, FK 0 y conteos preexistentes idénticos. Backfills: 6/6 versiones de
  turno y 330/330 asignaciones de área. El warning no transaccional de ROT-DESC apareció.
- El EXE del release repitió la cadena sobre otra copia y dejó 15 migraciones, integridad `ok`
  y FK 0. El ZIP extraído arrancó y respondió `/estado`.
- El smoke autenticado no quedó cerrado: no hay credencial autorizada coincidente y no se creó
  ninguna. Un primer smoke demostró desvío por argumento de ruta sin quoting; sus archivos
  locales se preservaron, el comando se corrigió y la ruta exacta se verificó.

### Piloto, backup y cuenta

- Planta técnica propuesta: Id 3, `PLANTA 1`; 1 trabajador, 376 marcas recientes, 30 tareos,
  una cuadrilla activa y cero programaciones. Período Id 2 abierto, 2026-09-01..30, con
  inconsistencia entre código `2026-09` y nombre `octubre`. Requiere aprobación de negocio.
- Storage local de artifacts: 234,033,336,320 bytes libres, ratio 46,910.55×; write/read/delete
  y restore sintético conformes. Comparte volumen C:; F: no es accesible; falta destino final.
- Backend: COORDINADOR es global; ADMINISTRADOR se acota por `usuario_planta` y sin asignación
  queda fail-closed. Prueba focalizada 1/1 verde: procesa planta permitida y excluye otra.
- En la base candidata existen dos COORDINADOR y cero ADMINISTRADOR. No hay cuenta piloto
  válida y no se crearon/modificaron credenciales.
- Todas las personas de Release, Backup, Migración, validación, Smoke, Negocio, GO/NO-GO y
  Rollback siguen `PENDIENTE DESIGNACIÓN`.

Documento: `OP_03A_ROT_DESC_PRODUCTION_READINESS.md`. Estado actualizado:
**OP-03A NO VALIDADO; LISTO PARA PROGRAMAR VENTANA = NO; OP-03 NO EJECUTADO; MIGRACIÓN
PRODUCTIVA NO**.

---

## Actualización de contexto — Fase 7, inventario de hardening

Fecha: 2026-10-09. Se leyeron las fuentes maestras, ocho fichas modulares y los documentos
BD-01/BD-02/OP-02/OP-02B/R-WEB/OP-03/OP-03A exigidos. Build Release 0/0; pruebas
368 Domain + 435 Infrastructure + 4 Web = 807/807. Cobertura guardada en
`artifacts/phase7-hardening-20261009/coverage/`.

El inventario maestro `PHASE_07_HARDENING_INVENTORY.md` registra 23 hallazgos con contrato
de salida. Los dos P0 son secreto de bootstrap versionado/copiado a artefactos normales y
bases/backups SQLite sensibles en el árbol Git o sin exclusión. Nunca copiar valores secretos
al contexto. La base local del árbol tiene integridad `ok`, FK 0 y 307 trabajadores con más de
un `PeriodoLaboral` abierto; no confundirla con producción.

No se modificó código, no se repararon datos, no se ejecutó OP-03 y no se aplicó migración
productiva. Siguiente bloque: 7A, contención coordinada de seguridad y datos.

---

## Actualización de contexto — Fase 7A

Fecha: 2026-10-09. `appsettings.json` y builds actuales tienen bootstrap vacío. Desarrollo:
user-secrets; operación futura: `Seguridad__AdminCorreo` y `Seguridad__AdminClave`. Una base
vacía sin esos valores lanza `CredencialesBootstrapFaltantesException`. Tests nuevos: fail-safe
y guard de appsettings distribuido. Baseline: Domain 368, Infrastructure 436, Web 5; total
809/809; build 0 errores/0 warnings.

No copiar al contexto los valores históricos: solo máscaras `sis****.pe` y `Sis****027`.
El ZIP con dos entradas afectadas fue retirado y renombrado como histórico/comprometido en
cuarentena; rotación obligatoria antes de producción. Inventario de datos: 27 bases/backups +
48 sidecars = 75 archivos. Con el ZIP: 76/76 copias y hashes conformes, 76 originales retirados,
0 bloqueados y 0 sensibles restantes. Los cuatro antes rastreados salieron del índice actual.
Historia Git y producción intactas. Ver `PHASE_07A_SECURITY_DATA_CONTAINMENT.md` y
`PHASE_07A_SQLITE_INVENTORY.md`.

---

## Actualización de contexto — P1-STARTUP-HEALTH

Fecha: 2026-10-09. El defecto se confirmó: el `catch` de `MigrateAsync` continuaba y
`/estado` era 200 constante. Corrección: `IPreparadorAplicacion` concentra migración/bootstrap;
`/vivo` identifica proceso; `IVerificadorEstadoOperativo` hace readiness read-only con conexión,
set exacto de migraciones, `quick_check`, FK y tablas/columnas del modelo. `/estado` devuelve 503
con código genérico y se recupera a 200 cuando la base vuelve compatible, sin escribir.

SH-01..07 verdes. Build 0/0; suite 368 + 436 + 9 = 813/813. No se regeneraron DB en el árbol,
no se tocó producción ni OP-03. Documento: `P1_STARTUP_HEALTH.md`.

Actualización 2026-10-13: se cerró el hueco restante del modo NOT-READY. Un singleton
thread-safe marca Starting/Ready/Failed y el middleware devuelve 503
`INICIALIZACION_NO_COMPLETA` para rutas funcionales mientras la preparación no sea válida.
`/vivo` y `/estado` siguen disponibles; readiness compatible rehabilita el tráfico. Build 0/0;
suite 398 + 441 + 23 = 862/862. Producción y migraciones intactas.
# Registro 2026-10-12 — P1-DESKTOP-INSTALLER

Se añadió instalación Inno Setup self-contained win-x64, sin DB ni secretos, con ProgramData
persistente e instancia única. No se habilitó autostart, firewall ni despliegue productivo.
Se cerró con ciclo UAC real completo y DB preservada, política automática de logs
diaria/20 MB/30 días, build 0/0 y 855/855 pruebas verdes. Estado: **COMPLETADO**.

---

# Registro 2026-10-13 — P1-LOCAL-FIRST-RUN

La instalación real 1.0.0 estaba en `C:\Program Files\ControlMarcacion` y la base en
`C:\ProgramData\ControlMarcacion\data`. La DB tenía integridad `ok`, FK 0 y cero usuarios.
Con las variables `Seguridad__AdminCorreo`/`Seguridad__AdminClave` ausentes, el proceso lanzaba
`CredencialesBootstrapFaltantesException` y salía con `-532462766`; las migraciones, el puerto,
el descriptor y los ACL no eran la causa.

Se eligió wizard Web `/setup`, protegido con antiforgery, Identity, transacción serializable y
recomprobación de usuario único. Readiness diferencia Starting, SetupRequired, Ready y Failed;
un error técnico nunca habilita setup. Inno Setup dejó de recopilar credenciales. Build 0/0,
862/862 pruebas y smoke aislado válidos. Instalador 1.0.1 SHA-256:
`2F7DE84936E0F240B9BD462E26BD5C92EA933F5566D41025078FD4B483350D9C`.
El histórico 1.0.0 fue preservado. Pendiente: setup elegido por el usuario y ciclo elevado real;
producción, ProgramData y migraciones productivas intactas.

---

# Registro 2026-10-13 — P1-ZKTECO-SDK

Se inspeccionaron Standalone 6.3.1.37 y PULL 2.2.0.220. La API nativa x64
`ProcessBackupDataExt` procesa respaldos offline, pero en `BKtransaction(5).dat` solo reconoce
estructura: 981 filas y 0 marcaciones SDK válidas. El interno entrega 851 válidas, 130
rechazadas y 127 IDs. Se implementaron modos `Interno`, `Sdk` y `Comparar`, errores SDK
explícitos, comparación no persistente y artefactos por hash. `Interno` permanece autoritativo.

La dependencia queda confinada a Infrastructure y no se incorporó DLL al repositorio,
portable ni instalador. Redistribución: NO DETERMINADA por ausencia de licencia/EULA en el
paquete. Build 0 errores/0 warnings; suite 398 + 453 + 23 = 874/874. Producción y migraciones
no fueron tocadas.

---

# Registro 2026-10-13 — P1-DATA-PERIODOLABORAL Fase 3C

Se cerró la migración progresiva de consumidores históricos. `ConsultaContextoLaboral`
centraliza periodo, empresa y planta por fecha con precarga por rango y estados explícitos; los
reportes, el recálculo y la autorización de tareos ya no usan la ficha actual para decidir el
pasado. Preingreso, postcese y gaps producen `SinVinculo`; una justificación fuera del vínculo
se conserva sin generar pago. La jornada nocturna sigue gobernada por su día de inicio.

Evidencia: 26 contratos nuevos, build 0/0, suite final Domain 405 + Infrastructure 472 + Web 23
= 900/900. Copia 3B: 775 pares sin deltas, integridad `ok`, FK 0, `/vivo` y `/estado` 200
`LISTO`. No hubo producción, saneamiento/migración productivos, OP-03 ni commit. Estado máximo:
`LISTO PARA FASE 3D`.

La indicación anterior queda **SUPERADA** por las fases 3D y 3E-A..E.

---

# Registro 2026-10-15 — P1-DATA-PERIODOLABORAL cierre 3E-E

## Autoridad vigente

```text
PeriodoLaboral       = vínculo laboral y empresa históricos
AsignacionPlanta     = planta histórica dentro del vínculo
Trabajador.FechaIngreso / FechaCese / EmpresaId / PlantaId
                     = proyección actual y compatibilidad
```

Los planos no deben usarse como historia ni fallback. La identidad física de una marca sigue
siendo `EquipoId + IdMarcador + vigencia de AsignacionMarcador`.

## Trazabilidad cerrada

- **3B:** saneamiento y backfill exclusivamente sobre copia controlada. 352 trabajadores y
  913 periodos; 307 trabajadores con múltiples abiertos y 900 solapamientos quedaron en 0;
  307 grupos autocorregibles, 0 casos humanos; 352 plantas Nivel A y 0 B/C/D.
- **3C:** `ConsultaContextoLaboral` centralizó la resolución; reportes y tareo migraron a
  empresa/planta históricas; fallback histórico legacy = 0.
- **3D:** cese de cardinalidad exacta, historia completa en escrituras, eliminación de
  `Update(graph)`, persistencia selectiva, invariantes y rollback.
- **3E-A:** 36 usos legacy clasificados. Los cuatro planos quedaron definidos como proyección
  actual; cuatro usos históricos incorrectos quedaron identificados.
- **3E-B:** esos cuatro usos se corrigieron en asignación/integrantes/candidatos de cuadrilla y
  PMV; 11 contratos dirigidos.
- **3E-C:** reingreso Application/UI mínimo, atómico, autorizado, auditado y resistente al
  doble envío.
- **3E-D:** build 0/0, suite 937/937 y regresión dirigida 108/108; 0 regresiones.
- **3E-E:** reconciliación de `PROJECT_STATE`, `MASTER_DOSSIER`, `ARCHITECTURE`,
  `REMEDIATION_PLAN`, `AI_CONTEXT_MAP` y este registro. Los dossiers previos se conservaron.

## Afirmaciones superadas

- “`PeriodoLaboral` no es fuente del motor” → **SUPERADA**.
- “Motor/reportes usan campos planos deliberadamente” → **SUPERADA**.
- “Hay 85 grupos/170 filas pendientes” → **OBSOLETA** como dato operativo; queda como evidencia
  del corte antiguo.
- “Ejecutar DATA-PERIODOLABORAL / listo para Fase 3D” → **RESUELTA/SUPERADA**.

## Deuda residual explícita

1. Los cuatro campos planos y su dual-write permanecen por compatibilidad de UI, filtros,
   autorización actual e importación.
2. Edición de ficha y carga masiva todavía pueden expresar correcciones del vínculo abierto;
   separar comandos es una mejora futura.
3. SQLite protege un único periodo abierto, pero no todos los solapamientos entre periodos
   cerrados ni el rango completo de fechas; las guardas de dominio siguen siendo obligatorias.
4. `PeriodoLaboral` no tiene token propio de concurrencia. El índice parcial resuelve la carrera
   principal de doble apertura/reingreso.
5. P1-06 permanece solo para otros maestros históricos no congelados; vínculo, empresa y planta
   no forman parte de esa deuda residual.
6. Una operación sobre otra base debe repetir inventario, dry-run, clasificación, backup y
   verificaciones. Los conteos 3B no describen producción.

## Estado formal

`P1-DATA-PERIODOLABORAL = CERRADO`.

## Registro 2026-10-16 — P1-BUSINESS-RULES-FINAL

- Se creó `BUSINESS_RULES_FINAL.md` como autoridad funcional vigente.
- Se reconciliaron 95 entradas: 69 `VIGENTE_IMPLEMENTADA`, 11 `VIGENTE_PARCIAL`,
  4 `VIGENTE_NO_IMPLEMENTADA`, 2 `NO_DETERMINADA`, 1 `HISTORICA_OBSOLETA` y 8 `SUPERADA`.
- Las 86 reglas operativas cubren horarios, tardanza, salida, HE, sábado/domingo,
  nocturnidad, rotación/descanso, feriados, ausencias, marcaciones/importación,
  PeriodoLaboral/AsignacionPlanta, cuadrillas, PMV, reportes, seguridad y auditoría.
- `BUSINESS_RULES.md` queda preservado como corte histórico e índice de IDs legados.
- No se tocaron código, tests, migraciones, datos ni producción; OP-03 no se ejecutó.

Producción: no tocada. Migración productiva: no. OP-03: no ejecutado. Commit: no.

---

# Registro 2026-10-19 — P0-FER-002 + P0-FER-007

- Fuente interna: FER-NAC-01..08, BD02-D01..D09, FER-002 y FER-007.
- Fuente externa contrastada: D. Leg. 713 arts. 8/9 y D.S. 012-92-TR art. 9; el MTPE
  confirma cuatro componentes del 1 de mayo trabajado coincidente con descanso y dos si no
  se trabaja.
- `ConceptoLiquidado` separa `MinutosBaseEconomica` y `MinutosRecargo`; no multiplica la
  cantidad física.
- `ComponenteRemuneracionDiaria` expresa derechos en días-persona, separados de
  `TotalHorasEquivalentes`.
- `CalculoTareo` persiste el tipo feriado también sin trabajo y conserva la coincidencia del
  1 de mayo para descanso programado, descanso por turno o domingo.
- El bloqueo preventivo del 1 de mayo se sustituyó por la fórmula auditable aprobada.
- 15/15 contratos dirigidos; suite 958/958; build Release 0/0.
- FER-001/002/004/007 cerrados. FER-003 (sustitutorio) y FER-005 (día compensable) siguen
  abiertos y no se inventaron datos.
- Producción, OP-03, migraciones productivas y commit: no.


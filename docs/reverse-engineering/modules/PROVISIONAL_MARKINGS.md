# Módulo — Marcaciones Provisionales (PMV)

Fecha de corte: 2026-10-05  
Carácter: **provisional y separado**. No forma parte del motor principal de marcaciones/tareo.

## Ficha consolidada de Fase 6

### Responsabilidades

- Reutilizar lectores ZKTeco TXT/DAT y registrar importación, marcas y errores en `prov_*`.
- Atribuir dinámicamente sin persistir trabajador.
- Consultar y exportar bajo límites de rango.
- Permanecer aislado de importación principal y tareo oficial.

### Entidades

`ImportacionProvisional`, `MarcacionProvisional` y `ErrorImportacionProvisional`.

### Servicios

`ServicioImportacionProvisional`, `ServicioConsultaMarcacionProvisional`, `ExportadorMarcacionesProvisionales` y `MarcacionesProvisionalesController`.

### Reglas

- 1–30 días: normal; 31–40: advertencia; 41+: consulta permitida y exportación bloqueada.
- No persiste `TrabajadorId` ni escribe `ImportacionMarcador`, `MarcacionCruda` o `TareoDiario`.
- El alcance por planta se aplica a listado/historial, importación y consulta; exportación reutiliza la consulta protegida.

### Flujo

`.dat/TXT` → `IFuenteDeMarcaciones` → importación y tablas `prov_*` → atribución temporal → consulta por rango → Excel cuando el rango lo permite.

### Fuentes de verdad

Archivo/hash para importación; tablas `prov_*` para staging; equipo, asignación y fecha para atribución; `ACCEPTANCE_RULES.md` y `REMEDIATION_PLAN.md` para el P0 de seguridad.

### Archivos críticos

- `src/ControlMarcacion.Infrastructure/MarcacionesProvisionales/ServicioImportacionProvisional.cs`
- `src/ControlMarcacion.Infrastructure/MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs`
- `src/ControlMarcacion.Infrastructure/MarcacionesProvisionales/ExportadorMarcacionesProvisionales.cs`
- `src/ControlMarcacion.Web/Controllers/MarcacionesProvisionalesController.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Configuraciones/ConfiguracionesMarcacionesProvisionales.cs`

### Pruebas y baseline vigente

Pruebas: `ImportacionProvisionalTests`, `ConsultaMarcacionProvisionalTests`, `ExportadorMarcacionesProvisionalesTests`, `SyntheticPerformanceBaselineTests` y `AislamientoPmvAcceptanceTests` (SEC-01..04 verdes). Falta una prueba de controlador extremo a extremo, no el contrato de backend.

El baseline sintético de Fase 5 reemplaza mediciones físicas cacheadas anteriores: 1k=295.56 ms/6.33 MB; 20k=5,187.13 ms/65.66 MB; consultas 1/30/31/40/41 días=127.17/1.65/6.72/2.00/4.57 ms. Es una ejecución sintética, no SLA; un día incluye arranque frío.

### Problemas conocidos

- SEGURIDAD/P0: **RESUELTO** con alcance compartido en backend.
- DEUDA/P2: sin cobertura de controlador extremo a extremo.
- RIESGO: consulta amplia puede consumir recursos aunque la exportación se bloquee.
- DEUDA/P3: falta criterio ejecutable de retiro.

Véase [D14](../diagrams/D14_PMV_DATA_FLOW.md) y [Marcaciones](MARKINGS.md).

## Propósito confirmado

PMV permite cargar un archivo ZKTeco, conservar sus marcas en tablas propias, cruzarlas en consulta con el padrón histórico de marcador, agruparlas por persona/ID y día, y exportar un Excel sencillo. No calcula tardanza, horas extra, nocturnidad, incidencias ni tareo.

```text
TXT / attlog.dat / BKtransaction.dat
→ detección por contenido y lector compartido
→ SHA-256 y deduplicación
→ prov_importacion + prov_marcacion + prov_error_importacion
→ atribución dinámica por equipo + ID + vigencia
→ jornada calendario (primera/última marca)
→ vista o Excel
```

## Componentes

| Capa | Componentes reales |
|---|---|
| Domain | `ImportacionProvisional`, `MarcacionProvisional`, `ErrorImportacionProvisional` |
| Application | `IServicioImportacionProvisional`, `IServicioConsultaMarcacionProvisional`, `IExportadorMarcacionesProvisionales`, DTO de carga/consulta |
| Infrastructure | `ServicioImportacionProvisional`, `ServicioConsultaMarcacionProvisional`, `ExportadorMarcacionesProvisionales`, configuraciones EF |
| Web | `MarcacionesProvisionalesController`, `MarcacionesProvisionalesVm`, `Views/MarcacionesProvisionales/Index.cshtml` |
| Compartido | `IFuenteDeMarcaciones` → `LectorMarcadorAutomatico` → lector TXT o respaldo binario |

## Casos de uso y rutas

| Acción | Método | Efecto |
|---|---|---|
| Abrir módulo | `GET Index` | lista equipos activos e historial PMV |
| Cargar | `POST Importar` | valida archivo/equipo, calcula hash, decodifica y guarda en transacción |
| Consultar | `GET Consulta` | obtiene marcas por equipo/rango, atribuye y agrupa |
| Exportar | `GET Exportar` | repite consulta y genera XLSX si el rango no supera 40 días |

Autorización de clase: roles `COORDINADOR` o `ADMINISTRADOR`. El formulario de carga exige antiforgery y declara límite de 20 MiB.

Desde R-P0-01, `ConsultaCatalogos`, `ServicioImportacionProvisional` y `ServicioConsultaMarcacionProvisional` consumen `IProveedorAlcancePlantas`. Catálogo e historial se filtran en la consulta; importación y consulta rechazan un equipo fuera de alcance como inexistente/inactivo. La exportación reutiliza la consulta protegida, por lo que una URL manipulada no evita el control. Los roles con alcance global conservan la conducta previa.

## Persistencia

| Tabla | Responsabilidad | Claves y protección |
|---|---|---|
| `prov_importacion` | cabecera, hash, usuario y contadores | FK equipo; índices hash y equipo+fecha; hash no único |
| `prov_marcacion` | hecho provisional inmutable | FK equipo/importación; unique equipo+ID+instante |
| `prov_error_importacion` | rechazo o duplicado de línea | FK importación con cascade |

Estado canónico observado:

- 2 importaciones;
- 731 marcas en cuatro fechas;
- 761 errores/rechazos;
- 0 FK rotas;
- 0 hashes duplicados;
- 0 marcas duplicadas por clave natural;
- 0 diferencias entre contadores y filas físicas.

Las dos escrituras de la importación PMV están dentro de una transacción explícita. Si falla el guardado de marcas, la cabecera y sus errores se revierten con ella.

## Frontera de aislamiento

PMV escribe exclusivamente sus tres tablas. Lee:

- `equipo_marcador`, para validar y describir el origen;
- `asignacion_marcador`, para atribuir por fecha;
- `trabajador`, `planta`, `area` y `centro_costo`, para enriquecer;
- `asignacion_turno`, `turno` y `cuadrilla`, para la etiqueta de turno.

No lee ni escribe `marcacion_cruda`, `importacion_marcador`, `tareo_diario` o `periodo`; tampoco escribe trabajadores o asignaciones. Las entidades PMV están excluidas del interceptor general, por lo que no añaden filas a `registro_auditoria`.

## Archivo, hash y duplicados

- El hash es SHA-256 de todos los bytes del stream, representado por 64 caracteres hexadecimales minúsculos.
- Se calcula antes de decodificar y se busca globalmente en el historial PMV.
- Un hash existente produce una excepción de negocio.
- El mismo lote elimina duplicados por `(IdMarcador, FechaHora)`.
- La base impide repetir `(equipo_id, id_marcador, fecha_hora)`.

Clasificación: **PROTEGIDO POR APLICACIÓN** para archivo; **PROTEGIDO POR BD + APLICACIÓN** para marcas. Falta unique físico del hash, por lo que cargas simultáneas o archivos sin marcas válidas conservan una ventana de carrera.

## Soporte `.dat`

La extensión no decide el formato. `LectorMarcadorAutomatico` inspecciona el contenido:

- un attlog tabulado, aunque termine en `.dat`, se envía a `LectorTxtZkteco`;
- una cabecera de respaldo válida se envía a `LectorRespaldoZkteco`.

El respaldo binario contiene una cabecera con serie, modelo y esquema. El lector deduce el desplazamiento de campos y valida timestamps. Limitaciones conocidas: el ID entero pierde ceros iniciales y no se conoce con certeza la semántica de todos los campos intermedios. PMV no duplica este parser.

## Atribución y resultado diario

La atribución usa `equipo_id + id_marcador + vigencia inclusiva de asignacion_marcador`. No se guarda `trabajador_id` en la marca. Después agrupa por fecha calendario e ID:

- una marca → ingreso presente, salida nula;
- dos o más → primera como ingreso, última como salida;
- más de dos → observación con cantidad;
- sin asignación → mantiene la jornada y la rotula sin dueño.

En la base canónica hay 275 marcas/165 jornadas sin dueño y cero atribuciones ambiguas. Esa ausencia no prueba por sí sola que el trabajador usó el equipo incorrecto.

El cruce histórico solo alcanza la asignación del marcador y la asignación de turno. Planta, área y centro de costo proceden de la ficha actual de trabajador; `AsignacionArea` no se consulta. Si la asignación es por cuadrilla, el campo “Turno” recibe el nombre de cuadrilla, no el turno rotativo del día.

## Rangos

| Días inclusivos | Consulta | Exportación |
|---:|---|---|
| 1–30 | permitida | permitida |
| 31–40 | permitida con advertencia | permitida con advertencia |
| 41 o más | permitida con advertencia | bloqueada en controller y oculta en vista |

El backend rechaza rangos invertidos. No existe un máximo de consulta; 40 días es solo el máximo de exportación.

## Excel

Hoja `Marcaciones`, 13 columnas:

1. Fecha
2. ID marcador
3. DNI
4. Apellido paterno
5. Apellido materno
6. Nombres
7. Hora ingreso
8. Hora salida
9. Turno
10. Planta
11. Área
12. Centro de costo
13. Observación

No existe columna `Unidad`. La hoja `Trazabilidad` añade fecha de generación, equipo, rango, total, usuario y advertencias. Los textos potencialmente interpretables como fórmulas se neutralizan.

## Pruebas localizadas

Se declararon 35 pruebas funcionales PMV y cuatro contratos de seguridad:

- 12 de importación: inserción, hash, dedupe, validaciones, historial y errores;
- 13 de consulta: vigencia, sin asignación, agrupación, rangos, no escritura y turno;
- 10 de exportación: hojas/columnas, datos, trazabilidad, orden, vacío, anti-fórmula y área/CECO.
- SEC-01..04: listado/historial acotado, bloqueo de consulta/exportación, bloqueo de importación y operación autorizada.

Las 39 pruebas PMV/seguridad dirigidas pasan. No existe aún una prueba de controlador Web extremo a extremo; el bloqueo efectivo está en los servicios de backend que usa el controlador.

## Performance observada

Con 731 marcas, la lectura de rango de un equipo devolvió entre 2 y 380 filas y tuvo medianas de 0.04–0.35 ms en SQLite con caché caliente. Es evidencia suficiente para clasificar el volumen actual como normal, no para extrapolar crecimiento.

El plan usa el prefijo por equipo del unique `(equipo, ID, fecha)` y un B-tree temporal para ordenar por fecha. La consulta y el XLSX se materializan en memoria. Ambos son puntos de observación futura, no problemas confirmados hoy.

## Riesgos y decisiones pendientes

- Definir tratamiento de las 165 jornadas sin dueño.
- Mantener el contrato de alcance por planta y añadir cobertura de controlador si se crea un proyecto Web de pruebas.
- Decidir si la organización histórica debe reflejar la fecha de marca.
- Decidir qué debe significar “Turno” cuando el trabajador pertenece a una cuadrilla.
- Confirmar si consultar más de 40 días debe seguir permitido.
- Decidir si el hash requiere unique físico y qué alcance debe tener.
- Mantener PMV separado hasta una decisión explícita de producto.

Ver también `../PHASE_03B_DATABASE_VALIDATION.md` y `../diagrams/D14_PMV_DATA_FLOW.md`.

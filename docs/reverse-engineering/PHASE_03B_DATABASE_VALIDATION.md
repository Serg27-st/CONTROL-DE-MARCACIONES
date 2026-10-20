# Fase 3B — Validación física de SQLite e incorporación de PMV

Fecha de corte: 2026-10-05  
Alcance: base canónica `ControlMarcacion.db`, integridad real, planes SQLite, riesgo transaccional de la importación principal y módulo Marcaciones Provisionales (PMV).  
Estado: auditoría de solo lectura; no se iniciaron la aplicación, migraciones, semillas ni reparaciones.

## 1. Resumen ejecutivo

- `[CONFIRMADO POR ESQUEMA]` El modelo EF vigente, su snapshot, las 14 migraciones manuales y la base canónica son **CONSISTENTES**. El snapshot espera 47 tablas de aplicación y 87 índices explícitos; todos están presentes. SQLite contiene además `__EFMigrationsHistory` y `__EFMigrationsLock`, para un total físico de 49 tablas no internas.
- `[CONFIRMADO POR DATOS]` `PRAGMA integrity_check` devolvió exactamente `ok` y `PRAGMA foreign_key_check` devolvió cero filas.
- `[CONFIRMADO POR DATOS]` No se encontraron huérfanos en las FK físicas ni en las referencias escalares críticas sin FK revisadas.
- `[CONFIRMADO POR DATOS]` La anomalía principal de datos son 85 grupos abiertos duplicados en todos los campos de negocio comparados de `periodo_laboral`: 170 filas, dos por trabajador afectado. Los campos planos de `trabajador` coinciden con esas filas; no se observó divergencia empresa/fecha, sino duplicación.
- `[CONFIRMADO POR CÓDIGO]` PMV es un módulo separado de primer nivel. Escribe únicamente `prov_importacion`, `prov_marcacion` y `prov_error_importacion`; no escribe marcaciones, tareo, asignaciones, trabajadores ni periodos principales.
- `[CONFIRMADO POR CÓDIGO]` PMV exige rol Coordinador o Administrador, pero no aplica `IProveedorAlcancePlantas`: un Administrador limitado a plantas puede listar historial, cargar y consultar cualquier equipo activo. Es una brecha de aislamiento de lectura/escritura dentro de PMV, no una escritura en tablas principales.
- `[CONFIRMADO POR DATOS]` PMV contiene 2 importaciones, 731 marcaciones y 761 errores/rechazos. Sus contadores cuadran, no hay hashes repetidos ni claves naturales duplicadas y sus FK están íntegras.
- `[CONFIRMADO POR DATOS]` 275 marcas PMV, agrupadas en 165 jornadas, no encuentran una `asignacion_marcador` vigente. No existen atribuciones ambiguas. El dato sigue siendo una marcación válida sin dueño resuelto; la causa de negocio no puede distinguirse solo con la BD.
- `[CONFIRMADO POR CÓDIGO]` La deduplicación de archivo PMV usa SHA-256 sobre todos los bytes del stream, pero `hash_contenido` solo tiene índice no único. La clasificación es **PROTEGIDO POR APLICACIÓN**, con una ventana de carrera estructural.
- `[CONFIRMADO POR CÓDIGO]` La importación PMV envuelve sus dos `SaveChangesAsync` en una transacción explícita. La importación principal persiste cabecera/errores y marcas en dos commits sin transacción envolvente; puede dejar una cabecera que declara inserciones pero no tiene sus marcas.
- `[CONFIRMADO POR DATOS]` Los planes representativos usan índices en hash, atribución, vigencias, periodos y rangos. La consulta PMV por equipo+rango usa solo el prefijo `equipo_id` del índice único y un B-tree temporal para ordenar; con 731 filas el tiempo observado es submilisegundo y no constituye hoy un problema confirmado.

Comprensión global reevaluada: **83 %**. La persistencia física y PMV quedan explicados con evidencia; siguen pendientes la validación exhaustiva de reglas de cálculo, linaje completo de reportes, seguridad operativa y comportamiento con volumen representativo.

## 2. Base canónica y método seguro

| Metadato | Valor observado antes de abrir SQLite |
|---|---|
| Ruta | `C:\Users\stafur\Documents\CONTROL DE MARCACIONES\src\ControlMarcacion.Web\ControlMarcacion.db` |
| Tamaño | 2,256,896 bytes |
| Fecha de modificación | 2026-10-05 09:01:27 -05:00 |
| SHA-256 | `d4bcdab88d74668b74de123346b941f17fb86d103bb07ce1a81fb119b9a3effa` |
| `.db-wal` inicial | ausente |
| `.db-shm` inicial | ausente |

Método:

- conexión URI SQLite con `mode=ro`;
- `PRAGMA query_only=ON` en cada conexión;
- consultas agregadas sin extraer nombres, documentos ni otra PII;
- no se inició `Program.cs`, no se ejecutó `dotnet ef`, migraciones, seed ni SQL de escritura;
- los benchmarks son lecturas repetidas de una consulta, no una prueba de carga;
- el hash y la fecha del archivo principal se comprobaron antes y después y permanecieron idénticos.

Nota operativa: `[CONFIRMADO POR DATOS]` la primera apertura de una base configurada con `journal_mode=wal` creó un `-wal` de 0 bytes y un `-shm` de 32,768 bytes aun usando `mode=ro`; el archivo principal no cambió. Son archivos de coordinación de SQLite, no cambios de datos. Su retiro quedó fuera de la auditoría porque requiere autorización destructiva explícita.

## 3. Modelo, snapshot, migraciones y base

Valores físicos:

| Comprobación | Resultado |
|---|---|
| SQLite de la herramienta | 3.53.1 |
| `PRAGMA user_version` | 0 |
| `PRAGMA journal_mode` | `wal` |
| `PRAGMA foreign_keys` en la conexión de auditoría | 0 |
| `PRAGMA integrity_check` | `ok` |
| `PRAGMA foreign_key_check` | 0 filas |
| Tablas de aplicación esperadas por snapshot | 47 |
| Tablas físicas de aplicación | 47 |
| Tablas físicas incluyendo metadatos EF | 49 |
| FK físicas | 45 |
| Índices explícitos | 87 |
| Índices explícitos únicos | 32 |
| Índices parciales | 9 |
| Checks de negocio | 1 |
| Triggers / vistas | 0 / 0 |

`user_version=0` no es drift: EF Core registra versión mediante `__EFMigrationsHistory`, no mediante ese pragma. `foreign_keys=0` es un estado por conexión de la biblioteca de auditoría: no altera ni invalida `foreign_key_check`, pero tampoco demuestra cuál es el valor efectivo de las conexiones abiertas por Microsoft.Data.Sqlite en ejecución. La cadena configurada no declara `Foreign Keys=True`; verificar ese valor en una instancia controlada queda pendiente operativo.

### 3.1 Migraciones aplicadas

`[CONFIRMADO POR DATOS]` `__EFMigrationsHistory` contiene, en orden:

1. `20260817165631_EsquemaInicial`
2. `20260910150246_EquipoMarcadorComoIdentidad`
3. `20260921143551_PadronesPlanta18Set`
4. `20260921162221_PuestosGenerales`
5. `20260921185600_AtribucionDeMarcacionesAlCalcular`
6. `20260922205054_GraciaDeSalidaPorTurno`
7. `20260922205415_OrigenVerificadoDeLaCarga`
8. `20260923140604_RotacionPorCuadrilla`
9. `20260924194824_JustificacionUnicaPorDiaYLicenciaSinGoce`
10. `20260924201125_IncidenciasYRegularizaciones`
11. `20260925142617_VersionDelPeriodo`
12. `20260925143248_VigenciaDelHorarioDeTurno`
13. `20260925150116_VigenciaDelAreaDelTrabajador`
14. `20261001212252_MarcacionesProvisionales`

No existe en el repositorio ni en el historial una segunda migración PMV de renombrado. La mención a `NombreTablasProvisionalesSnakeCase` en documentación funcional está obsoleta.

### 3.2 Comparación física

| Objeto | Esperado por EF/snapshot | Existe en DB | Diferencia |
|---|---:|---:|---|
| Tablas de aplicación | 47 | 47 | ninguna |
| Índices explícitos por nombre | 87 | 87 | ninguna |
| FK | 45 | 45 | ninguna por inventario |
| PMV: tablas | 3 | 3 | ninguna |
| PMV: índices | 6 | 6 | ninguna |
| Check `CK_asignacion_turno_turno_o_cuadrilla` | 1 | 1 | ninguna |
| Migraciones manuales actuales | 14 | 14 aplicadas | ninguna |

Clasificación: **CONSISTENTE**.

La subida de 44 a 47 tablas frente a Fase 3A es evolución legítima: se añadieron las tres tablas PMV después del corte anterior.

## 4. PMV funcional → técnico

| Concepto PMV | Clase real | Tabla | Servicio | Consumidor | Estado |
|---|---|---|---|---|---|
| Evento de carga | `ImportacionProvisional` | `prov_importacion` | `ServicioImportacionProvisional` | controller e historial Razor | CONFIRMADO |
| Marca provisional | `MarcacionProvisional` | `prov_marcacion` | importación y consulta provisional | agrupación diaria/exportador | CONFIRMADO |
| Error/rechazo | `ErrorImportacionProvisional` | `prov_error_importacion` | importación provisional | persistencia; no se lista en la vista actual | CONFIRMADO |
| Contrato de carga | `IServicioImportacionProvisional` | dos tablas PMV | `ServicioImportacionProvisional` | `MarcacionesProvisionalesController` | CONFIRMADO |
| Contrato de consulta | `IServicioConsultaMarcacionProvisional` | `prov_marcacion` + maestros leídos | `ServicioConsultaMarcacionProvisional` | controller | CONFIRMADO |
| Contrato de exportación | `IExportadorMarcacionesProvisionales` | ninguna escritura | `ExportadorMarcacionesProvisionales` | acción `Exportar` | CONFIRMADO |
| Entrada HTTP | `MarcacionesProvisionalesController` | indirecta | tres contratos PMV + catálogos | usuarios COORDINADOR/ADMINISTRADOR | CONFIRMADO |
| Modelo de página | `MarcacionesProvisionalesVm` | ninguna | composición del controller | `Views/MarcacionesProvisionales/Index.cshtml` | CONFIRMADO |
| Vista | `Index.cshtml` | ninguna | formularios Importar/Consulta/Exportar | navegador | CONFIRMADO |

`[CONFIRMADO POR CÓDIGO]` El módulo está registrado en DI, tiene DbSets propios, enlace de navegación y autorización de servidor para Coordinador o Administrador.

## 5. Frontera PMV ↔ sistema principal

| Tabla/módulo principal | Importación PMV | Consulta PMV | Exportación PMV | Clasificación |
|---|---|---|---|---|
| `marcacion_cruda` | — | — | — | NINGUNA |
| `importacion_marcador` | — | — | — | NINGUNA |
| `tareo_diario` | — | — | — | NINGUNA |
| `asignacion_marcador` | — | resuelve trabajador por equipo+ID+vigencia | resultado ya proyectado | LECTURA |
| `asignacion_turno` | — | resuelve turno directo o nombre de cuadrilla | resultado ya proyectado | LECTURA |
| `asignacion_area` | — | — | — | NINGUNA |
| `trabajador` | — | documento, nombres y IDs organizativos actuales | resultado ya proyectado | LECTURA |
| `periodo` | — | — | — | NINGUNA |
| `equipo_marcador` | valida equipo activo y muestra catálogo | valida/describre equipo | descripción | LECTURA |
| `planta`, `area`, `centro_costo`, `turno`, `cuadrilla` | — | enriquecimiento | resultado ya proyectado | LECTURA |
| `prov_importacion` | C | R | — | LECTURA/ESCRITURA propia |
| `prov_marcacion` | C | R | — | LECTURA/ESCRITURA propia |
| `prov_error_importacion` | C | — | — | ESCRITURA propia |

Conclusión: `[CONFIRMADO POR CÓDIGO]` **NO ESCRIBE** ninguna de las tablas principales solicitadas. El interceptor de auditoría excluye expresamente las tres entidades PMV, por lo que la carga tampoco genera escrituras colaterales en `registro_auditoria`.

La frontera de datos no equivale a frontera de autorización. `MarcacionesProvisionalesController` no inyecta el proveedor de alcance, `EquiposActivosAsync` devuelve todos los equipos activos, `ListarImportacionesAsync()` devuelve todo el historial y los servicios aceptan cualquier `equipoId` activo. `[CONFIRMADO POR CÓDIGO]` Un usuario Administrador puede atravesar plantas mediante la URL aunque su alcance operativo esté restringido. No se ejecutó una prueba de sesión; la ruta estática no contiene un guardia alternativo.

## 6. Esquema físico PMV

### 6.1 `prov_importacion`

- PK: `id INTEGER` autoincremental.
- FK: `equipo_id → equipo_marcador.id`, `RESTRICT`.
- Obligatorios: archivo, equipo, hash, fecha/usuario de carga y tres contadores.
- Índices: `(equipo_id, fecha_carga)` y `hash_contenido`; ambos no únicos.
- Hash: `TEXT NOT NULL`; longitud 64 configurada en EF, no reforzada por un check SQLite.

### 6.2 `prov_marcacion`

- PK: `id INTEGER` autoincremental.
- FK: equipo `RESTRICT`; importación provisional `RESTRICT`.
- Obligatorios: equipo, ID de marcador, fecha-hora e importación.
- Opcionales: modo de verificación, línea original y campos de modificación.
- Unique: `(equipo_id, id_marcador, fecha_hora)`.
- Índices adicionales: `fecha_hora` e `importacion_provisional_id`.
- No existe `trabajador_id`: la atribución es dinámica.

### 6.3 `prov_error_importacion`

- PK: `id INTEGER` autoincremental.
- FK: importación provisional, `CASCADE`.
- Obligatorios: importación, número de línea y motivo.
- Opcional: contenido original.
- Índice: `importacion_provisional_id`.

Flujo físico:

```text
archivo
  → prov_importacion
       ├─ prov_marcacion (RESTRICT)
       └─ prov_error_importacion (CASCADE)
```

`[CONFIRMADO POR DATOS]` No hay violaciones FK, marcas duplicadas por clave natural, hashes duplicados, importaciones sin marcas ni diferencias entre contadores y filas relacionadas.

## 7. Deduplicación y lectura `.dat`

### Deduplicación

1. `CalcularHashAsync` reposiciona el stream a cero.
2. `SHA256.HashDataAsync` consume los bytes completos, sin normalización por nombre/extensión.
3. El hash hexadecimal minúsculo se consulta globalmente en `prov_importacion`.
4. Si existe, se lanza `ReglaNegocioException` antes de decodificar y escribir.
5. Dentro del archivo se usa un `HashSet<(IdMarcador, FechaHora)>`; contra la base se consulta la misma clave con equipo.
6. El índice único de `prov_marcacion` constituye la última defensa contra la duplicación de hechos.

Clasificación: **PROTEGIDO POR APLICACIÓN**. No existe `UNIQUE(hash_contenido)`. Dos cargas concurrentes del mismo archivo podrían superar la lectura previa; si contienen marcas iguales, una transacción fallaría por el unique de marcas, pero un archivo sin marcas válidas no tiene esa defensa física.

### `.dat`

`[CONFIRMADO POR CÓDIGO]` PMV inyecta la misma `IFuenteDeMarcaciones` que la importación principal. La implementación registrada es `LectorMarcadorAutomatico`, que decide por contenido:

- texto tabulado `.txt` o `*_attlog.dat` → `LectorTxtZkteco`;
- respaldo binario `BKtransaction.dat` → `LectorRespaldoZkteco`.

El lector binario interpreta cabecera, serie/modelo, tamaño y descriptores; deduce el offset de registros y valida que al menos 90 % de timestamps sean plausibles. El timestamp usa el empaquetado ZKTeco, no Unix. Conserva la limitación documentada de perder ceros iniciales del ID entero y de no conocer semánticamente todos los campos intermedios.

No hay parser PMV duplicado. Sí hay duplicación de orquestación entre los dos servicios: validación de equipo, hash, consulta de existentes, armado de cabecera/errores y dos guardados. La diferencia importante es que PMV sí añade transacción explícita.

## 8. Atribución de trabajador y agrupación diaria

`[CONFIRMADO POR CÓDIGO]` La clave de atribución es:

```text
prov_marcacion.equipo_id
+ prov_marcacion.id_marcador
+ fecha de la marca dentro de [fecha_inicio, fecha_fin]
→ asignacion_marcador.trabajador_id
```

La marca no guarda FK a trabajador. El servicio trae todas las asignaciones que intersectan el rango y resuelve la primera coincidencia en memoria. No filtra por estado actual del trabajador ni guarda la decisión; los cambios de vigencia alteran una consulta posterior. Una superposición podría producir una elección no determinista porque la consulta no ordena las asignaciones, aunque la base actual no tiene solapamientos ni marcas con más de una coincidencia.

Resultados reales anonimizados:

| Métrica | Resultado |
|---|---:|
| Marcas PMV | 731 |
| IDs de reloj distintos | 134 |
| Marcas sin asignación vigente | 275 |
| Jornadas equipo+ID+día sin asignación | 165 |
| Marcas con más de una asignación posible | 0 |

`[REQUIERE NEGOCIO]` La BD no permite distinguir si los 165 casos representan padrón desactualizado, asignación histórica ausente, personal aún no cargado, selección incorrecta de equipo o un ID inválido. No deben etiquetarse automáticamente como “reloj incorrecto”.

La agrupación es por fecha calendario e ID. La primera marca es ingreso y la última salida; una sola marca deja salida nula. PMV no aplica ventana nocturna, versión de turno, feriado, tolerancias ni cálculo de tareo.

## 9. Rangos PMV

| Regla | Backend/servicio | Controller | Frontend | Prueba declarada | Resultado |
|---|---|---|---|---|---|
| 1 día mínimo | rechaza inicio > fin; igualdad produce 1 | exige ambas fechas | inputs `required` | consulta de un día | CONFIRMADO |
| 31–40 días | devuelve advertencia | la propaga y permite exportar | muestra aviso y botón | >30 | CONFIRMADO |
| >40 días | consulta todavía se ejecuta; marca límite de exportación | acción Exportar vuelve sin generar libro | oculta botón | >40 flag | CONFIRMADO |

El “bloqueo >40” aplica a **exportación**, no a consulta. La protección crítica no depende solo del frontend: el controller vuelve a consultar y rechaza exportar. No existe tope absoluto para ejecutar una consulta arbitrariamente larga; el servicio únicamente produce flags.

Se localizaron 35 `[Fact]` PMV (12 importación, 13 consulta, 10 exportación). No se ejecutaron en esta fase para mantener el protocolo no invasivo; `[CONFIRMADO POR TEST]` significa cobertura declarada por el código, no resultado de ejecución. La cifra documental de 31 pruebas está obsoleta.

## 10. Exportación PMV y linaje

El libro real tiene dos hojas: `Marcaciones` con 13 columnas y `Trazabilidad`. No exporta `Unidad`.

| Columna Excel | DTO | Fuente real |
|---|---|---|
| Fecha | `Fecha` | fecha calendario de `prov_marcacion.fecha_hora` |
| ID marcador | `IdMarcador` | `prov_marcacion.id_marcador` |
| DNI | `NumeroDocumento` | `trabajador.numero_documento` actual |
| Apellido paterno | `ApellidoPaterno` | `trabajador.apellido_paterno` |
| Apellido materno | `ApellidoMaterno` | `trabajador.apellido_materno` |
| Nombres | `Nombres` | `trabajador.nombres` |
| Hora ingreso | `HoraIngreso` | mínimo de las marcas del grupo |
| Hora salida | `HoraSalida` | máximo del grupo; nulo con una marca |
| Turno | `TurnoNombre` | `turno.nombre` si asignación directa; `cuadrilla.nombre` si es rotativa |
| Planta | `Planta` | `trabajador.planta_id → planta.nombre` actual |
| Área | `Area` | `trabajador.area_id → area.nombre` actual; no `asignacion_area` histórica |
| Centro de costo | `CentroCosto` | `trabajador.centro_costo_id → centro_costo.descripcion` actual |
| Observación | `Observacion` | reglas en memoria: sin asignar, marca única o cantidad >2 |

La columna conceptual `id` se materializa como “ID marcador”; no se exportan PK técnicas. La columna `Unidad` no existe. El exportador neutraliza texto iniciado por `=`, `+`, `-` o `@` mediante `SanitizadorExcel`.

Riesgos semánticos candidatos:

- una consulta histórica combina atribución histórica del marcador con planta, área y centro de costo **actuales**;
- para cuadrilla, “Turno” muestra el nombre de cuadrilla, no el turno rotativo resuelto para ese día;
- toda la consulta y el libro se materializan en memoria, aunque la exportación se limita a 40 días.

## 11. Integridad y cardinalidades

### 11.1 Huérfanos

`[CONFIRMADO POR DATOS]` Resultado cero en todas las FK físicas mediante `foreign_key_check`. También se comprobaron explícitamente las referencias críticas sin FK de trabajador, tareo, reintegros y módulos temporales; no aparecieron huérfanos. Varias tablas están vacías, por lo que ese cero no constituye prueba bajo carga futura.

### 11.2 Cardinalidades críticas

| Tabla | Filas |
|---|---:|
| `trabajador` | 211 |
| `periodo_laboral` | 296 |
| `asignacion_marcador` | 170 |
| `asignacion_area` | 210 |
| `asignacion_sueldo` | 211 |
| `asignacion_turno` | 0 |
| `turno` / `version_turno` | 5 / 5 |
| `cuadrilla` / `programacion_cuadrilla` / `tramo_rotacion` | 0 / 0 / 0 |
| `importacion_marcador` / `marcacion_cruda` | 0 / 0 |
| `periodo` / `tareo_diario` | 2 / 0 |
| `solicitud_ausencia` / `justificacion` / `reintegro_tareo` | 0 / 0 / 0 |
| `prov_importacion` | 2 |
| `prov_marcacion` | 731 |
| `prov_error_importacion` | 761 |
| `registro_auditoria` | 1,276 |

Las importaciones PMV registran, anonimizadas, `772/380/392` y `720/351/369` filas leídas/insertadas/rechazadas. En ambos casos se cumple `leídas = insertadas + rechazadas` y las filas físicas asociadas coinciden con los contadores.

## 12. Líneas temporales con datos reales

Se consideraron filas activas y límites inclusivos.

| Línea | Filas | Invertidas | Claves con >1 abierta | Pares solapados | Huecos detectados | Clasificación |
|---|---:|---:|---:|---:|---:|---|
| `asignacion_marcador` | 170 | 0 | 0 | 0 | 0 | VÁLIDO en muestra |
| `asignacion_area` | 210 | 0 | 0 | 0 | 0 | VÁLIDO en muestra |
| `asignacion_turno` | 0 | 0 | 0 | 0 | 0 | NO DETERMINABLE |
| `asignacion_sueldo` | 211 | 0 | 0 | 0 | 0 | VÁLIDO en muestra |
| `periodo_laboral` | 296 | 0 | 85 | 85 | 0 | INCONSISTENCIA CONFIRMADA: duplicados exactos |
| `version_turno` | 5 | 0 | 0 | 0 | 0 | VÁLIDO en muestra |
| `programacion_cuadrilla` | 0 | 0 | 0 | 0 | 0 | NO DETERMINABLE |

Los huecos son cero en las líneas pobladas; aun si existieran, serían `REQUIERE NEGOCIO`, no error automático.

### 12.1 PeriodoLaboral

- 0 trabajadores sin alguna fila de periodo.
- 85 trabajadores con dos filas activas abiertas.
- 85 grupos duplicados en trabajador, empresa, fechas y campos de cese; 170 filas involucradas y máximo dos filas por grupo. PK y metadatos de auditoría se excluyeron de la comparación.
- 0 rangos invertidos y 0 divergencias entre empresa/fecha plana y cada periodo abierto.
- 126 trabajadores tienen una sola fila abierta y 85 tienen dos.

Ejemplo anonimizado: `Trabajador A` tiene dos filas físicamente distintas, pero iguales en trabajador, empresa, fecha de ingreso, ausencia de cese, motivo y observación. No representa una recontratación distinguible por los campos de negocio: es una duplicación funcional. La revisión independiente acertó al rebajar el riesgo de fuentes deliberadamente duplicadas, pero los datos confirman un problema diferente de multiplicidad no protegida.

### 12.2 Historia organizativa

- 0 referencias actuales inexistentes para empresa, planta, área, sección, puesto o centro de costo.
- 0 trabajadores activos con área actual pero sin asignación de área abierta.
- 0 divergencias entre `trabajador.area_id` y la asignación abierta.
- Planta, sección y centro de costo no tienen historia equivalente; no es posible validar divergencia temporal para esos atributos.

Clasificación: estado actual **CONSISTENTE**; cobertura histórica **REQUIERE NEGOCIO/DISEÑO**.

### 12.3 Turno y VersionTurno

- 0 turnos sin versión; 0 múltiples versiones abiertas; 0 solapamientos.
- 0 divergencias entre los nueve campos horarios planos del turno y su versión abierta.
- 0 referencias de versión a turno inexistente.

Clasificación: **VÁLIDO en la muestra**.

### 12.4 Cuadrillas y rotaciones

No existen cuadrillas, programaciones, tramos ni asignaciones de turno en la base. Las consultas estructurales devuelven cero, pero no validan el flujo real. El gap funcional sigue confirmado por modelo: el ciclo requerido `14 diurno → 1 descanso móvil → 7 nocturno` no puede representar el descanso porque cada tramo exige `turno_id`. No se corrigió.

## 13. Importación principal: riesgo transaccional A–G

Secuencia real:

```text
opcional: aprender serie del equipo y guardar
→ Add ImportacionMarcador + ErrorImportacion
→ SaveChanges #1
→ construir MarcacionCruda con importacion.id
→ SaveChanges #2
```

### A. ¿Qué queda almacenado después del primer commit?

`[CONFIRMADO POR CÓDIGO]` La cabecera `importacion_marcador`, sus `error_importacion`, contadores incluido `filas_insertadas`, y la auditoría de la cabecera. Si se aprendió una serie, el equipo ya pudo haberse actualizado en un guardado anterior. Aún no existe ninguna `marcacion_cruda` de esa carga.

### B. ¿Qué puede fallar antes del segundo?

Cancelación, pérdida de proceso, bloqueo/I/O de SQLite, excepción del segundo `SaveChanges`, o una carrera que choque con el unique `(equipo_id,id_marcador,fecha_hora)`. La preparación en memoria también puede abortar antes de llamar al segundo guardado.

### C. ¿Qué estado parcial es físicamente posible?

Una cabecera vigente con `filas_insertadas > 0` y cero marcaciones asociadas. El segundo `SaveChanges` es atómico internamente, por lo que el fallo esperado deja cero filas nuevas de ese lote, no una fracción de ellas; la falta de atomicidad está entre los dos guardados.

### D. ¿La aplicación puede recuperarse mediante reintento?

Parcialmente. La UI detecta el hash previo y exige confirmación. Tras confirmar, el servicio crea otra cabecera; como no hay marcas de la carga fallida, puede insertar las nuevas. La cabecera incompleta anterior no se repara ni elimina.

### E. ¿El hash impediría el reintento?

No de forma absoluta. El hash no es único y el servicio solo rechaza automáticamente la misma carga vigente en **otro** equipo. Para el mismo equipo, la UI permite continuar con confirmación explícita.

### F. ¿Puede quedar una importación sin todas sus marcaciones?

Sí. Está permitido por FK porque la relación padre → hijos no exige mínimo uno y los contadores no tienen constraint con el detalle.

### G. ¿Existe algún mecanismo compensatorio?

No hay transacción envolvente, borrado/invalidación automática, estado “fallida” ni reconciliador. La unicidad de marca, el aviso por hash y el reintento reducen duplicados, pero no compensan la cabecera inconsistente.

`[CONFIRMADO POR DATOS]` La base canónica tiene cero importaciones principales, por lo que el estado parcial no está observado. No se provocó el fallo ni se creó una copia: la posibilidad queda **confirmada por código y esquema**, no por datos.

## 14. Índices y planes SQLite

Los 87 índices explícitos esperados están presentes; no hay `ESPERADO Y AUSENTE` ni `NO ESPERADO`. Hay además siete autoíndices de constraints Identity, para 94 índices físicos enumerables.

| Consulta representativa | Plan observado | Evaluación |
|---|---|---|
| hash PMV | `ix_prov_importacion_hash`; B-tree temporal para ordenar por fecha | NORMAL con 2 filas; revisar si crece |
| PMV por equipo+rango | prefijo equipo de `uq_prov_marcacion_equipo_marcador_fecha`; B-tree temporal para ordenar | NORMAL hoy; índice no alinea equipo+fecha |
| dedupe PMV por equipo+IDs+rango | covering unique completo | NORMAL |
| atribución por equipo+ID+vigencia | `ix_asignacion_marcador_equipo_id_id_marcador_fecha_inicio_fecha_fin` | NORMAL |
| turno por trabajador+vigencia | `ix_asignacion_turno_trabajador_id_fecha_inicio_fecha_fin` | NORMAL; tabla vacía |
| hash principal | `ix_importacion_marcador_hash_contenido`; B-tree temporal por orden | NO MEDIBLE con datos actuales |
| marcación cruda por rango agrupada | `ix_marcacion_cruda_fecha_hora`; B-tree para `GROUP BY` | NO MEDIBLE; tabla vacía |
| tareo por periodo+estado | `ix_tareo_diario_periodo_id_estado` | NO MEDIBLE; tabla vacía |

No se reprodujo un plan integral de recálculo o reporte porque las tablas de hechos principales están vacías y EF genera varias consultas/materializaciones, no una única sentencia estable. Declarar performance a partir de ese vacío sería engañoso.

### 14.1 Medición PMV indicativa

Consulta: marcas de un equipo por rango, ordenadas por fecha; 100 repeticiones con caché caliente, misma conexión de solo lectura. La base solo contiene cuatro días y el rango amplio no añade filas.

| Rango | Filas devueltas | Mediana | p95 aproximado |
|---:|---:|---:|---:|
| 1 día | 2 | 0.0408 ms | 0.0760 ms |
| 7 días | 380 | 0.3536 ms | 0.5891 ms |
| 30 días | 380 | 0.3337 ms | 0.5740 ms |
| 40 días | 380 | 0.3435 ms | 0.5045 ms |

Clasificación: **NORMAL para el volumen actual**, no extrapolable. El servicio ejecuta un número fijo de consultas de enriquecimiento y agrupa en memoria; no se observó N+1 por jornada. Exportar también materializa todo el libro en memoria.

## 15. Matriz CRUD actualizada

| Componente | ProvImport | ProvMarca | ProvError | Equipo | AsigMarcador | Trabajador/Org | AsigTurno/Turno | Tablas principales de marcas/tareo |
|---|---|---|---|---|---|---|---|---|
| `ServicioImportacionProvisional` | C/R | C/R | C | R | — | — | — | — |
| `ServicioConsultaMarcacionProvisional` | — | R | — | R | R | R | R | — |
| `ExportadorMarcacionesProvisionales` | — | — | — | — | — | usa DTO | usa DTO | — |
| `MarcacionesProvisionalesController` | vía servicio | vía servicio | vía servicio | R vía catálogo | — | — | — | — |

No aparecen escrituras inesperadas.

## 16. Deuda confirmada y candidata

### Confirmada

1. **Atomicidad de importación principal**: dos commits sin transacción, con estado parcial físicamente permitido.
2. **Duplicación real de PeriodoLaboral**: 85 grupos abiertos iguales en los campos de negocio comparados, sin unique físico ni reparación observada.
3. **Descanso rotativo no representable**: el modelo exige turno en cada tramo.
4. **Trazabilidad de jornada incompleta**: `tareo_diario` no conserva qué marcas crudas originaron el resultado.
5. **Alcance por planta ausente en PMV**: el rol Administrador puede operar y consultar equipos/historial fuera de sus plantas autorizadas.

### Candidata, con consecuencia concreta pero sin incidencia observada

1. **Hash PMV no único**: carrera concurrente o archivo sin marcas puede producir más de una cabecera del mismo contenido.
2. **Enriquecimiento PMV mixto**: atribución de trabajador histórica, pero organización actual; puede alterar una exportación histórica tras un traslado.
3. **Turno de cuadrilla simplificado**: exporta nombre de cuadrilla, no el tramo rotativo vigente.
4. **Consulta PMV sin tope máximo**: >40 bloquea exportación, pero no evita materializar consultas arbitrariamente largas.
5. **Índice PMV de rango no alineado**: el plan usa solo `equipo_id` y ordena en temporal; requiere dataset grande antes de considerarlo deuda de performance.
6. **Fuentes planas e históricas**: hoy coinciden para área/turno, pero dependen de sincronización de aplicación.
7. **Historia organizativa parcial**: planta, sección y centro de costo actuales no tienen línea histórica.

### Hallazgos refutados o acotados

- No existe duplicación de parser PMV: ambos módulos comparten `IFuenteDeMarcaciones`.
- PMV no modifica tablas principales ni auditoría.
- No hay drift de esquema, migraciones pendientes físicamente ni índices esperados ausentes.
- No hay huérfanos, FK rotas, vigencias invertidas o solapamientos fuera de `periodo_laboral` en los datos poblados.
- Los campos planos de trabajador no divergen de sus periodos abiertos; el problema real es duplicación exacta.
- No hay evidencia de N+1 PMV por fila ni de lentitud con el volumen actual.

## 17. Dudas humanas

1. ¿Cuál es el origen operativo de las 85 duplicaciones exactas de `periodo_laboral` y cuál fila debe considerarse autoritativa?
2. ¿Las 165 jornadas PMV sin dueño reflejan padrón incompleto esperado o requieren una cola de conciliación antes de presentar/exportar?
3. ¿Una exportación histórica PMV debe mostrar organización actual o la vigente en la fecha de la marca?
4. ¿Para una asignación por cuadrilla, “Turno” debe mostrar la cuadrilla o el turno rotativo resuelto por día?
5. ¿Se permite consultar más de 40 días aunque no se pueda exportar?
6. ¿El hash PMV debe ser global, por equipo o permitir una recarga anulada/expresamente confirmada?
7. ¿La importación principal incompleta debe quedar visible como fallida, reanudarse o revertirse por completo?
8. ¿Coordinador es global y Administrador debe conservar estrictamente su alcance por planta también en PMV?

## 18. Cierre

PMV conserva su carácter provisional y separado. Esta fase no recomienda integrarlo automáticamente al motor principal. La siguiente fase recomendada es **Fase 4 — flujos, linaje y catálogo de reglas**, incorporando las decisiones humanas anteriores y un dataset controlado para importación, recálculo y reportes. No se inicia automáticamente.

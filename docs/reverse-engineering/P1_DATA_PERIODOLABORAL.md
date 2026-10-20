# P1-DATA-PERIODOLABORAL — diagnóstico y mapa de fuentes de verdad

Fecha de corte: 2026-10-12  
Estado: **REQUIERE DECISIÓN DE NEGOCIO**  
Tipo de intervención: análisis y especificación; **sin cambios de código, esquema o datos**

## 1. Resumen ejecutivo

El sistema tiene hoy una fuente operativa clara, aunque no una fuente histórica completa:

| Concepto | Fuente de verdad de facto actual | Alcance real |
|---|---|---|
| Ingreso | `Trabajador.FechaIngreso` | Motor, reportes, UI, filtros, validaciones e importación |
| Cese | `Trabajador.FechaCese` | Motor, reportes, UI, filtros, baja y selección de trabajadores |
| Vigencia | Rango plano de `Trabajador`, normalmente además de `Trabajador.Activo` | `FechaIngreso <= fecha <= FechaCese`; `Activo` es baja lógica de la ficha, no cese |
| Empresa | `Trabajador.EmpresaId` para operación y reportes; `PeriodoLaboral.EmpresaId` solo para el historial de vínculos | Dos fuentes de facto con consumidores distintos |
| Planta | `Trabajador.PlantaId` | Única fuente laboral; `PeriodoLaboral` no tiene `PlantaId` |

`PeriodoLaboral` **no participa** en la selección de población del tareo, en la decisión diaria de
vigencia, en los filtros de reportes ni en la organización usada por esos reportes. Se escribe al
crear, editar y cesar; se lee para mostrar la pestaña de historial y para cargar el vínculo abierto
en la ruta de escritura. Es, por tanto, una historia parcial y una sombra de los campos planos, no
la fuente canónica operativa.

La coherencia interna actual entre motor y reportes es intencional: ambos usan los campos planos.
Eso evita que el mismo sistema dé dos respuestas distintas hoy, pero no resuelve reingresos,
huecos entre vínculos ni organización histórica. Además, el modelo físico no impide más de un
periodo abierto ni periodos solapados.

Se confirman tres defectos de código con consecuencia histórica:

1. editar una ficha cesada, aun sin reingresarla, crea otro `PeriodoLaboral` cerrado porque el
   repositorio solo carga periodos abiertos y `SincronizarVinculoAbierto` llama a `AbrirVinculo`;
2. un cambio de empresa permitido después del cese cambia la empresa plana usada por todos los
   reportes, y la edición agrega además un periodo cerrado superpuesto con la empresa nueva;
3. cambiar `PlantaId` reescribe la planta de toda consulta histórica y cambia los insumos de un
   eventual recálculo pasado —feriados y turnos candidatos— porque no existe vigencia de planta.

La última inspección read-only disponible, efectuada en Fase 7 sobre la base local entonces
presente en el árbol —no producción—, encontró **307 trabajadores con más de un periodo abierto,
868 filas abiertas en esos grupos y 900 pares solapados**. Esa base fue después aislada en Fase 7A;
no existe actualmente una copia de desarrollo no sensible dentro del workspace y no se abrió la
cuarentena privada para esta fase.

La recomendación es una variante de la opción D: definir un vínculo laboral temporal completo
como fuente canónica histórica —incluidas empresa y planta— y mantener una proyección explícita
del vínculo vigente para compatibilidad y rendimiento. Debe hacerse por etapas y solo después de
decidir la semántica de reingreso, traslado de planta, corrección retroactiva y saneamiento de los
periodos ya divergentes. No se debe apuntar el motor a `PeriodoLaboral` en su estado actual.

## 2. Modelo actual

### 2.1 Entidades y propiedades

Las cuatro entidades heredan, directa o indirectamente, la convención de `EntidadBase`:
`Id`, `CreadoEn`, `CreadoPor?`, `ModificadoEn?`, `ModificadoPor?` y `Activo`. `Activo` vale `true`
por defecto y representa baja lógica; el cese laboral se representa por `FechaCese`.

#### Trabajador

Archivo: `src/ControlMarcacion.Domain/Personal/Trabajador.cs`  
Configuración: `TrabajadorConfiguracion` en `ConfiguracionesPersonal.cs`

| Campo | Entidad | Tipo | Nullable | FK real | Índice | Uso aparente/confirmado |
|---|---|---|---:|---|---|---|
| `Id` | Trabajador | `long` | No | PK | PK | Identidad de ficha |
| `TipoDocumentoId` | Trabajador | `long` | No | No configurada | Único con `NumeroDocumento` | Catálogo por consulta manual |
| `NumeroDocumento` | Trabajador | `string(20)` | No | No | Único compuesto | Identidad funcional/importación |
| `ApellidoPaterno` | Trabajador | `string(80)` | No | No | No | UI/reportes |
| `ApellidoMaterno` | Trabajador | `string(80)` | No | No | No | UI/reportes |
| `Nombres` | Trabajador | `string(120)` | No | No | No | UI/reportes |
| `Sexo` | Trabajador | enum como texto | No | No | No | Ficha |
| `Telefono` | Trabajador | `string(30)` | Sí | No | No | Ficha |
| `Direccion` | Trabajador | `string(250)` | Sí | No | No | Ficha |
| `CorreoElectronico` | Trabajador | `string(150)` | Sí | No | No | Ficha |
| `TieneHijos` | Trabajador | `bool` | No | No | No | Asignación familiar/reportes |
| `EmpresaId` | Trabajador | `long` | No | **No configurada** | `(EmpresaId, Activo)` | Empresa operativa actual |
| `Unidad` | Trabajador | `string(120)` | Sí | No | No | Campo legado |
| `TipoPlanilla` | Trabajador | enum como texto | No | No | No | Régimen/filtros |
| `PlantaId` | Trabajador | `long` | No | **No configurada** | `(PlantaId, AreaId, SeccionId)` | Planta operativa actual y alcance |
| `AreaId` | Trabajador | `long` | Sí | No configurada | Índice organizativo | Snapshot actual/compatibilidad |
| `SeccionId` | Trabajador | `long` | Sí | No configurada | Índice organizativo | Snapshot actual/compatibilidad |
| `FechaIngreso` | Trabajador | `DateOnly` | No | No | No propio | Inicio operativo plano |
| `FechaCese` | Trabajador | `DateOnly` | Sí | No | No | Fin operativo plano |
| `MotivoCese` | Trabajador | enum como texto | Sí | No | No | Baja |
| `ObservacionCese` | Trabajador | `string(300)` | Sí | No | No | Baja |
| `PuestoId` | Trabajador | `long` | No | No configurada | No | Puesto actual |
| `CentroCostoId` | Trabajador | `long` | Sí | No configurada | No | CECO actual |
| Auditoría + `Activo` | Trabajador | heredados | Mixto | No | Por `Activo` con empresa | Trazabilidad/baja lógica |

Navegaciones: `AsignacionesMarcador`, `AsignacionesTurno`, `AsignacionesSueldo`,
`AsignacionesArea` y `PeriodosLaborales`. No tiene navegación a `Empresa` ni `Planta`. Las
propiedades calculadas `ApellidosYNombres` y `EstaVigente` se ignoran en EF; `TieneVinculoEn`
también es comportamiento de dominio y no una columna.

#### PeriodoLaboral

Archivo: `src/ControlMarcacion.Domain/Personal/PeriodoLaboral.cs`  
Configuración: `PeriodoLaboralConfiguracion` en `ConfiguracionesPersonal.cs`

| Campo | Entidad | Tipo | Nullable | FK real | Índice | Uso aparente/confirmado |
|---|---|---|---:|---|---|---|
| `Id` | PeriodoLaboral | `long` | No | PK | PK | Identidad del vínculo |
| `TrabajadorId` | PeriodoLaboral | `long` | No | `Trabajador`, cascade | `(TrabajadorId, FechaIngreso)` no único | Dueño del vínculo |
| `Trabajador` | PeriodoLaboral | navegación | Sí en CLR | Inversa | — | Carga EF |
| `EmpresaId` | PeriodoLaboral | `long` | No | `Empresa`, restrict | Índice FK no único | Empresa histórica del vínculo |
| `FechaIngreso` | PeriodoLaboral | `DateOnly` | No | No | Compuesto no único | Inicio histórico |
| `FechaCese` | PeriodoLaboral | `DateOnly` | Sí | No | No | Nulo significa abierto |
| `MotivoCese` | PeriodoLaboral | enum como texto | Sí | No | No | Causal histórica |
| `ObservacionCese` | PeriodoLaboral | `string(300)` | Sí | No | No | Detalle histórico |
| Auditoría + `Activo` | PeriodoLaboral | heredados | Mixto | No | No | Trazabilidad/baja lógica |
| `PlantaId` | PeriodoLaboral | **no existe** | — | — | — | No hay historia laboral de planta |

`EstaVigenteEn(fecha)` y `EstaAbierto` existen en dominio; `EstaAbierto` se ignora en EF y las
consultas SQL expresan directamente `FechaCese == null`.

No hay:

- índice único parcial para un solo periodo abierto por trabajador;
- restricción de no solapamiento;
- `CHECK (FechaCese IS NULL OR FechaCese >= FechaIngreso)`;
- unicidad por trabajador/empresa/fechas;
- `PlantaId` ni otra relación temporal con planta.

#### Empresa

Archivo: `src/ControlMarcacion.Domain/Organizacion/Empresa.cs`

| Campo | Entidad | Tipo | Nullable | FK real | Índice | Uso aparente |
|---|---|---|---:|---|---|---|
| `Id` | Empresa | `long` | No | PK | PK | Identidad |
| `ClienteId` | Empresa | `long` | No | `Cliente`, restrict | Índice FK | Cliente dueño |
| `Cliente` | Empresa | navegación | Sí en CLR | Inversa | — | EF |
| `Codigo` | Empresa | `string(20)` | No | No | Único | Catálogo |
| `RazonSocial` | Empresa | `string(200)` | No | No | No | UI/reportes |
| `Ruc` | Empresa | `string(11)` | No | No | Único | Identidad tributaria |
| Auditoría + `Activo` | Empresa | heredados | Mixto | No | No | Catálogo |

Navegación: `Plantas`. No hay navegación inversa a `PeriodoLaboral` ni a `Trabajador`.

#### Planta

Archivo: `src/ControlMarcacion.Domain/Organizacion/Planta.cs`

| Campo | Entidad | Tipo | Nullable | FK real | Índice | Uso aparente |
|---|---|---|---:|---|---|---|
| `Id` | Planta | `long` | No | PK | PK | Identidad |
| `EmpresaId` | Planta | `long` | No | `Empresa`, restrict | Único con `Codigo` | Propietario de la planta |
| `Empresa` | Planta | navegación | Sí en CLR | Inversa | — | EF |
| `Codigo` | Planta | `string(20)` | No | No | Único dentro de empresa | Catálogo |
| `Nombre` | Planta | `string(120)` | No | No | No | UI/reportes |
| Auditoría + `Activo` | Planta | heredados | Mixto | No | No | Catálogo |

Navegación: `Areas`. `Trabajador.PlantaId` no está configurado como FK hacia esta entidad.

### 2.2 Migraciones

Las cuatro tablas y los campos laborales analizados nacieron en
`20260817165631_EsquemaInicial.cs`:

- `trabajador`: líneas 363–400;
- `empresa`: líneas 535–560;
- `periodo_laboral`: líneas 772–804;
- `planta`: líneas 807–830;
- índices de `periodo_laboral`: líneas 1520–1528;
- índice único de planta por empresa/código: líneas 1530–1534.

La búsqueda sobre migraciones no Designer confirma que ninguna migración posterior agregó,
eliminó o modificó columnas o restricciones de `periodo_laboral`, `fecha_ingreso` o
`fecha_cese`. `20260925150116_VigenciaDelAreaDelTrabajador.cs` solo **lee**
`trabajador.fecha_ingreso` para el backfill de la línea temporal de áreas; no modifica el modelo
laboral.

## 3. Fuente(s) de verdad

### 3.1 Estado demostrado

| Concepto | Escritura primaria | Lectura operativa | Lectura histórica | Conclusión |
|---|---|---|---|---|
| Ingreso | `Trabajador.FechaIngreso`; se copia al periodo abierto | Campo plano | `PeriodoLaboral.FechaIngreso` solo en Historial | Plano canónico de facto; periodo sombra/historia |
| Cese | `Trabajador.DarDeBaja`; intenta cerrar un periodo abierto | Campo plano | `PeriodoLaboral.FechaCese` solo en Historial | Plano canónico de facto; periodo puede divergir |
| Vigencia | Derivada del rango plano | Tareo, reportes, dropdowns y UI | `PeriodoLaboral.EstaVigenteEn` no tiene consumidor operativo | Rango plano; `Activo` es guard adicional |
| Empresa | Campo plano; se copia al periodo abierto | UI, filtros y reportes usan plano | Historial usa empresa del periodo | Dos fuentes por caso de uso |
| Planta | Campo plano, sobrescrito directamente | Todo consumidor laboral usa plano | No existe fuente histórica | Única fuente, pero solo snapshot actual |

### 3.2 Qué significa `Activo`

`Trabajador.DarDeBaja` no pone `Activo = false`. La ficha sigue consultable y la vigencia laboral
se decide con `FechaIngreso`/`FechaCese`. Por eso:

```text
Trabajador.Activo = existencia lógica de la ficha
FechaCese NULL     = vínculo plano vigente
```

El motor y reportes combinan ambos conceptos al seleccionar población: requieren `Activo` y que
el rango plano se solape con el periodo consultado. Varios desplegables simplifican a
`Activo && FechaCese == null` y no evalúan una fecha histórica.

### 3.3 Diseño intencional frente a comportamiento real

Los comentarios de `PeriodoLaboral.cs` declaran que la entidad preserva reingresos. Los comentarios
de `ServicioReportes.ResolverTrabajadoresAsync` declaran explícitamente que motor y reportes deben
seguir usando campos planos mientras el motor no migre. Ambas afirmaciones documentan intención,
pero no crean un flujo de reingreso: no hay método, endpoint ni caso de uso que limpie el cese plano
y abra un segundo vínculo válido.

## 4. Mapa de lecturas

La tabla agrupa todos los consumidores productivos relevantes encontrados. Las repeticiones de la
misma proyección en página/panel/Excel se consolidan sin ocultar su fuente.

| Capa | Archivo · clase/método | Campo/fuente | Propósito | Efecto | Riesgo si diverge |
|---|---|---|---|---|---|
| [D] | `Trabajador.TieneVinculoEn` | Fechas planas | Vigencia inclusiva | Regla de dominio | Ignora huecos/reingresos |
| [D] | `Trabajador.EstaVigente`, `DarDeBaja`, `CambiarEmpresa` | `FechaCese`, `FechaIngreso`, `EmpresaId` planos | Estado, baja y guardia de empresa | Validación/escritura | Un periodo abierto divergente no gobierna |
| [D] | `Trabajador.AbrirVinculo` | Copia campos planos | Crear historia | Escritura derivada | Copia también un cese no nulo |
| [D] | `PeriodoLaboral.EstaVigenteEn/EstaAbierto` | Fechas del periodo | Helpers | Solo escritura/tests/UI indirecta | No decide tareo ni reportes |
| [A] | DTOs `TrabajadorListaDto`, `TrabajadorPanelDto` | Fechas planas | `EstaActivo` y presentación | UI | Puede contradecir Historial |
| [A] | `ServicioTrabajadores.AsignarCuadrillaAsync`, `ValidarAsync`, reasignaciones | Fechas y planta planas | Permitir asignación y validar fronteras | Validación | Segundo vínculo no considerado |
| [I] | `TrabajadorRepository.ObtenerPorIdAsync` | Solo periodos con `FechaCese == null` | Ruta de escritura | `Include` | Múltiples abiertos se cargan; elección posterior no ordenada |
| [I] | `ConsultaTrabajadores.ListarAsync` | Empresa/planta/fechas planas | Filtros y listado | UI | Historia no interviene |
| [I] | `ConsultaTrabajadores.ObtenerDetalleAsync/ObtenerPanelAsync/BuscarRapidoAsync` | Organización y fechas planas | Ficha/panel/búsqueda | UI | Muestra snapshot actual para cualquier periodo |
| [I] | `ConsultaTrabajadores.ObtenerHistorialAsync` | `PeriodoLaboral` + empresa del periodo | Mostrar vínculos | UI histórica | Único consumidor funcional de todos los periodos |
| [I] | `ConsultaTrabajadores.CandidatosParaCuadrillaAsync` | `PlantaId`, `FechaCese == null` planos | Candidatos | Operación | El parámetro `fecha` no gobierna vigencia laboral |
| [I] | `ConsultaTrabajadores.IntegrantesDeCuadrillaAsync` | Planta actual de trabajador | Alcance de seguridad | UI/seguridad | Traslado cambia alcance de historia |
| [I] | `ServicioCalculoTareo.RecalcularPeriodoAsync` | Fechas/planta planas | Población y decisión diaria | **Cálculo** | Huecos, reingresos y planta histórica ignorados |
| [I] | `ServicioReportes.ResolverTrabajadoresAsync` | Fechas/empresa/planta planas | Población, filtros y dimensiones | **Reportes** | Periodo histórico ignorado |
| [I] | `ServicioReportes.ArmarFilasTareoAsync` | Fechas/planta resueltas desde ficha plana | Tareo final | **Reporte/Excel** | Reemisión puede mutar |
| [I] | detector de frescura de `ServicioReportes` | Fechas y planta planas | Determinar desactualización | Operación | `PeriodoLaboral` no es fuente observada |
| [I] | `ServicioSolicitudes`, `ServicioJustificaciones`, `ServicioReintegrosTareo` | `Activo && FechaCese == null`; planta plana | Dropdowns y alcance | UI/validación | No ofrecen un vigente histórico por fecha |
| [I] | `AlcancePlantasConsultas` | `Trabajador.PlantaId` | Autorización por planta | Seguridad | Traslado redefine el alcance actual |
| [I] | PMV `ServicioConsultaMarcacionProvisional` | Planta/dimensiones planas | Enriquecer consulta provisional | UI/reporte PMV | Resultado retrospectivo mutable |
| [I] | `CargaMasivaTrabajadores`, `MapaColumnas`, `CatalogosResueltos` | Entrada plana; planta resuelta dentro de empresa | Alta/actualización masiva | Importación | Hereda la semántica de `ServicioTrabajadores` |
| [I] | `ExportadorReportesExcel` | DTO ya resuelto | Escribir fechas del tareo final | Exportación | No corrige la fuente; reproduce el DTO plano |
| [W] | `TrabajadoresController` + vistas Editar/Panel/Index/Baja | DTOs planos | Editar, mostrar y cesar | UI | Edición de cesado sigue disponible |
| [W] | `TrabajadoresController.Historial` + `Historial.cshtml` | DTO de `PeriodoLaboral` | Mostrar periodos | UI histórica | Puede contradecir listado/panel/reportes |
| [W] | `ReportesController` | Servicio de reportes | Pantalla y Excel | Reporte | Exportaciones reutilizan la misma fuente plana |
| [T] | `PeriodoLaboralAcceptanceTests` | Periodo abierto | PL-01/PL-02 | Tests | Solo un vínculo; no reingreso |
| [T] | `BajaTrabajadorTests` | Campos planos + colección en memoria | Baja/empresa/vigencia | Tests | No prueba persistencia divergente salvo PL-02 |
| [T] | `AlcanceYVigenciaReportesTests` | Fechas planas | Límites de reportes | Tests | Consolida el comportamiento actual, no multiperiodo |
| [T] | `CalculadoraTareoTests` | Booleano `TieneVinculo` | Resultado `SinVinculo` | Test Domain | No prueba cómo Infrastructure deriva el booleano |

No se encontró AutoMapper, Specification ni SQL manual de negocio que haga a
`PeriodoLaboral` fuente de vigencia. La única referencia SQL posterior relevante es el backfill de
área ya descrito. Los archivos Designer/Snapshot repiten metadatos EF, no agregan consumidores.

## 5. Mapa de escrituras

| Archivo · método | Operación/canal | Qué escribe | Transacción | Validaciones y auditoría | Puede afectar pasado/divergir |
|---|---|---|---|---|---|
| `ServicioTrabajadores.CrearAsync` | Create UI/import | Todos los planos; `AbrirVinculo`; asignaciones | Un `SaveChanges` de EF | Documento, catálogos, marcador, fechas parciales; interceptor audita ambas entidades | Sí: acepta alta histórica y ya cesada |
| `ServicioTrabajadores.ActualizarAsync` | Update UI/import | Empresa/planta/ingreso planos; sincroniza primer abierto o crea periodo | Un `SaveChanges` de EF | Empresa activa no cambia; cese plano no se edita; interceptor | **Sí**: ingreso/planta pueden reescribir interpretación histórica; cesado crea periodo cerrado nuevo |
| `Trabajador.DarDeBaja` vía `DarDeBajaAsync` | Cese UI | Cese/motivo/observación planos; primer periodo abierto; asignaciones | Un `SaveChanges` de EF | Cese >= ingreso plano; segundo cese rechazado; interceptor | Sí; con múltiples abiertos cierra solo uno |
| `Trabajador.CambiarEmpresa` | Parte de Update | `Trabajador.EmpresaId` | Misma unidad de Update | Solo tras cese; interceptor | Sí: reportes pasados usan nueva empresa plana |
| `SincronizarVinculoAbierto` | Parte de Update | Ingreso/empresa del primer abierto o `AbrirVinculo` | Misma unidad de Update | Sin validación de solape/unicidad | Sí: `FirstOrDefault`; crea fila cerrada en cesados |
| `CargaMasivaTrabajadores.CargarAsync` | Import masivo | Llama Create/Update por fila | Transacción explícita alrededor del lote; errores de negocio por fila se acumulan; `soloValidar` revierte | Resolución empresa→planta sí es coherente; interceptor | Sí: con `actualizarExistentes` hereda todos los riesgos de Update |
| Formulario Web `Guardar` | UI | Envía `EmpresaId`, `PlantaId`, `FechaIngreso`; cese oculto | Delegada al servicio | Cascadas mejoran UX; no son constraint de DB | Petición manipulada puede combinar empresa/planta si Application no lo rechaza |

No existe escritura denominada reingreso. `FechaCese` solo se asigna en alta histórica o baja; la
actualización normal no la pone en `null`. Por eso `SincronizarVinculoAbierto` no implementa por sí
solo el reingreso que menciona su comentario.

La auditoría es transversal mediante `InterceptorAuditoria`: `Trabajador` y `PeriodoLaboral` no
están excluidos, por lo que altas y cambios se registran. Eso no sustituye invariantes físicos ni
convierte dos guardados separados en una regla de sincronización demostrable.

### 5.1 Secuencia problemática al editar un cesado

```text
Repositorio carga Trabajador
  + solo PeriodosLaborales con FechaCese NULL
Trabajador cesado → colección cargada vacía
ActualizarAsync conserva Trabajador.FechaCese
SincronizarVinculoAbierto no encuentra abierto
AbrirVinculo copia FechaIngreso, EmpresaId y también FechaCese no nulo
SaveChanges inserta otro periodo cerrado y solapado
```

Este defecto existe aun si el usuario solo corrige un teléfono.

## 6. Flujo de tareo

Punto de entrada: `ServicioCalculoTareo.RecalcularPeriodoAsync`.

```text
Periodo de planilla solicitado
  ↓
Trabajador.Activo
+ Trabajador.FechaIngreso <= Periodo.FechaFin
+ (Trabajador.FechaCese NULL o >= Periodo.FechaInicio)
  ↓
proyección: Id + FechaIngreso + FechaCese + PlantaId planos
  ↓
por cada fecha:
TieneVinculo = FechaIngreso <= fecha
             && (FechaCese NULL || FechaCese >= fecha)
  ↓
PlantaId actual decide feriado aplicable y turnos candidatos
  ↓
asignaciones temporales de marcador/turno/cuadrilla
  ↓
CalculadoraTareo.Calcular(EntradaCalculo)
  ↓
TareoDiario persistido
```

Evidencia exacta:

- población: `ServicioCalculoTareo.cs:61-80`;
- decisión diaria de vínculo: `ServicioCalculoTareo.cs:356-365`;
- feriado y candidatos por planta plana: líneas 366-373;
- cálculo Domain: línea 376.

Clasificación solicitada: **A) utiliza exclusivamente `Trabajador.FechaIngreso/FechaCese` para
vigencia**. No usa `PeriodoLaboral`. La empresa no participa en la aritmética diaria; la planta
plana sí participa en alcance, feriados y candidatos de turno.

Las asignaciones de turno, cuadrilla y marcador sí tienen vigencia propia. Esto evita parte de la
reinterpretación histórica, pero no repara la falta de vigencia del vínculo, empresa o planta.

## 7. Flujo de reportes

Los tres reportes pasan por `ServicioReportes.ResolverTrabajadoresAsync`. Ese método reproduce el
mismo solapamiento plano que el motor, filtra por `Trabajador.EmpresaId` y
`Trabajador.PlantaId`, y proyecta las fechas/organización actuales. Las exportaciones Excel llaman
los mismos métodos del servicio; no existe una segunda consulta.

| Reporte/salida | FechaIngreso | FechaCese | Empresa | Planta | Fuente | Estado |
|---|---|---|---|---|---|---|
| Resumen de tareo (pantalla) | Filtra por plana; no la muestra | Filtra por plana; no la muestra | Plana | Plana | `ResolverTrabajadoresAsync` | `[DIVERGENCIA POSIBLE]` |
| Resumen de tareo (Excel) | Igual que pantalla | Igual | Plana | Plana | Mismo DTO del servicio | `[DIVERGENCIA POSIBLE]` |
| Excepciones (pantalla) | Filtra por plana; no la muestra | Filtra por plana; no la muestra | Solo delimita población/filtro | Solo delimita población/filtro | `ResolverTrabajadoresAsync` + `TareoDiario` | `[DIVERGENCIA POSIBLE]` |
| Excepciones (Excel) | Igual que pantalla | Igual | Igual | Igual | Mismo DTO del servicio | `[DIVERGENCIA POSIBLE]` |
| Tareo final | Plana y visible | Plana y visible | Plana; también encabezado/filtro | Plana y visible | `TrabajadorResuelto` | `[DIVERGENCIA CONFIRMADA]` ante traslado/cambio |
| Anexos del tareo final | Población plana | Población plana | Razón social resuelta con filtro/planos | Población plana | Mismo conjunto resuelto | `[DIVERGENCIA POSIBLE]` |
| Historial de trabajador | Periodo | Periodo | Periodo | No existe | `PeriodoLaboralDto` | `[CONSISTENTE]` con la tabla; no con motor necesariamente |

`[DIVERGENCIA CONFIRMADA]` significa que el código usa el snapshot actual para reemitir historia;
no afirma que cada archivo emitido haya sido materialmente incorrecto. `[DIVERGENCIA POSIBLE]`
requiere que los planos difieran de los periodos o que haya un segundo vínculo real.

La elección plana está documentada en el propio código (`ServicioReportes.cs:975-978`) para no
discrepar del motor. Es una consistencia local intencional, no una solución histórica.

## 8. Resultados SQLite

### 8.1 Disponibilidad y guardas

En esta fase no se abrió ninguna base:

- Fase 7A retiró del árbol operativo 75 archivos SQLite/sidecar/backup, más hallazgos posteriores,
  después de copiarlos y verificar hashes;
- los cuatro archivos versionados permanecen como eliminaciones staged;
- la cuarentena privada no forma parte de este workspace y no se abrió;
- producción no se identificó, no se consultó y no se modificó;
- no se creó una base temporal porque eso no aportaría evidencia sobre los datos existentes.

### 8.2 Evidencia read-only disponible

| Corte | Origen | Resultado | Limitación |
|---|---|---|---|
| Fase 3B | Base local del árbol, read-only | 0 trabajadores sin periodo; 85 trabajadores con dos periodos abiertos; 170 filas; 0 divergencias entre planos y cada abierto en esa muestra | Foto anterior; quedó superada |
| Fase 7 (2026-10-09) | Base local del árbol, no producción | Integridad `ok`; 0 FK rotas; **307 trabajadores con >1 abierto; 868 filas abiertas; 900 pares solapados** | Última foto; archivo ya aislado |
| Esta fase | Workspace posterior a 7A | **No hay copia no sensible disponible** | Sin conteos nuevos; no se extrapola a producción |

Los conteos 307/868/900 son una `[INCONSISTENCIA DE DATOS]` confirmada en aquella copia local,
no un conteo de producción ni una garantía sobre el estado presente de la cuarentena.

### 8.3 Paquete de consultas para una futura copia autorizada

Todas son `SELECT`/CTE. Deben ejecutarse con conexión read-only/`immutable=1` sobre una copia
identificada y con el proceso de aplicación detenido.

```sql
-- 1. Trabajadores sin ningún PeriodoLaboral
SELECT COUNT(*) AS trabajadores_sin_periodo
FROM trabajador t
WHERE NOT EXISTS (
  SELECT 1 FROM periodo_laboral p WHERE p.trabajador_id = t.id
);

-- 2. Trabajadores con más de un periodo total y más de uno abierto
SELECT COUNT(*) AS trabajadores_con_multiples_periodos
FROM (
  SELECT trabajador_id FROM periodo_laboral
  GROUP BY trabajador_id HAVING COUNT(*) > 1
);

SELECT COUNT(*) AS trabajadores_con_multiples_abiertos,
       COALESCE(SUM(cantidad), 0) AS filas_abiertas_involucradas
FROM (
  SELECT trabajador_id, COUNT(*) AS cantidad
  FROM periodo_laboral
  WHERE fecha_cese IS NULL
  GROUP BY trabajador_id HAVING COUNT(*) > 1
);

-- 3. Pares solapados, límites inclusivos
SELECT COUNT(*) AS pares_solapados
FROM periodo_laboral a
JOIN periodo_laboral b
  ON b.trabajador_id = a.trabajador_id
 AND b.id > a.id
 AND a.fecha_ingreso <= COALESCE(b.fecha_cese, '9999-12-31')
 AND b.fecha_ingreso <= COALESCE(a.fecha_cese, '9999-12-31');

-- 4. Ingreso plano distinto del primer ingreso histórico
SELECT COUNT(*) AS ingreso_plano_distinto
FROM trabajador t
WHERE EXISTS (SELECT 1 FROM periodo_laboral p WHERE p.trabajador_id = t.id)
  AND t.fecha_ingreso <> (
    SELECT MIN(p.fecha_ingreso) FROM periodo_laboral p WHERE p.trabajador_id = t.id
  );

-- 5. Cese plano distinto del periodo más reciente (desempate por id)
WITH ultimo AS (
  SELECT p.*,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id ORDER BY fecha_ingreso DESC, id DESC
         ) AS rn
  FROM periodo_laboral p
)
SELECT COUNT(*) AS cese_plano_distinto
FROM trabajador t
JOIN ultimo u ON u.trabajador_id = t.id AND u.rn = 1
WHERE NOT (t.fecha_cese IS u.fecha_cese);

-- 6. Algún periodo abierto con cese plano informado/pasado
SELECT COUNT(DISTINCT t.id) AS abierto_con_cese_plano
FROM trabajador t
JOIN periodo_laboral p ON p.trabajador_id = t.id
WHERE p.fecha_cese IS NULL
  AND t.fecha_cese IS NOT NULL;

-- 7. Sin periodo abierto pero cese plano NULL
SELECT COUNT(*) AS plano_vigente_sin_periodo_abierto
FROM trabajador t
WHERE t.fecha_cese IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM periodo_laboral p
    WHERE p.trabajador_id = t.id AND p.fecha_cese IS NULL
  );

-- 8. Empresa plana distinta de un periodo abierto
SELECT COUNT(DISTINCT t.id) AS empresa_plana_distinta_del_abierto
FROM trabajador t
JOIN periodo_laboral p ON p.trabajador_id = t.id AND p.fecha_cese IS NULL
WHERE p.empresa_id <> t.empresa_id;

-- 9. Referencias planas sin catálogo o planta que pertenece a otra empresa
SELECT COUNT(*) AS trabajador_con_empresa_inexistente
FROM trabajador t LEFT JOIN empresa e ON e.id = t.empresa_id
WHERE e.id IS NULL;

SELECT COUNT(*) AS trabajador_con_planta_inexistente_o_de_otra_empresa
FROM trabajador t LEFT JOIN planta p ON p.id = t.planta_id
WHERE p.id IS NULL OR p.empresa_id <> t.empresa_id;
```

No es posible comparar “planta plana vs planta del periodo”: ese segundo campo no existe.

## 9. Cobertura actual

Baseline heredado y verificado al cierre de `P1-STARTUP-HEALTH`:

```text
Domain:         368 / 368
Infrastructure: 436 / 436
Web:              9 / 9
Total:          813 / 813
Omitidos:         0
```

Esta fase no cambió ejecutables; no se volvió a ejecutar la suite para validar un documento.

| Contrato/caso | Estado | Evidencia |
|---|---|---|
| Crear ficha crea un vínculo | `[CUBIERTO]` | PL-01 |
| Editar dos veces un trabajador activo conserva un abierto | `[CUBIERTO]` | PL-01, contextos separados |
| Cese cierra el vínculo abierto | `[CUBIERTO]` | PL-02 + `BajaTrabajadorTests` |
| Cese anterior al ingreso y segundo cese | `[CUBIERTO]` | `BajaTrabajadorTests` |
| Empresa no cambia mientras está activo | `[CUBIERTO]` | `BajaTrabajadorTests` |
| Empresa puede cambiar tras cese y periodo cerrado conserva la anterior | `[PARCIAL]` | Solo Domain; no prueba `ActualizarAsync` ni el periodo adicional |
| Vigencia inclusiva del rango plano | `[CUBIERTO]` | `TieneVinculoEn...` y reportes de límites |
| Tareo Domain sin vínculo | `[PARCIAL]` | Prueba booleano `false`, no derivación desde persistencia |
| Tareo antes de ingreso/después de cese en Infrastructure | `[SIN COBERTURA DIRECTA]` | No hay aceptación de `RecalcularPeriodoAsync` dedicada al borde laboral |
| Edición de trabajador cesado | `[SIN COBERTURA]` | No existe contrato |
| Reingreso/segundo vínculo | `[SIN COBERTURA]` | No existe caso de uso |
| Múltiples periodos legítimos | `[SIN COBERTURA]` | PL-01 exige una sola fila |
| Huecos entre periodos | `[SIN COBERTURA]` | Motor no consulta periodos |
| Solapamientos | `[SIN COBERTURA]` | Sin validador/constraint/test concurrente |
| Periodo futuro | `[SIN COBERTURA]` | Motor no consulta periodos |
| Cambio histórico de planta | `[SIN COBERTURA]` | No existe modelo temporal de planta |
| Cambio de empresa extremo a extremo | `[SIN COBERTURA]` | Solo regla Domain aislada |
| Divergencia plano/periodo | `[SIN COBERTURA]` | Ningún test inyecta fuentes contradictorias |
| Reporte con segundo vínculo | `[SIN COBERTURA]` | Reportes prueban un solo rango plano |
| Unicidad concurrente de periodo abierto | `[SIN COBERTURA]` | La base tampoco la impone |

Los 813 tests verdes demuestran el baseline actual; no contradicen los huecos anteriores.

## 10. Casos problemáticos

Comportamiento **real** del código actual, sin describir todavía el objetivo:

| Caso | Estado preparado | Comportamiento real |
|---:|---|---|
| 1 | Un solo periodo | Alta escribe campos planos y una fila de periodo. Motor/reportes leen planos; Historial lee la fila. Mientras coincidan, el resultado parece único y consistente. |
| 2 | Trabajador cesado | Baja escribe cese plano y cierra solo el primer periodo abierto cargado; motor/reportes lo incluyen únicamente si el rango plano se solapa. UI lo muestra cesado. |
| 3 | Reingreso | No hay operación soportada. Editar no limpia `FechaCese`; un nuevo ingreso posterior al cese se rechaza porque `cese < ingreso`. Una edición permitida crea otro periodo **cerrado**, no un vínculo vigente. |
| 4 | Cambio de planta | `ActualizarAsync` sobrescribe `Trabajador.PlantaId` sin fecha efectiva. Listados/reportes históricos muestran la planta nueva; un recálculo usa la planta nueva para feriados y turnos candidatos incluso en fechas anteriores. No queda historial laboral de planta. |
| 5 | Cambio de empresa | Se rechaza si el plano está vigente. Tras el cese, se sobrescribe `Trabajador.EmpresaId`; el periodo cerrado antiguo conserva la empresa anterior, pero la misma edición crea otro periodo cerrado con empresa nueva y fechas copiadas. Reportes históricos usan la empresa plana nueva. |
| 6 | Periodo abierto + cese plano pasado | Motor, reportes, UI y dropdowns tratan al trabajador como cesado. Historial muestra un vínculo vigente. Una segunda baja es rechazada por el plano; una edición sincroniza ingreso/empresa del primer abierto pero no lo cierra. |
| 7 | Cese plano NULL + sin abierto | Motor/reportes lo tratan vigente. Historial no muestra abierto. La próxima edición llama a `AbrirVinculo` y crea uno; hasta entonces las fuentes difieren. |
| 8 | Periodos solapados | DB los acepta. Motor/reportes los ignoran y calculan por el único rango plano. Cese/sincronización usa el primer abierto cargado sin orden; Historial muestra todos. |
| 9 | Hueco entre dos periodos | El hueco no existe para el motor. Si el rango plano abarca ambas puntas, se calcula vínculo continuo y pueden generarse faltas/tareo dentro del hueco. |
| 10 | Periodo futuro | El motor lo ignora. Si el plano dice vigente hoy, calcula hoy; si el plano tiene ingreso futuro, excluye hasta esa fecha. Historial sí muestra la fila futura. |

## 11. Hallazgos priorizados

| ID | Clase | P | Descripción y evidencia | Impacto / probabilidad | Componente e historia | Migración | Decisión negocio |
|---|---|---:|---|---|---|---|---|
| PL-B01 | `[BUG CONFIRMADO]` | P1 | Editar un cesado crea un nuevo periodo cerrado: repositorio carga solo abiertos y `AbrirVinculo` copia el cese no nulo | Historial duplicado/solapado; probabilidad alta ante cualquier edición | Personal; sí afecta historia | No para impedirlo; datos sí pueden requerir saneamiento | No para prevención; sí para reparar |
| PL-B02 | `[BUG CONFIRMADO]` | P1 | Cambio de empresa tras cese actualiza el plano usado por reportes y crea un periodo cerrado superpuesto con la empresa nueva | Atribución histórica incorrecta; probabilidad media | Personal/reportes; sí | Desconocido | Sí |
| PL-B03 | `[BUG CONFIRMADO]` | P1 | Cambio de planta sin vigencia reescribe reportes pasados y los insumos de recálculo | Exactitud histórica/feriados/turnos; probabilidad media | Tareo/reportes/seguridad; sí | Sí si se historiza | Sí |
| PL-D01 | `[INCONSISTENCIA DE DATOS]` | P1 | Última base local auditada: 307 trabajadores, 868 filas abiertas y 900 pares solapados | Integridad alta; ocurrencia confirmada en copia local | Datos históricos | Sí después del saneamiento para defensa física | Sí para sobrevivientes |
| PL-R01 | `[RIESGO]` | P1 | DB permite múltiples abiertos, rangos invertidos y solapes; índice actual no es único | Recurrencia/concurrencia; probabilidad media | Persistencia | Sí | No para un abierto; sí para solapes |
| PL-R02 | `[RIESGO]` | P1 | Motor/reportes ignoran periodos: reingresos, huecos y futuros no se expresan | Tareo incorrecto si aparecen vínculos reales múltiples; probabilidad hoy desconocida | Tareo/reportes; sí | Probable | Sí |
| PL-R03 | `[RIESGO]` | P1 | `FirstOrDefault` sobre abiertos sin orden cuando la DB admite varios | Cierra/sincroniza una fila no determinista; probabilidad alta con los datos auditados | Personal/datos | No para ordenar; sí para unicidad | Sí para saneamiento |
| PL-R04 | `[RIESGO]` | P2 | `Trabajador.EmpresaId/PlantaId` no son FKs y Application no demuestra en la ruta general que la planta pertenezca a la empresa | Combinaciones inválidas por request/manual; probabilidad baja-media | Personal/reportes/alcance | Sí para FK; validación no | No |
| PL-R05 | `[RIESGO]` | P1 | Cambiar el motor a periodos sin perfilar/sanear usaría datos ya duplicados | Cambio masivo de población y tareo; probabilidad cierta si se migra hoy | Tareo/datos | Sí/posible | Sí |
| PL-DT01 | `[DEUDA TÉCNICA]` | P2 | Predicado de vigencia plano duplicado en Domain, tareo, reportes y servicios | Drift futuro; probabilidad media | Varias capas | No | No |
| PL-DT02 | `[DEUDA TÉCNICA]` | P2 | Comentarios prometen reingreso pero no existe operación ni contrato | Falsa seguridad arquitectónica | Personal/docs | No | Sí |
| PL-I01 | `[DISEÑO INTENCIONAL]` | — | Reportes usan planos para coincidir con el motor | Evita discrepancia interna actual | Tareo/reportes | No | No |
| PL-NP01 | `[NO PROBLEMA]` | — | `Activo` no se apaga al cesar | Permite conservar/consultar ficha; la vigencia la da el rango | Personal | No | No |
| PL-PN01 | `[PENDIENTE DE NEGOCIO]` | P1 | Definir semántica de snapshot plano vs vínculo canónico | Bloquea arquitectura objetivo | Todo el módulo | Desconocido | Sí |
| PL-PN02 | `[PENDIENTE DE NEGOCIO]` | P1 | Definir reingreso, corrección de cese y cambio de razón social | Bloquea caso de uso | Personal/tareo | Probable | Sí |
| PL-PN03 | `[PENDIENTE DE NEGOCIO]` | P1 | Definir traslado de planta y fecha efectiva | Bloquea historia de planta | Tareo/reportes/seguridad | Probable | Sí |
| PL-PN04 | `[PENDIENTE DE NEGOCIO]` | P1 | Definir resolución de duplicados, solapes y huecos existentes | Bloquea saneamiento | Datos | Sí | Sí |
| PL-PN05 | `[PENDIENTE DE NEGOCIO]` | P1 | Elegir snapshot de reporte cerrado o resolución temporal reproducible | Bloquea RQ-013/P1-06 | Reportes | Desconocido | Sí |

Conteo usado en el resumen de cierre: **3 bugs confirmados, 5 riesgos y 5 decisiones de negocio
pendientes**. La inconsistencia de datos, deudas y diseños intencionales se cuentan aparte.

## 12. Opciones de remediación

### Opción A — campos planos como snapshot; PeriodoLaboral como histórico

Los planos siguen gobernando el vínculo vigente y el cálculo. `PeriodoLaboral` se corrige como
bitácora histórica, con una operación explícita de reingreso que actualiza el snapshot.

| Dimensión | Evaluación |
|---|---|
| Ventajas | Menor cambio en motor/reportes; rendimiento y compatibilidad altos |
| Riesgos | Dos fuentes siguen pudiendo divergir; los planos no expresan huecos históricos |
| Compatibilidad histórica | Buena solo si toda consulta pasada cambia a periodos |
| Migraciones | Índice único abierto y constraints tras saneamiento; agregar planta si se exige historia |
| Reportes/cálculo | Reportes pasados deberían resolver periodos; motor puede requerir segmentación igualmente |
| Pruebas | Media-alta; sincronización y reingreso son críticos |
| Despliegue | Medio; transición parcial puede dejar consumidores diferentes |

### Opción B — PeriodoLaboral canónico; campos planos derivados

Cada fecha se resuelve contra `PeriodoLaboral`. Los planos quedan temporalmente como cache/proyección
del vínculo vigente y no admiten escritura independiente.

| Dimensión | Evaluación |
|---|---|
| Ventajas | Una semántica temporal para ingreso/cese/empresa; expresa huecos y reingresos |
| Riesgos | La tabla actual está incompleta y físicamente inconsistente; coste de consultas |
| Compatibilidad histórica | Alta tras reconciliar datos; no resuelve planta sin ampliar el modelo |
| Migraciones | Saneamiento, `PlantaId` o vínculo organizativo, constraints e índices |
| Reportes/cálculo | Ambos deben cambiar en la misma entrega lógica para no divergir |
| Pruebas | Alta; matriz por fecha, reingreso, huecos, solapes y rendimiento |
| Despliegue | Alto si es “big bang”; inaceptable antes del saneamiento |

### Opción C — sincronización explícita bidireccional

Ambas representaciones conservan autoridad y un servicio central intenta mantenerlas idénticas.

| Dimensión | Evaluación |
|---|---|
| Ventajas | Cambio superficial menor; consumidores antiguos continúan |
| Riesgos | Autoridad ambigua, carreras, reparaciones difíciles y divergencia permanente posible |
| Compatibilidad histórica | Media; una pareja plana no representa múltiples rangos |
| Migraciones | Constraint de abierto; planta histórica sigue sin solución |
| Reportes/cálculo | Pueden seguir planos, pero no ganan historia real |
| Pruebas | Muy alta por matriz de escrituras y concurrencia |
| Despliegue | Medio, con riesgo operativo acumulativo alto |

### Opción D — vínculo canónico completo + proyección vigente explícita

Modelar el vínculo como unidad temporal completa (`Trabajador`, empresa, planta, ingreso, cese y
causal). Los campos actuales se conservan durante la transición como una proyección read-only del
vínculo vigente/último, con reconciliación verificable. Reportes cerrados se protegen con snapshot
o con resolución temporal aprobada.

| Dimensión | Evaluación |
|---|---|
| Ventajas | Separa identidad personal, vínculo laboral y proyección actual; cubre empresa/planta/reingreso |
| Riesgos | Mayor diseño y migración; exige evidencia para backfill de planta histórica |
| Compatibilidad histórica | La mejor, siempre que no se inventen datos faltantes |
| Migraciones | Sí, por etapas y posteriores al saneamiento/decisiones |
| Reportes/cálculo | Resolución común de vínculo por fecha; proyección actual solo para UI rápida |
| Pruebas | Alta, pero con autoridad única y menos combinaciones ambiguas que C |
| Despliegue | Alto si se mezcla; medio si se divide en contratos, datos, esquema, dual-read y corte |

## 13. Recomendación

Adoptar **opción D por etapas**, no una sustitución inmediata:

1. cerrar decisiones PL-PN01..05 y aprobar invariantes ejecutables;
2. obtener una copia objetivo autorizada y repetir el perfilado read-only;
3. impedir nuevas duplicaciones de cesados en código, sin cambiar todavía la fuente del motor;
4. diseñar un saneamiento reversible con tabla de correspondencia/sobrevivientes y bitácora;
5. completar el vínculo histórico con planta solo cuando exista evidencia; no inferirla del estado
   actual si pudo cambiar;
6. aplicar constraints después del saneamiento;
7. introducir un único resolvedor de vínculo por fecha y usarlo en tareo/reportes bajo contratos;
8. convertir los planos en proyección derivada y retirar su escritura independiente en una fase
   posterior;
9. tratar reproducibilidad de reportes cerrados como contrato separado.

No se recomienda B directamente porque `PeriodoLaboral` contiene duplicados confirmados, carece de
planta y no tiene invariantes físicos. C perpetúa la doble autoridad. A puede ser un paso transitorio,
pero no resuelve por sí sola huecos históricos ni traslado de planta.

## 14. Tests requeridos

Contratos propuestos; no implementados en esta fase:

| ID | Nivel | Contrato esperado |
|---|---|---|
| PL-N01 | Application + SQLite | Alta crea exactamente un vínculo, plano y periodo coinciden |
| PL-N02 | Application + SQLite | Editar activo N veces conserva exactamente un abierto |
| PL-N03 | Application + SQLite | Editar cesado no crea ningún periodo nuevo |
| PL-N04 | Application + SQLite | Cese cierra exactamente el vínculo vigente y todas sus asignaciones requeridas |
| PL-N05 | Application | Reingreso abre segundo vínculo sin reescribir el primero |
| PL-N06 | SQLite | Dos abiertos concurrentes: uno gana y el otro falla controladamente |
| PL-N07 | Domain/Application | Cese anterior al ingreso y rango invertido se rechazan |
| PL-N08 | Domain/Application | Solape entre periodos se rechaza o sigue la política aprobada |
| PL-N09 | Domain/Application | Hueco entre periodos se conserva; no se inventa continuidad |
| PL-N10 | Tareo integración | Antes del primer ingreso: `SinVinculo`/no falta |
| PL-N11 | Tareo integración | Después del cese: `SinVinculo`/no falta |
| PL-N12 | Tareo integración | Durante segundo vínculo sí calcula; durante el hueco no |
| PL-N13 | Tareo integración | Periodo futuro no da vínculo antes de su inicio |
| PL-N14 | Reportes integración | Consulta histórica elige empresa del vínculo de la fecha |
| PL-N15 | Reportes integración | Consulta histórica elige planta del vínculo de la fecha |
| PL-N16 | Application + SQLite | Cambio de empresa exige cierre y fecha de nuevo vínculo |
| PL-N17 | Application + SQLite | Cambio de planta exige fecha efectiva y no muta el tramo anterior |
| PL-N18 | Reportes | Reemisión de periodo cerrado conserva dimensiones aprobadas |
| PL-N19 | Integridad | Dataset con plano/periodo divergente se detecta y no se corrige silenciosamente |
| PL-N20 | Migración | Saneamiento es idempotente, auditable y reversible; conteos antes/después reconciliados |
| PL-N21 | Web | No existe edición accidental de vínculo/cese por campos ocultos manipulados |
| PL-N22 | Rendimiento | Resolver vínculos por fecha para un periodo representativo no introduce N+1 |

Cada contrato de negocio debe tener una variante con límites inclusivos en ingreso y cese. Los tests
de motor deben usar `ServicioCalculoTareo`, no limitarse a entregar manualmente
`EntradaCalculo.TieneVinculo`.

## 15. Riesgos

- **Datos antes que fuente:** apuntar el motor a la tabla actual convertiría duplicados históricos
  en decisiones operativas.
- **Backfill inventado:** la planta actual no prueba la planta histórica. Un backfill sin evidencia
  produciría una historia aparentemente precisa pero falsa.
- **Cambios parciales:** migrar solo reportes o solo tareo rompería la coherencia intencional actual.
- **Periodos cerrados:** corregir organización o vínculo puede cambiar entregables ya pagados; debe
  existir política de corrección/reintegro, no un update silencioso.
- **Concurrencia:** una guardia Application no sustituye el índice/constraint físico.
- **Seguridad por planta:** si el alcance se hace histórico, debe definirse si el usuario puede ver
  trabajadores trasladados y bajo qué planta/fecha.
- **Rendimiento:** una resolución temporal mal implementada por trabajador/día puede introducir N+1;
  el resolvedor debe cargar intervalos por lote.
- **Auditoría:** el saneamiento no debe borrar evidencia ni reutilizar el interceptor como única
  bitácora de una transformación masiva.
- **Producción desconocida:** ningún conteo de la copia local se debe presentar como estado
  productivo.
- **Staged deletions:** las cuatro eliminaciones de Fase 7A son evidencia de contención y no deben
  restaurarse para facilitar pruebas.

## 16. Próximo paso

El siguiente paso no es implementar el fix. Es una revisión humana de este informe para resolver,
como mínimo, estas cinco decisiones:

1. ¿`Trabajador.FechaIngreso/FechaCese/EmpresaId/PlantaId` serán snapshot vigente, cache derivada o
   seguirán siendo autoridad?
2. ¿Cómo se registra un reingreso y cuándo puede corregirse un vínculo cerrado?
3. ¿Un traslado de planta abre un nuevo vínculo laboral o una vigencia organizativa separada?
4. ¿Cómo se elige y documenta el sobreviviente de duplicados/solapes sin inventar historia?
5. ¿Los reportes cerrados se reproducen por snapshot o por resolución temporal versionada?

Después de esa aprobación debe prepararse una especificación de aceptación y un plan de datos
separado. Recién entonces corresponde diseñar el cambio de esquema/código. No se creó migración, no
se modificaron datos, no se abrió producción y no se ejecutó OP-03.


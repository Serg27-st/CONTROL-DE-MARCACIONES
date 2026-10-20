# P1-DATA-PERIODOLABORAL — Fase 3E-A

## Clasificación y cierre de deuda legacy

Estado: **COMPLETADA**  
Fecha: 2026-10-14  
Alcance: inventario, clasificación y documentación.  
Cambios funcionales: **ninguno**.

## 1. Baseline inicial

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 415/415 |
| Infrastructure | 476/476 |
| Web | 23/23 |
| Total | 914/914 verdes; 0 fallos; 0 omitidos |

## 2. Criterio de inventario

Se revisaron las definiciones, lecturas, escrituras, filtros, validaciones, DTO, formularios,
importación, configuración EF, consultas de Personal, servicios operativos, PMV y evidencia de
tests. Los nombres homónimos de catálogos, equipos, feriados, usuarios u otras entidades no se
contabilizan como usos de los cuatro campos legacy de `Trabajador`.

El conteo es por **sitio lógico de uso**, no por coincidencia textual. La búsqueda bruta contiene
centenares de apariciones porque `EmpresaId` y `PlantaId` existen en muchas entidades ajenas a
`Trabajador`. El inventario normalizado contiene **36 grupos**.

Categorías permitidas aplicadas: `PROYECCION_ACTUAL`, `COMPATIBILIDAD`, `UI_ACTUAL`,
`IMPORTACION`, `VALIDACION_ACTUAL`, `CANDIDATO_ELIMINACION`,
`USO_HISTORICO_INCORRECTO` y `NO_APLICA`.

## 3. Semántica real confirmada

| Campo | Semántica actual real | No significa | Fuente autoritativa | Estado |
|---|---|---|---|---|
| `Trabajador.FechaIngreso` | Inicio del vínculo actual o del último vínculo proyectado; tras reingreso toma la fecha del nuevo episodio | Primer ingreso histórico | `PeriodoLaboral.FechaIngreso` del episodio aplicable | Proyección redundante necesaria temporalmente |
| `Trabajador.FechaCese` | Cese del vínculo proyectado; `null` mientras el vínculo actual está abierto; vuelve a `null` al reingresar | Último cese histórico independiente del estado actual | `PeriodoLaboral.FechaCese` del episodio aplicable | Proyección redundante necesaria temporalmente |
| `Trabajador.EmpresaId` | Empresa actual/del vínculo proyectado | Empresa histórica para cualquier fecha | `PeriodoLaboral.EmpresaId` por fecha | Proyección redundante necesaria temporalmente |
| `Trabajador.PlantaId` | Planta actual/última planta proyectada | Planta histórica para cualquier fecha | `AsignacionPlanta.PlantaId` contenida en el periodo y vigente por fecha | Proyección redundante necesaria temporalmente |

`Trabajador` no posee navegaciones `Empresa` ni `Planta`. Los miembros llamados `Empresa` y
`Planta` encontrados en DTO son textos de presentación: en Personal representan el estado actual;
en reportes históricos ya son valores resueltos desde el contexto canónico.

## 4. Matriz completa de usos y deuda

| ID | Elemento | Archivo | Método/área | Categoría | Fuente actual | Fuente autoritativa | Riesgo | Acción futura | Prioridad |
|---|---|---|---|---|---|---|---|---|---|
| L01 | `FechaIngreso` plano | `Domain/Personal/Trabajador.cs` | propiedad | PROYECCION_ACTUAL | columna `Trabajador` | periodo aplicable | Divergencia si una escritura elude el agregado | Mantener hasta migrar consumidores actuales; retirar después | P3 |
| L02 | `FechaCese` plano | mismo | propiedad | PROYECCION_ACTUAL | columna `Trabajador` | periodo aplicable | Se usa como bandera rápida de activo actual | Mantener temporalmente; reemplazar por proyección explícita | P3 |
| L03 | `EmpresaId` plano | mismo | propiedad | PROYECCION_ACTUAL | columna `Trabajador` | empresa del periodo aplicable | Redundancia organizativa | Mantener temporalmente; retirar tras migrar UI/autorización actual | P3 |
| L04 | `PlantaId` plano | mismo | propiedad | PROYECCION_ACTUAL | columna `Trabajador` | planta vigente del periodo aplicable | Redundancia y alcance ligado al snapshot actual | Mantener temporalmente; retirar tras migrar UI/autorización actual | P3 |
| L05 | Estado vigente actual | `Trabajador.cs` | `EstaVigente` | VALIDACION_ACTUAL | `FechaCese is null` | periodo abierto | Duplica la noción de abierto | Sustituir por proyector actual cuando exista | P2 |
| L06 | Apertura inicial | `Trabajador.cs` | `AbrirVinculo` | COMPATIBILIDAD | copia los cuatro planos | nuevo periodo + primera planta | Dual-write deliberado | Conservar mientras alta acepte DTO plano | P2 |
| L07 | Cese | `Trabajador.cs` | `DarDeBaja` | PROYECCION_ACTUAL | escribe `FechaCese` y periodo/planta | periodo abierto exacto | Bajo; escritura atómica validada en 3D | Mantener dual-write hasta retirar proyección | P3 |
| L08 | Cambio de empresa | `Trabajador.cs` | `CambiarEmpresa` | VALIDACION_ACTUAL | compara/escribe `EmpresaId` | nuevo periodo tras cese/reingreso | El nombre sugiere una operación que no crea historia | Reemplazar por caso de uso explícito de reingreso en fase autorizada | P2 |
| L09 | Reingreso de dominio | `Trabajador.cs` | `Reingresar` | PROYECCION_ACTUAL | proyecta los cuatro campos desde el episodio nuevo | periodo y planta nuevos | No hay flujo Application/UI que lo invoque | Mantener; resolver en 3E-B o fase funcional separada | P1 |
| L10 | Cambio de planta | `Trabajador.cs` | `CambiarPlanta` | PROYECCION_ACTUAL | actualiza `PlantaId` tras crear vigencia | `AsignacionPlanta` | Dual-write necesario hoy | Encapsular en proyector actual futuro | P2 |
| L11 | Alta histórica ya cesada | `Trabajador.cs` | `CerrarAsignacionesSiNaceCesado` | IMPORTACION | usa `FechaCese` importada | periodo creado en la misma alta | Dependencia del DTO/importador | Conservar mientras la importación acepte cesados | P2 |
| L12 | Alta de ficha | `Application/Personal/ServicioTrabajadores.cs` | `CrearAsync` | PROYECCION_ACTUAL | solicitud plana; luego `AbrirVinculo` | agregado canónico creado | Dos representaciones en una operación | Futuro comando de alta orientado al episodio | P2 |
| L13 | Edición de ficha | mismo | `ActualizarAsync`, `SincronizarVinculoAbierto` | COMPATIBILIDAD | UI escribe `FechaIngreso`, empresa y planta actuales | periodo abierto + planta vigente | Write-back acoplado; una fecha de ingreso editada corrige el periodo abierto | Separar edición personal de corrección laboral explícita | P1 |
| L14 | Baja de ficha | mismo | `DarDeBajaAsync` | VALIDACION_ACTUAL | acceso por planta plana; dominio cierra canónico | periodo/planta abiertos | Autorización usa propiedad actual, coherente con comando actual | Migrar junto con autorización actual, no aisladamente | P2 |
| L15 | Asignación fechada de cuadrilla | mismo | `AsignarCuadrillaAsync` | USO_HISTORICO_INCORRECTO | `PlantaId`, `FechaIngreso`, `FechaCese` actuales frente a `Desde` | periodo/planta de `Desde` | Puede rechazar o aceptar una asignación pasada según el vínculo actual | Resolver contexto laboral en `Desde` antes de validar | P1 |
| L16 | Vigencias de marcador/turno/sueldo | mismo | `ValidarAsync`, `ReasignarMarcador`, `Reasignar`, `ReasignarTurno` | COMPATIBILIDAD | límites iniciales desde ingreso/cese actuales | periodo abierto del comando | Acoplamiento; correcto solo mientras proyección y abierto coincidan | Recibir explícitamente el periodo abierto y sus límites | P2 |
| L17 | Índices de snapshot | `ConfiguracionesPersonal.cs` | `TrabajadorConfiguracion` | COMPATIBILIDAD | índices por empresa/planta planas | no aplica a historia | Mantiene consumidores actuales y dificulta retiro inmediato | Retirar solo con migración posterior a consumidores | P3 |
| L18 | DTO de Personal | `Application/Personal/Dto/TrabajadorDtos.cs` | lista, detalle, panel, guardar, filtros | UI_ACTUAL | cuatro valores planos | proyección actual derivada | Contrato público de UI conserva redundancia | Reemplazar en una versión compatible posterior | P2 |
| L19 | Listado y filtros de Personal | `ConsultaTrabajadores.cs` | `ListarAsync` | UI_ACTUAL | cuatro columnas planas | proyección actual | Ninguno histórico: pantalla de estado actual | Mantener temporalmente | P3 |
| L20 | Ficha, panel y búsqueda | mismo | `ObtenerDetalleAsync`, `ObtenerPanelAsync`, `BuscarRapidoAsync` | UI_ACTUAL | empresa/planta/fechas planas | proyección actual | Ninguno histórico | Mantener temporalmente | P3 |
| L21 | Historial de trabajador | mismo | `ObtenerHistorialAsync` | NO_APLICA | cabecera usa identidad y alcance actual; filas usan periodos | `PeriodoLaboral` por fila | Sin fallback legacy histórico | Conservar; migrar solo el alcance actual al retirar `PlantaId` | P3 |
| L22 | Integrantes de cuadrilla en fecha | mismo | `IntegrantesDeCuadrillaAsync` | USO_HISTORICO_INCORRECTO | filtra alcance con `Trabajador.PlantaId` actual aunque recibe `fecha` | `AsignacionPlanta` en `fecha` | Traslado puede ocultar o exponer integrantes históricos por la planta actual | Aplicar alcance con contexto laboral histórico | P1 |
| L23 | Candidatos de cuadrilla en fecha | mismo | `CandidatosParaCuadrillaAsync` | USO_HISTORICO_INCORRECTO | `PlantaId` y `FechaCese` actuales; recibe `fecha` | periodo/planta en `fecha` | Una fecha pasada/futura puede ofrecer candidatos incorrectos | Resolver vigencia y planta en la fecha solicitada | P1 |
| L24 | Autorización de ficha actual | `AlcancePlantasConsultas.cs` | `PuedeVerTrabajadorAsync` | VALIDACION_ACTUAL | `Trabajador.PlantaId` | proyección actual | No debe reutilizarse para autorización histórica | Renombrar/segregar explícitamente de alcance histórico | P2 |
| L25 | Justificaciones | `ServicioJustificaciones.cs` | comandos y desplegable actual | VALIDACION_ACTUAL | `PlantaId`, `FechaCese` actuales | proyección actual | Política de propietario actual, no resolución histórica del tareo | Mantener; documentar semántica de autorización actual | P2 |
| L26 | Solicitudes | `ServicioSolicitudes.cs` | comandos y desplegable actual | VALIDACION_ACTUAL | `PlantaId`, `FechaCese` actuales | proyección actual | Igual que L25 | Mantener temporalmente | P3 |
| L27 | Reintegros | `ServicioReintegrosTareo.cs` | acceso y desplegable actual | VALIDACION_ACTUAL | `PlantaId`, `FechaCese` actuales | proyección actual | Selección actual ligada a snapshot | Migrar con la autorización actual | P2 |
| L28 | Protección de catálogo Planta | `ServicioCatalogos.cs` | conteos antes de baja | COMPATIBILIDAD | referencias desde `Trabajador.PlantaId` | proyección actual + historia canónica | El conteo plano no representa toda referencia histórica, aunque las FKs canónicas protegen la historia | Contar ambas fuentes mientras coexistan | P2 |
| L29 | Carga masiva de padrón | `Personal/CargaMasivaTrabajadores.cs` | `Construir`, alta/actualización | IMPORTACION | CSV llena los cuatro campos; delega al servicio | escritor canónico de Personal | Un padrón desactualizado puede intentar corregir el vínculo abierto | Separar alta, actualización administrativa y corrección laboral | P1 |
| L30 | Controlador de Personal | `Web/Controllers/TrabajadoresController.cs` | alta, edición, baja, panel | UI_ACTUAL | transporta DTO actual | servicio/agregado | No escribe EF directamente | Mantener hasta cambiar contrato de UI | P3 |
| L31 | Vistas de Personal | `Views/Trabajadores/*` | listado, formulario, panel, baja | UI_ACTUAL | muestra/edita proyección actual | servicio/agregado al guardar | `FechaIngreso`, empresa y planta son editables; cese usa flujo separado | En futuro separar datos personales de evento laboral | P2 |
| L32 | Cascadas de formulario | `wwwroot/js/trabajador-form.js` | empresa/planta actual | UI_ACTUAL | IDs del formulario | proyección actual | Ninguno histórico | Retirar junto con nuevo contrato UI | P3 |
| L33 | Consulta PMV por rango | `MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs` | consulta de marcaciones provisionales | USO_HISTORICO_INCORRECTO | atribuye por vigencia de marcador, pero etiqueta trabajador con `PlantaId` actual | `AsignacionPlanta` en la fecha de la marca | Traslado puede mostrar la planta equivocada para marcas históricas provisionales | Resolver planta por fecha sin alterar reglas PMV | P1 |
| L34 | Reportes históricos oficiales | `ServicioReportes.cs`, `ConsultaContextoLaboral.cs` | población, filtros, filas y alcance | NO_APLICA | contexto canónico por rango/día | periodos + plantas | No usa los cuatro planos para decisiones históricas; existe comentario antiguo que aún dice lo contrario | Corregir comentario en limpieza documental posterior | P3 |
| L35 | Tareo/asistencia oficial | `ServicioCalculoTareo.cs` | población y cálculo diario | NO_APLICA | contexto canónico por día | periodos + plantas | Sin fallback legacy en ruta crítica | Mantener contratos 3C | P3 |
| L36 | Exportación y tests | `ExportadorReportesExcel.cs`; tests PL3C/PL3D | salida resuelta y evidencia | NO_APLICA | DTO ya resuelto; asserts de proyección | canónico en producción | Las coincidencias textuales no son lectores legacy adicionales | Conservar como evidencia hasta retiro | P3 |

## 5. Usos históricos incorrectos

Se identificaron **4** grupos:

1. `ServicioTrabajadores.AsignarCuadrillaAsync`: valida una fecha efectiva contra ingreso, cese y
   planta actuales en vez del contexto laboral de esa fecha.
2. `ConsultaTrabajadores.IntegrantesDeCuadrillaAsync`: aplica alcance por planta actual al listar
   integrantes para una fecha recibida como parámetro.
3. `ConsultaTrabajadores.CandidatosParaCuadrillaAsync`: selecciona por planta y cese actuales aunque
   la operación está fechada.
4. `ServicioConsultaMarcacionProvisional`: atribuye correctamente por vigencia de marcador, pero
   presenta la planta actual para un rango histórico de marcas provisionales.

Ninguno está dentro de las rutas históricas oficiales de tareo/reportes que Fase 3C migró. Por eso
el resultado previo “fallback histórico crítico = 0” sigue siendo cierto, pero no equivale a “cero
deuda legacy en todo el repositorio”. No se corrigió ninguno en 3E-A.

## 6. Sincronización vigente

| Operación | Fuente autoritativa escrita | Proyección actual actualizada | Responsable | Estado |
|---|---|---|---|---|
| Alta | nuevo `PeriodoLaboral` + primera `AsignacionPlanta` | cuatro campos nacen desde la solicitud | `CrearAsync` → `AbrirVinculo` | Coherente |
| Cese | cierra exactamente periodo y planta abiertos | `FechaCese`, causal y observación | `DarDeBajaAsync` → `DarDeBaja` | Coherente |
| Reingreso de dominio | crea periodo y planta nuevos; no toca cerrados | ingreso, cese `null`, empresa y planta nuevos | `Trabajador.Reingresar` | Coherente; sin flujo Application/UI |
| Cambio de planta | cierra/abre `AsignacionPlanta` dentro del periodo | `PlantaId` nuevo | `ActualizarAsync` → `CambiarPlanta` | Coherente |
| Edición actual | corrige solo periodo abierto y primera planta cuando aplica | UI escribe ingreso/empresa/planta actuales | `ActualizarAsync` → `SincronizarVinculoAbierto` | Coherente pero acoplado |

## 7. Write-back y duplicación lógica

### Write-back

- Ninguna vista o controlador escribe directamente en EF.
- El formulario sí permite modificar `FechaIngreso`, `EmpresaId` y `PlantaId`; el servicio traduce
  esos valores al periodo abierto y a `AsignacionPlanta`. `FechaCese` viaja para presentación y
  detección de manipulación, pero el cese real usa una acción separada.
- La importación masiva reutiliza el mismo DTO y el mismo servicio. Esto evita un bypass técnico,
  pero mezcla actualización de ficha con corrección del vínculo abierto.

### Duplicación encontrada: 3 grupos

1. **Periodo abierto:** `Trabajador.DarDeBaja`, `ServicioTrabajadores.ActualizarAsync` y
   `SincronizarVinculoAbierto` resuelven su cardinalidad por separado. El índice físico protege el
   dato, pero los mensajes y el tipo de fallo pueden divergir.
2. **Activo actual:** `FechaCese == null` se repite en Personal, Justificaciones, Solicitudes y
   Reintegros, además de `Trabajador.EstaVigente` y DTO. Es una proyección legítima hoy, pero no una
   abstracción única.
3. **Alcance por planta actual:** existe el helper `PuedeVerTrabajadorAsync`, pero Personal y varias
   rutas de Justificaciones aplican comprobaciones directas. Ninguna de ellas debe confundirse con
   el alcance histórico canónico de Reportes/Tareo.

No se refactorizó ninguno en esta fase.

## 8. Campos que deben permanecer temporalmente

Los cuatro campos deben permanecer por ahora:

- `FechaIngreso`: formulario, filtros, listado, validaciones y arranque de asignaciones actuales.
- `FechaCese`: bandera de vigencia actual para UI y desplegables operativos.
- `EmpresaId`: filtros, formulario y proyección empresarial actual.
- `PlantaId`: UI, aislamiento de ficha/comando actual, catálogos e integraciones actuales.

Retirarlos ahora rompería contratos de UI, importación, autorización y consultas actuales. Su
permanencia no les devuelve autoridad histórica.

## 9. Candidatos de eliminación futura

Los cuatro son `CANDIDATO_ELIMINACION`, pero solo después de esta secuencia:

1. crear una proyección actual explícita derivada del periodo abierto/último episodio;
2. separar comandos de datos personales, corrección laboral, cambio de planta y reingreso;
3. migrar filtros, DTO, UI, importación y autorización actual;
4. eliminar dual-write y añadir verificación de ausencia de consumidores;
5. recién entonces considerar una migración física para retirar columnas e índices legacy.

No se creó ni se recomienda una migración en 3E-A.

## 10. Priorización consolidada

| Prioridad | Cantidad | Deuda |
|---|---:|---|
| P0 | 0 | No se encontró una ruta normal que corrompa inmediatamente la historia canónica |
| P1 | 7 | 4 usos históricos incorrectos; write-back acoplado de edición; importación ambigua de actualización; flujo de reingreso ausente |
| P2 | 14 | Dual-write, validaciones actuales y tres grupos de lógica duplicada que prolongan el acoplamiento |
| P3 | 15 | Compatibilidad/UI y limpieza futura, incluidos campos, índices, vistas y comentario obsoleto |

Las cantidades suman los **36 grupos** de la matriz. La duplicación se describe transversalmente y
no añade filas artificiales al total.

## 11. Cambios y evidencia

- Código de producción: **ningún cambio**.
- Tests nuevos: **0**; la semántica ya está demostrada por contratos PL3C y PL3D.
- Migraciones: **ninguna**.
- Documentación maestra: **no actualizada**.
- Producción: **no tocada**.
- OP-03: **no ejecutado**.
- Commit: **no realizado**.

Validación integral final:

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 415/415 |
| Infrastructure | 476/476 |
| Web | 23/23 |
| Total | 914/914 verdes; 0 fallos; 0 omitidos |

## 12. Conclusión

La deuda legacy quedó inventariada y clasificada. Los campos planos siguen siendo una proyección
actual necesaria para compatibilidad, pero todos son candidatos de eliminación futura. Las rutas
históricas oficiales permanecen canónicas; los cuatro usos fechados residuales quedan identificados
para una fase posterior, sin modificación funcional en 3E-A.

Estado siguiente permitido: **LISTO PARA FASE 3E-B**. Esto no autoriza ejecutar 3E-B ni implica
preparación para producción.

> **Nota de cierre 3E-B — 2026-10-14:** los cuatro grupos clasificados como
> `USO_HISTORICO_INCORRECTO` (L15, L22, L23 y L33) fueron corregidos y cubiertos por 11 contratos
> dirigidos. Esta matriz se conserva como evidencia del inventario previo; el resultado vigente se
> documenta en `P1_DATA_PERIODOLABORAL_FASE3E_B.md`.

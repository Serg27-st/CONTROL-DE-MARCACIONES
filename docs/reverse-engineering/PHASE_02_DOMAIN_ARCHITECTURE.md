# Fase 2 — Dominio y arquitectura detallada

Fecha de corte: 2026-09-29  
Estado: completada  
Alcance: ingeniería inversa documental; sin cambios de código funcional, esquema, migraciones, rendimiento ni refactorización.

## 1. Resumen ejecutivo

El sistema es un monolito modular por capas para controlar trabajadores, equipos de marcación, importaciones, horarios, rotaciones, ausencias, cálculo de tareo, ajustes y reportes. El núcleo funcional no es la marca aislada, sino la reconstrucción de una jornada laboral para una fecha: se determina a quién pertenece un código de marcador, qué turno le corresponde por asignación directa o cuadrilla, qué versión del horario regía, qué marcas caen en la ventana y qué justificaciones o incidencias modifican el resultado.

La arquitectura física conserva una dirección de dependencias limpia entre proyectos: Domain no depende de infraestructura; Application define contratos y cálculo puro; Infrastructure implementa persistencia e integraciones; Web expone MVC/Razor. Sin embargo, las fronteras funcionales no equivalen a ensamblados independientes. Los servicios de cálculo y reportes consultan varias áreas del modelo mediante MarcacionDbContext, por lo que tareo y reportes son puntos de alto acoplamiento funcional.

Las historias temporales están modeladas con distinto grado de madurez. Marcador, turno, sueldo y área tienen asignaciones fechadas; el horario tiene VersionTurno; las cuadrillas tienen programaciones y tramos. En cambio, la planta del trabajador es un campo actual sin historial, y PeriodoLaboral coexiste con FechaIngreso, FechaCese y EmpresaId del registro actual sin ser utilizado por los flujos principales de cálculo y reporte. Esa asimetría es el principal riesgo arquitectónico hallado en esta fase.

La rotación por cuadrilla sí soporta secuencias de tramos de turnos con duración variable y repetición modular. No soporta de forma directa un tramo explícito de descanso: cada tramo exige un TurnoId y un turno válido debe tener días aplicables. Por ello, «dos semanas de día, un día de descanso, una semana de noche y repetir» está soportado solo parcialmente.

## 2. Alcance, método y nivel de certeza

Se tomó como punto de partida PHASE_01_DISCOVERY.md y CONTEXT_REGISTER.md. Esta fase profundizó únicamente en archivos necesarios para cerrar relaciones, vigencias, resolución de horarios, cálculo de jornada, generación de justificaciones, reportes, seguridad y configuración relacional.

Convenciones:

| Etiqueta | Significado |
|---|---|
| CONFIRMADO | Comportamiento observado directamente en entidades, servicios, contratos o configuración EF. |
| INFERIDO | Conclusión consistente con varias evidencias, pero no probada todavía mediante ejecución o base real. |
| NO DETERMINADO | Falta una decisión humana, dato productivo o validación de ejecución. |

No se auditó aún el estado físico de una base productiva, el contenido de migraciones, planes de consulta, cobertura exhaustiva de pruebas ni concurrencia bajo carga. Esos temas pertenecen a fases posteriores.

## 3. Mapa funcional → técnico completo

| Concepto funcional | Entidad o clase principal | Contrato / interfaz | Implementación o consumidor | Persistencia y relaciones | Responsabilidad y estado |
|---|---|---|---|---|---|
| 1. Empresa | Empresa | IConsultaCatalogos, servicios de trabajadores | ServicioTrabajadores, carga masiva, reportes y controladores de catálogo | Tabla de empresas; Trabajador.EmpresaId y PeriodoLaboral.EmpresaId | Identifica al empleador. El trabajador conserva empresa actual y también puede registrar empresa por periodo laboral. |
| 2. Planta | Planta | IConsultaCatalogos, IProveedorAlcancePlantas | ProveedorAlcancePlantasHttp, ServicioTrabajadores, reportes y varios controladores | Trabajador.PlantaId, EquipoMarcador.PlantaId, Cuadrilla.PlantaId y UsuarioPlanta | Unidad de operación y frontera de autorización. No existe historial de planta del trabajador. |
| 3. Área | Area | IConsultaCatalogos, IConsultaTrabajadores | ServicioTrabajadores, reportes, carga masiva | Trabajador.AreaId actual más AsignacionArea temporal | Clasificación organizativa con historia explícita. El valor actual y la línea temporal deben permanecer sincronizados. |
| 4. Sección | Seccion | IConsultaCatalogos | ServicioTrabajadores, vistas y filtros | Trabajador.SeccionId; la sección pertenece a un área | Nivel organizativo actual, sin asignación histórica propia observada. |
| 5. Centro de costo | CentroCosto en CatalogosSimples | IConsultaCatalogos | ServicioTrabajadores, carga masiva, reportes | Trabajador.CentroCostoId | Clasificación contable actual. No se confirmó historial temporal. |
| 6. Trabajador | Trabajador | ITrabajadorRepository, IConsultaTrabajadores | ServicioTrabajadores, CargaMasivaTrabajadores y casi todos los flujos operativos | Raíz de personal; relaciona asignaciones, periodos, tareos, ausencias y reintegros | Mantiene identidad y estado laboral actual. Su cese cierra asignaciones abiertas de marcador, turno y sueldo. |
| 7. Periodo laboral | PeriodoLaboral | No tiene contrato exclusivo | ServicioTrabajadores lo mantiene; reportes y cálculo no lo usan como filtro principal | Historial por trabajador, empresa y rango de fechas | Registra episodios laborales. Coexiste con campos planos actuales; su autoridad histórica no está consumida uniformemente. |
| 8. Equipo marcador | EquipoMarcador | IServicioEquipos | ServicioEquipos, ServicioImportacion y controladores | Pertenece a planta; contiene marcadores e importaciones | Representa el dispositivo físico, serie y origen de archivos. |
| 9. Marcador / código | Marcador | IServicioMarcadores | ServicioMarcadores, ServicioTrabajadores y carga masiva | Clave funcional por equipo y código; tiene PlantaId derivado del equipo | Catálogo de códigos existentes en cada equipo. No asigna por sí solo el código a una persona. |
| 10. Asignación de marcador | AsignacionMarcador | Operada mediante servicios de trabajadores y marcadores | ServicioTrabajadores, atribución de marcas, consultas | Trabajador + EquipoId + IdMarcador + vigencia; una abierta por trabajador y por código/equipo | Determina quién era dueño de un código en una fecha. Es la base de la atribución temporal. |
| 11. Importación | ImportacionMarcador | IServicioImportacion, IFuenteDeMarcaciones | ServicioImportacion, lectores de archivo, panel y controlador | Archivo, hash, equipo, serie, origen, errores y estado de anulación; origina marcas crudas | Traza una carga y permite excluirla lógicamente mediante anulación. |
| 12. Marcación cruda | MarcacionCruda | Accedida por importación y cálculo | ServicioImportacion y ServicioCalculoTareo | Equipo + código + fecha/hora; asociada a importación; no guarda TrabajadorId | Hecho inmutable de origen. Su atribución a trabajador se resuelve por fecha, no se materializa en la marca. |
| 13. Atribución de marcaciones | AtribucionMarcaciones | Sin contrato público independiente | Lógica consultada por importación/cálculo | Se determina con equipo + código + fecha frente a AsignacionMarcador | Traduce una marca física a trabajador respetando vigencia. |
| 14. Turno | Turno | IServicioTurnos, IConsultaCatalogos | ServicioTurnos, ServicioTrabajadores, ServicioCuadrillas y cálculo | Identidad y horario actual; relaciones con plantas, versiones, asignaciones y tramos | Define horas, comida, tolerancias, ventanas, gracia, minutos ordinarios y días aplicables. |
| 15. Versión de turno | VersionTurno | Gestionada por IServicioTurnos | ServicioTurnos y ServicioCalculoTareo | Historial temporal por Turno; una versión abierta | Es la verdad histórica del horario para una fecha. Si falta, el cálculo cae al Turno actual. |
| 16. Cuadrilla | Cuadrilla | IServicioCuadrillas | ServicioCuadrillas, ServicioTrabajadores y cálculo | Pertenece a planta; posee programaciones; puede recibir asignaciones de trabajadores | Agrupa trabajadores cuya selección de turno depende de una rotación común. |
| 17. Programación de cuadrilla | ProgramacionCuadrilla | IServicioCuadrillas | ServicioCuadrillas y ServicioCalculoTareo | Cuadrilla + vigencia + FechaAncla + colección ordenada de tramos | Define una versión temporal del ciclo rotativo. La fecha ancla fija el origen del módulo. |
| 18. Tramo de rotación | TramoRotacion | Parte del contrato de cuadrillas | ProgramacionCuadrilla.TramoEn y TurnoEn | Orden, TurnoId y cantidad de días dentro de una programación | Segmento consecutivo de un turno. No representa descanso porque exige un turno. |
| 19. Periodo de tareo | Periodo | IServicioPeriodos | ServicioPeriodos y todos los flujos que bloquean o reportan por periodo | Rango de fechas, estado abierto/cerrado y versión de concurrencia | Frontera de edición. Cambios que intersectan periodos cerrados se bloquean en servicios críticos. |
| 20. Jornada calculada | EntradaCalculo, ResultadoCalculo y método interno Jornada | IServicioCalculoTareo | ServicioCalculoTareo y CalculadoraTareo | No existe tabla Jornada; TareoDiario persiste su proyección resultante | Unidad conceptual trabajador-fecha-turno-ventana-marcas. Se construye dinámicamente y se asigna a la fecha de inicio. |
| 21. Tareo diario | TareoDiario | IServicioCalculoTareo | ServicioCalculoTareo, incidencias, panel y reportes | Único por trabajador y fecha; guarda entradas, salidas, minutos, extras, nocturnos, incidencias y validación | Resultado persistido recalculable de una jornada. Es salida del cálculo, no la fuente original de horario o marcas. |
| 22. Ausencia y justificación | SolicitudAusencia, Justificacion, TipoAusencia | IServicioSolicitudes, IServicioJustificaciones | ServicioSolicitudes, ServicioJustificaciones y cálculo | Solicitud con rango; al aprobar crea justificaciones diarias. Una justificación activa por trabajador/fecha | Flujo de aprobación y evidencia diaria que modifica el cálculo. Solo una justificación aprobada tiene efecto. |
| 23. Incidencia | TipoIncidencia más campos de TareoDiario | IServicioCalculoTareo y acciones MVC de incidencias | CalculadoraTareo, ServicioCalculoTareo y controladores | No hay entidad Incidencia; estado persistido dentro de TareoDiario | Clasifica el resultado diario y la necesidad de validación. La regla admite una incidencia prioritaria por día. |
| 24. Reintegro y reporte | ReintegroTareo; modelos de reporte | IServicioReintegrosTareo, IServicioReportes, IExportadorReportesExcel | Servicios de reintegros/reportes y exportador XLSX | Reintegro único por trabajador/periodo; el reporte no se persiste y combina varias fuentes | El reintegro es ajuste manual separado del cálculo. El reporte es una lectura derivada de tareos, datos laborales, parámetros y ajustes. |

Elementos transversales:

- Usuario y roles: ApplicationUser, Identity, RolesSistema y UsuarioPlanta.
- Parámetros legales y tasas: ParametroLegal y TasaSobretasa, consumidos según vigencia.
- Auditoría: RegistroAuditoria, alimentado automáticamente por un interceptor de EF Core.
- Cobertura de carga: CoberturaDeCarga e ImportacionMarcador permiten razonar sobre qué fechas/equipos fueron efectivamente importados.

## 4. Arquitectura del dominio y agregados temporales

La cadena central del dominio es:

Trabajador → AsignacionMarcador → MarcacionCruda → resolución de Turno → Jornada calculada → TareoDiario → Reporte.

Alrededor de ella operan cuatro contextos temporales:

1. Identidad laboral: Trabajador y PeriodoLaboral.
2. Pertenencia operativa: AsignacionMarcador, AsignacionArea, AsignacionTurno y AsignacionSueldo.
3. Definición de horario: Turno, VersionTurno, Cuadrilla, ProgramacionCuadrilla y TramoRotacion.
4. Excepciones y cierre: Periodo, feriados, Justificacion, incidencias, parámetros legales y reintegros.

Las vigencias usan rangos inclusivos y una fecha final nula como tramo abierto. ValidadorVigencias comprueba solapamientos antes de guardar, y las configuraciones EF agregan restricciones de unicidad para varias asignaciones abiertas. La defensa está repartida entre dominio, servicios y base de datos: no todas las líneas temporales tienen exactamente la misma protección física.

Trabajador funciona como raíz práctica del área de personal, pero no encapsula toda mutación; los servicios coordinan el contexto EF y múltiples tablas. ProgramacionCuadrilla sí se comporta como un agregado claro: su fecha ancla y colección ordenada de tramos solo tienen sentido juntas. Turno y VersionTurno forman otro conjunto temporal donde la identidad actual se separa del horario histórico.

## 5. Cadena temporal detallada de cálculo

Para una fecha de trabajo, el comportamiento confirmado es el siguiente:

1. El trabajador se considera desde sus datos laborales actuales y filtros del servicio consumidor.
2. La atribución de cada MarcacionCruda se resuelve por EquipoId, IdMarcador y vigencia de AsignacionMarcador.
3. ServicioCalculoTareo busca la AsignacionTurno vigente del trabajador; ante más de una coincidencia defensiva, prioriza la de inicio más reciente.
4. Si la asignación contiene TurnoId, utiliza ese turno.
5. Si contiene CuadrillaId, busca la ProgramacionCuadrilla vigente y calcula el tramo mediante FechaAncla y módulo positivo sobre la suma de días.
6. Con el TurnoId resuelto, busca VersionTurno vigente y aplicable a la fecha. Solo si no existe versión usa los campos actuales de Turno.
7. Si el turno no aplica al día de semana, la fecha queda sin horario aun cuando el ciclo apunte a ese turno.
8. Construye una ventana desde la entrada teórica menos la anticipación permitida hasta la salida teórica más un margen operativo de seis horas.
9. Para turnos nocturnos, la jornada pertenece a la fecha de inicio. Las marcas que ya pertenecen a la ventana anterior no se vuelven a consumir.
10. La calculadora pura toma primera y última marca útiles, tolerancias, comida, feriado, justificación y parámetros; obtiene horas ordinarias, extras, nocturnas, tardanza, salida anticipada e incidencia.
11. ServicioCalculoTareo vuelca ResultadoCalculo a TareoDiario, que queda como proyección persistida.
12. Los reportes leen TareoDiario y lo enriquecen con trabajador, asignaciones históricas disponibles, parámetros y ReintegroTareo.

Esta secuencia hace que la reproducibilidad histórica dependa de conservar correctamente las asignaciones, versiones, importaciones no anuladas, justificaciones y parámetros vigentes.

## 6. Turnos, vigencias y rotaciones A–F

### A. ¿Cómo se determina el turno de un trabajador en una fecha?

CONFIRMADO. Se consulta AsignacionTurno por trabajador y rango de vigencia. La asignación contiene exactamente una referencia: TurnoId o CuadrillaId. Una asignación directa entrega el turno inmediatamente. Una asignación por cuadrilla exige resolver primero la programación vigente y después su tramo para la fecha.

### B. ¿Puede coexistir asignación directa y por cuadrilla?

CONFIRMADO. No dentro de una misma AsignacionTurno: una restricción exige exactamente uno de los dos identificadores. En la entrada de ServicioTrabajadores, si llegan ambos, la cuadrilla prevalece y el turno directo se anula. Las validaciones temporales buscan impedir asignaciones solapadas; la selección por fecha y mayor inicio es una defensa adicional ante datos inconsistentes, no una regla de precedencia de negocio entre dos asignaciones legítimas.

### C. ¿Qué vigencias participan?

CONFIRMADO. Intervienen tres líneas temporales encadenadas:

- AsignacionTurno decide turno directo o cuadrilla para la fecha.
- ProgramacionCuadrilla decide qué ciclo estaba vigente para esa cuadrilla.
- VersionTurno decide qué definición horaria regía para el turno resultante.

Las modificaciones cierran el tramo anterior y abren uno nuevo desde la fecha efectiva. Los servicios impiden cambios que alcancen periodos ya cerrados.

### D. ¿Dónde vive el horario?

CONFIRMADO. Turno conserva la definición actual editable. VersionTurno conserva instantáneas fechadas. ServicioTurnos actualiza el estado actual y ajusta la versión: puede corregir la abierta cuando la fecha efectiva no crea una separación o cerrar la anterior el día previo y abrir una nueva. Para cálculo histórico, VersionTurno es la fuente preferida; recurrir al Turno actual es una compatibilidad defensiva cuando no hay versión.

### E. ¿Cómo funciona el ciclo de cuadrilla?

CONFIRMADO. ProgramacionCuadrilla contiene FechaAncla y tramos ordenados, cada uno con TurnoId y Dias. La diferencia de días respecto del ancla se normaliza mediante módulo positivo por la duración total del ciclo. El desplazamiento resultante cae en un tramo y entrega su turno. «Correr» una rotación desplaza el ancla; reprogramar desde una fecha cierra o reemplaza programaciones según su vigencia.

### F. ¿Soporta 2 semanas de día → 1 descanso → 1 semana de noche → repetir?

Resultado: PARCIALMENTE SOPORTADO.

- Sí puede expresar un tramo de 14 días con turno diurno seguido de 7 días con turno nocturno y repetirlos.
- No puede expresar directamente un tramo intermedio de un día sin turno: TramoRotacion exige TurnoId.
- Crear un «turno descanso» tampoco encaja con las reglas actuales porque un turno necesita días aplicables y el motor diferencia la no aplicación semanal del avance del ciclo.
- Turno.DiasAplicables permite descansos fijos por día de semana, pero eso no equivale a un descanso móvil cada 22 días.

Por tanto, si el descanso es un día explícito dentro del ciclo rotativo, el modelo actual no reproduce exactamente la regla. Si «descanso» significa simplemente que el turno del tramo no aplica en determinado día semanal, podría aproximarse, pero sería otra regla de negocio. La interpretación correcta requiere confirmación humana antes de cualquier diseño futuro.

## 7. El concepto de jornada

No existe una entidad persistida Jornada ni una tabla equivalente. El concepto está distribuido entre:

- ServicioCalculoTareo: atribuye marcas, resuelve turno y versión, delimita la ventana y evita doble consumo entre jornadas.
- EntradaCalculo: transporta los hechos y parámetros necesarios para una fecha.
- CalculadoraTareo y su rutina interna de jornada: ejecutan las reglas puras.
- ResultadoCalculo: expresa el resultado transitorio.
- TareoDiario: persiste la proyección final.

La identidad conceptual de una jornada es «trabajador + fecha de inicio». En turnos que cruzan medianoche, las marcas del día siguiente se asignan a la fecha en que inició el turno. La ventana comienza antes de la entrada teórica según la anticipación configurada y termina después de la salida teórica con un margen operativo. Dentro de ella, el cálculo usa la primera y la última marca; las intermedias no forman pares de entrada/salida.

Implicaciones:

- TareoDiario puede recalcularse y cambiar si cambian entradas temporales aún permitidas.
- No queda una relación persistida explícita entre el tareo y cada MarcacionCruda utilizada; la trazabilidad se reconstruye.
- Una marca cruda no conoce al trabajador; su significado depende de AsignacionMarcador.
- La ventana de jornada es parte del comportamiento del servicio, no una entidad ni configuración totalmente centralizada.

## 8. Matriz de dependencias entre módulos

La matriz describe módulos funcionales, no proyectos. «Alta» significa que el módulo consumidor necesita varias estructuras o reglas del proveedor; no implica por sí sola deuda.

| Módulo consumidor | Depende de | Contratos o acceso observado | Persistencia implicada | Acoplamiento preliminar |
|---|---|---|---|---|
| Organización | Ningún módulo de negocio superior | Catálogos y entidades base | Empresa, Planta, Area, Seccion, CentroCosto | Bajo; módulo base. |
| Personal | Organización | IConsultaCatalogos y DbContext | Trabajador, PeriodoLaboral, AsignacionArea | Medio; mantiene campos actuales e historia parcial. |
| Personal | Equipos/marcadores | IServicioMarcadores | AsignacionMarcador, Marcador, EquipoMarcador | Medio; asignación temporal validada contra catálogo físico. |
| Personal | Turnos/cuadrillas | IServicioTurnos, IServicioCuadrillas o consultas de catálogo | AsignacionTurno, Turno, Cuadrilla | Medio; el alta y la edición coordinan otra línea temporal. |
| Importaciones | Equipos/marcadores | IServicioEquipos e IFuenteDeMarcaciones | ImportacionMarcador, EquipoMarcador, MarcacionCruda | Alto por validación de equipo, archivo, cobertura y deduplicación. |
| Tareo | Personal | Acceso directo al contexto y alcance de plantas | Trabajador, AsignacionMarcador | Alto; población y atribución dependen de historia laboral. |
| Tareo | Turnos/cuadrillas | Acceso directo a entidades temporales | AsignacionTurno, ProgramacionCuadrilla, TramoRotacion, VersionTurno | Alto; encadena tres vigencias. |
| Tareo | Importaciones/marcaciones | IServicioImportacion y acceso al contexto | ImportacionMarcador, MarcacionCruda, CoberturaDeCarga | Alto; determina cobertura y marcas efectivas. |
| Tareo | Ausencias/catálogos | Acceso directo a justificaciones, feriados y parámetros | Justificacion, TipoAusencia, ParametroLegal, TasaSobretasa | Alto; modifica el resultado normativo. |
| Solicitudes | Personal, periodos y catálogos | IProveedorAlcancePlantas y DbContext | SolicitudAusencia, Justificacion, Periodo, TipoAusencia | Medio; aprobar expande una solicitud en hechos diarios. |
| Reintegros | Personal y periodos | IServicioReintegrosTareo | ReintegroTareo, Trabajador, Periodo | Medio; ajuste manual separado del motor diario. |
| Reportes | Personal, organización, tareo, reintegros y parámetros | IServicioReportes y acceso directo al contexto | TareoDiario más tablas de enriquecimiento | Muy alto; es el principal agregador de lectura. |
| Seguridad | Organización | IProveedorAlcancePlantas, Identity | ApplicationUser, UsuarioPlanta, Planta | Medio; aplica alcance transversal por planta. |
| Auditoría | Todos los cambios EF | Interceptor y proveedor de usuario actual | RegistroAuditoria | Medio y transversal; observa persistencia, no reglas de negocio. |
| Web | Todos los contratos de aplicación | Inyección de interfaces y acciones MVC | No persiste por sí mismo, salvo proveedor de alcance con DbContext | Medio; composición e interacción. |

Dependencias técnicas destacadas:

- ServicioPanelInicio orquesta periodos, importaciones, cálculo, solicitudes y reportes mediante interfaces.
- CargaMasivaTrabajadores, ubicada en Infrastructure, depende de la clase concreta ServicioTrabajadores además de contratos; es un acoplamiento más fuerte que el patrón general.
- ProveedorAlcancePlantasHttp vive en Web e interroga MarcacionDbContext, combinando identidad HTTP y persistencia.
- ServicioCalculoTareo y ServicioReportes son consultas multi-módulo deliberadas y concentran el conocimiento de integración.

## 9. Fuentes de verdad

| Dato o decisión | Clasificación | Fuente efectiva | Observación |
|---|---|---|---|
| Identidad y estado actual del trabajador | FUENTE ÚNICA | Trabajador | Nombre, documento, alta y estado actual. |
| Episodios de empleo y empresa histórica | POSIBLE DUPLICACIÓN | PeriodoLaboral frente a FechaIngreso, FechaCese y EmpresaId de Trabajador | Los flujos principales observados siguen usando campos planos. |
| Planta actual del trabajador | FUENTE ÚNICA | Trabajador.PlantaId | No hay fuente histórica equivalente. |
| Área actual e histórica | FUENTE DISTRIBUIDA | Trabajador.AreaId + AsignacionArea | Duplicación intencional actual/histórica, sincronizada por servicio. |
| Sección y centro de costo | FUENTE ÚNICA | Campos actuales de Trabajador | No se confirmó historia. |
| Equipo físico | FUENTE ÚNICA | EquipoMarcador | Serie y planta operativa del dispositivo. |
| Planta de un marcador | POSIBLE DUPLICACIÓN | Marcador.PlantaId frente a EquipoMarcador.PlantaId | El primer valor se deriva del segundo. |
| Existencia de un código de marcador | FUENTE ÚNICA | Marcador | Catálogo por equipo. |
| Propietario del código en una fecha | FUENTE ÚNICA | AsignacionMarcador | Resuelve trabajador por vigencia. |
| Marca original | FUENTE ÚNICA | MarcacionCruda | La efectividad también depende de que su ImportacionMarcador no esté anulada. |
| Origen y estado de una carga | FUENTE ÚNICA | ImportacionMarcador | Archivo, hash, equipo, errores, cobertura y anulación. |
| Asignación de horario | FUENTE ÚNICA | AsignacionTurno | Directa o por cuadrilla, nunca ambas en la misma fila. |
| Definición actual e histórica del horario | POSIBLE DUPLICACIÓN | Turno + VersionTurno | Duplicación intencional; cálculo histórico prefiere la versión. |
| Ciclo de rotación | FUENTE DISTRIBUIDA | ProgramacionCuadrilla + FechaAncla + TramoRotacion ordenado | Ninguna pieza aislada representa el ciclo. |
| Jornada de una fecha | FUENTE DISTRIBUIDA | Asignaciones + versión de turno + ventana + marcas + justificación + parámetros | No existe una entidad Jornada. |
| Resultado diario calculado | FUENTE ÚNICA | TareoDiario | Es la última proyección persistida; puede ser reemplazada por recálculo. |
| Incidencia diaria | FUENTE ÚNICA | Campos de TareoDiario | Las reglas de derivación viven en la calculadora. |
| Ausencia solicitada | FUENTE ÚNICA | SolicitudAusencia | Estado y rango solicitado. |
| Efecto diario aprobado de una ausencia | FUENTE ÚNICA | Justificacion | Se genera por día y solo aprobada afecta cálculo. |
| Ajuste por periodo | FUENTE ÚNICA | ReintegroTareo | Separado de TareoDiario. |
| Reporte | FUENTE DISTRIBUIDA | TareoDiario + personal/organización + asignaciones + parámetros + reintegros | Se deriva en cada consulta/exportación. |
| Rol y plantas autorizadas de un usuario | FUENTE DISTRIBUIDA | Identity + UsuarioPlanta + constantes RolesSistema | La autorización combina identidad y alcance organizativo. |
| Auditoría persistida | FUENTE ÚNICA | RegistroAuditoria | Bitácora automática del interceptor. |
| Regla exacta de descanso rotativo de negocio | NO DETERMINADO | No está representada inequívocamente | Requiere definición humana. |

## 10. Fronteras arquitectónicas y posibles violaciones

### Domain

CONFIRMADO. Contiene entidades, invariantes, enums y lógica pura de vigencias/rotación. No se observó dependencia hacia EF, ASP.NET o servicios externos. Las invariantes más importantes incluyen exclusión entre turno directo y cuadrilla, cálculo modular de tramos y detección de solapamientos.

### Application

CONFIRMADO. Define los contratos consumidos por Web, DTO, modelos de consulta, ServicioTrabajadores, ServicioPanelInicio y la calculadora pura. No es una capa exclusivamente declarativa: contiene orquestación de casos de uso y reglas.

### Infrastructure

CONFIRMADO. Implementa persistencia, Identity, importación/exportación, auditoría y la mayoría de servicios de aplicación. Varias implementaciones acceden directamente a conjuntos de diferentes módulos en MarcacionDbContext. Esto corresponde a un monolito modular pragmático, no a repositorios aislados por agregado.

### Web

CONFIRMADO. Compone dependencias, configura autenticación/autorización y expone controladores y vistas. ProveedorAlcancePlantasHttp consulta MarcacionDbContext directamente. Es una dependencia Web → Infrastructure válida a nivel de proyecto, pero mezcla contexto HTTP y consulta persistente dentro de la frontera de presentación.

### Hallazgos clasificados

| Hallazgo | Estado | Interpretación |
|---|---|---|
| Dirección de referencias entre proyectos sin ciclo | CONFIRMADO | La estructura de capas es coherente. |
| Servicios de cálculo y reporte cruzan múltiples módulos vía DbContext | CONFIRMADO | Alto acoplamiento funcional; razonable como integración, pero concentra riesgo de cambio. |
| CargaMasivaTrabajadores depende de ServicioTrabajadores concreto | CONFIRMADO | Excepción al uso habitual de contratos y mayor acoplamiento entre Infrastructure y una implementación de Application. |
| Web consulta DbContext para alcance de plantas | CONFIRMADO | Frontera híbrida de seguridad/persistencia. No se calificó como error funcional. |
| Modelo actual e histórico duplicado en trabajador, área y turno | CONFIRMADO | Requiere que todos los escritores pasen por servicios sincronizadores. |
| Podrían existir divergencias por escritura directa o concurrencia | INFERIDO | Debe validarse en auditoría de persistencia/concurrencia; no se afirma que ya existan datos corruptos. |
| Comentarios de seguridad dicen que el alcance por planta no se aplica | CONFIRMADO como deriva documental | El código actual sí inyecta y aplica IProveedorAlcancePlantas en servicios relevantes. |
| Comentarios de configuración mencionan SQL Server pese al uso de SQLite | CONFIRMADO como deriva documental | Riesgo de interpretación, no defecto de ejecución demostrado. |
| Autorización completa de cada acción | NO DETERMINADO | Requiere revisión sistemática de controladores/policies, fuera del alcance detallado de esta fase. |

## 11. Backend, frontend y seguridad

El backend expone casos de uso mediante controladores MVC, no mediante una API independiente versionada. Las respuestas alternan vistas completas, parciales HTML, JSON y archivos XLSX. Las interfaces de Application desacoplan a los controladores de la mayoría de implementaciones concretas.

El frontend usa Razor y JavaScript vanilla. fetch se utiliza para paneles y formularios parciales, búsquedas, selectores encadenados, filtros de reportes y mantenimiento. No hay evidencia de SPA, gestión de estado global o cliente API separado. La fuente de estado autoritativa permanece en el servidor.

La seguridad combina:

- ASP.NET Core Identity para usuarios y roles.
- RolesSistema como vocabulario de roles.
- UsuarioPlanta para limitar el universo de plantas.
- IProveedorAlcancePlantas para aplicar el alcance en consultas y servicios.
- Navegación de _Layout condicionada por rol.

Ocultar enlaces en la interfaz no sustituye la autorización del servidor. Se confirmó que existe filtrado de alcance en varios servicios críticos, pero la cobertura exhaustiva por acción queda pendiente de una fase de auditoría específica.

## 12. Diagramas versionables

Se generaron los siguientes artefactos Mermaid:

1. [D01 — Contexto del sistema](diagrams/D01_SYSTEM_CONTEXT.md)
2. [D02 — Capas y contenedores](diagrams/D02_CONTAINERS_LAYERS.md)
3. [D03 — Componentes backend](diagrams/D03_BACKEND_COMPONENTS.md)
4. [D04 — Navegación frontend](diagrams/D04_FRONTEND_NAVIGATION.md)
5. [D05 — Modelo de dominio](diagrams/D05_DOMAIN_MODEL.md)
6. [D13 — Dependencias entre módulos](diagrams/D13_MODULE_DEPENDENCIES.md)

Los diagramas distinguen relaciones conceptuales de relaciones físicas. En particular, D05 advierte que Jornada no es una entidad y que no existe una tabla puente confirmada entre TareoDiario y las marcas usadas.

## 13. Riesgos, deuda técnica candidata y dudas humanas

### Riesgos confirmados

1. Historia laboral incompleta en consumidores: PeriodoLaboral existe, pero cálculo y reportes observados filtran principalmente con FechaIngreso, FechaCese y EmpresaId actuales. Una recontratación o cambio de empresa puede alterar la lectura histórica.
2. Planta sin historia: Trabajador.PlantaId es actual. Un traslado puede hacer que reportes históricos aparezcan bajo la nueva planta o queden fuera del alcance anterior.
3. Doble representación actual/histórica: Turno/VersionTurno y Trabajador/AsignacionArea dependen de sincronización correcta en servicios.
4. Descanso rotativo no representable como tramo: el modelo de rotación obliga a elegir un turno.
5. Trazabilidad reconstruida de jornada: TareoDiario no conserva referencias explícitas a todas las marcas de entrada usadas.
6. Concentración de reglas: ServicioCalculoTareo y ServicioReportes conocen gran parte del modelo y son sensibles a cambios transversales.
7. Documentación interna desactualizada: hay comentarios que contradicen la implementación actual de alcance por planta y el proveedor SQLite.

### Riesgos inferidos que requieren verificación posterior

1. Escrituras que eludan los servicios podrían desincronizar valores actuales e históricos.
2. Programaciones concurrentes de cuadrilla podrían necesitar defensas adicionales de base; el servicio normaliza vigencias, pero esta fase no probó carreras.
3. El margen fijo posterior a la salida puede producir solapamientos difíciles en secuencias atípicas; el motor contiene defensa de consumo, pero falta prueba exhaustiva de casos límite.
4. La selección de primera y última marca puede no representar pausas o múltiples entradas si el negocio espera pareo de marcaciones.

### No clasificado como deuda todavía

- Acceso directo a DbContext desde servicios: es una elección arquitectónica coherente con un monolito y necesita evidencia de daño antes de proponerse reemplazo.
- Ubicación de implementaciones de casos de uso en Infrastructure: es consistente con la composición actual aunque la frontera no sea una Clean Architecture estricta.
- Uso de Razor y JavaScript sin framework: no es deuda por sí mismo.

### Dudas que requieren respuesta humana

1. En la regla «14 días de día, 1 descanso, 7 noches», ¿el descanso es un día móvil del ciclo de 22 días o un día semanal fijo?
2. ¿Los reportes históricos deben conservar empresa, planta, sección y centro de costo tal como eran en la fecha trabajada?
3. En recontrataciones, ¿cada PeriodoLaboral puede tener empresa y organización distintas, y cuál debe gobernar el cálculo?
4. ¿Una jornada debe usar solo primera/última marca o debe parear todas las entradas y salidas?
5. ¿Se exige conservar evidencia exacta de las MarcacionCruda que originaron cada TareoDiario?

Estas preguntas no bloquean la descripción de la arquitectura actual; sí bloquearían un rediseño correcto.

## 14. Conclusiones y cierre de fase

La Fase 2 eleva la comprensión estimada del 35 % al 62 %. El incremento proviene de cerrar el modelo de personal, organización, marcadores, turnos, versiones, cuadrillas, rotaciones, jornada, ausencias, tareo, reintegros, reportes, seguridad y sus dependencias efectivas. No se asigna un porcentaje mayor porque aún no se han contrastado las reglas contra datos reales, migraciones y pruebas completas, ni se han auditado rendimiento, seguridad acción por acción o comportamiento de concurrencia.

El dominio central puede resumirse así: los hechos originales son las marcaciones; su atribución depende de una asignación temporal; la expectativa laboral depende de otra cadena temporal; la jornada se calcula, no se almacena como entidad; y TareoDiario es una proyección persistida que alimenta reportes junto con datos organizativos y ajustes.

Siguiente fase recomendada, sin ejecutarla en este documento: Fase 3 — auditoría de persistencia, integridad de datos y correspondencia entre esquema/migraciones, restricciones, consultas y escenarios temporales. Debe comenzar verificando los riesgos de historia laboral y organizativa identificados aquí, no repitiendo el inventario de Fase 1 ni el mapa de dominio de esta fase.

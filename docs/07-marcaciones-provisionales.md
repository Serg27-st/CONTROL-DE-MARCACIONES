# Marcaciones provisionales — PMV

Documento de referencia del modulo provisional de marcaciones: descripcion funcional,
estructura tecnica, manual de uso, checklists de prueba y entrega, y plan de retiro.

> **Este modulo es temporal.** Se retira cuando el motor principal de asistencia cubra
> el flujo completo de carga, consulta y exportacion de marcaciones.

---

## 1. Descripcion general

### Para que sirve

Permite cargar archivos del marcador ZKTeco G3 (`.txt` o `.dat`), cruzar las
marcaciones con el padron de trabajadores y exportar un Excel con la jornada
provisional — sin esperar a que el periodo se abra, se calcule el tareo ni se
cierren justificaciones.

### Por que es provisional

El motor principal (`CalculadoraTareo`) calcula tardanzas, horas extras,
nocturnidad, sobretasas, aplica justificaciones y genera el tareo auditable que
va a planilla. Este modulo no hace nada de eso: solo muestra las marcaciones
crudas cruzadas con el padron. Existe porque GCA necesita verificar que los
archivos se lean correctamente y que las asignaciones de marcador esten
completas **antes** de que el periodo este listo para calcular.

### Que problema resuelve

- Ver rapidamente si un archivo se leyo bien (cuantas marcaciones, cuantas
  rechazadas).
- Identificar IDs de marcador que no tienen trabajador asignado.
- Obtener un Excel con la jornada provisional para revision interna.

### Que cosas NO resuelve

- No calcula horas extras, tardanzas, faltas ni tareo.
- No aplica justificaciones ni regularizaciones.
- No modifica trabajadores, asignaciones ni catalogos.
- No reemplaza al motor principal de asistencia.
- No genera datos para planilla.

### Diferencia con el motor principal

| Concepto | Motor principal | Modulo provisional |
|---|---|---|
| Calculo de jornada | Completo (tardanza, sobretiempo, nocturnidad) | Solo primera/ultima marca |
| Justificaciones | Aplica y audita | No existe |
| Periodo | Requiere apertura y cierre | No usa periodos |
| Resultado | `TareoDiario` persistido | DTOs en memoria, sin persistencia |
| Exportacion | Tareo final con 4 hojas | Excel de 2 hojas (Marcaciones + Trazabilidad) |
| Tablas | `marcacion_cruda`, `tareo_diario` | `prov_marcacion`, `prov_importacion` |

---

## 2. Flujo funcional

### Paso a paso

1. **Entrar al modulo.** Menu superior: *Marcaciones Prov.* (visible para
   COORDINADOR y ADMINISTRADOR).

2. **Seleccionar equipo marcador.** El desplegable muestra los equipos activos
   con su codigo, nombre y planta. Es obligatorio tanto para cargar como para
   consultar.

3. **Cargar archivo `.txt` o `.dat`.** Arrastrar o seleccionar el archivo en la
   zona de carga. El sistema:
   - Calcula el hash SHA-256 del contenido.
   - Rechaza si el hash ya existe (archivo repetido).
   - Parsea con el mismo lector que usa el motor principal (`IFuenteDeMarcaciones`).
   - Deduplica por (equipo, idMarcador, fechaHora) contra lo ya cargado.
   - Guarda las marcaciones nuevas en `prov_marcacion`.
   - Muestra el resultado: insertadas, rechazadas, rango de fechas.

4. **Revisar historial de cargas.** Debajo de la zona de carga aparece una tabla
   con: fecha de carga, nombre del archivo, equipo, marcaciones insertadas,
   rechazadas y quien cargo.

5. **Consultar por rango de fechas.** Seleccionar equipo, fecha de inicio y fecha
   de fin. El sistema cruza las marcaciones del rango con:
   - `AsignacionMarcador` (quien tenia ese numero en esa fecha).
   - `AsignacionTurno` (que turno o cuadrilla tenia).
   - `Trabajador` (documento, apellidos, nombres, planta, area, centro de costo).

6. **Revisar resultados.** La tabla muestra: fecha, ID marcador, DNI, apellidos,
   nombres, ingreso, salida, turno, planta y observacion. Las filas sin
   trabajador asignado se resaltan en amarillo.

7. **Exportar Excel.** El boton *Exportar Excel* genera un archivo `.xlsx` con
   13 columnas (Fecha, ID marcador, DNI, Apellido paterno, Apellido materno,
   Nombres, Hora ingreso, Hora salida, Turno, Planta, Area, Centro de costo,
   Observacion) ordenado por fecha, planta, area y apellidos.

8. **Revisar hoja de trazabilidad.** La segunda hoja del Excel incluye: fecha de
   generacion, equipo, rango consultado, total de jornadas, quien lo genero y
   advertencias si las hay.

---

## 3. Reglas funcionales

### Carga

- El archivo se procesa **completo**. El rango de fechas no afecta la carga,
  solo la consulta y la exportacion.
- Un archivo con el mismo contenido (mismo hash SHA-256) se rechaza como
  duplicado, indicando cuando y en que equipo se cargo.
- Las marcaciones que ya existen en la base (misma combinacion equipo +
  idMarcador + fechaHora) no se reinsertan; se cuentan como rechazadas.
- Las lineas que el lector no puede interpretar se guardan como errores con su
  numero de linea y motivo.

### Consulta

- **1 a 30 dias**: funcionamiento normal.
- **31 a 40 dias**: se muestra una advertencia visual. La consulta y la
  exportacion siguen disponibles.
- **Mas de 40 dias**: se muestra la advertencia y se **bloquea la exportacion**.
  La consulta sigue disponible.
- Las fechas de inicio y fin son obligatorias; si faltan, el sistema redirige
  con un mensaje de error.

### Resultados

- Una sola marcacion en el dia genera la observacion *"Solo una marcacion
  registrada"*.
- Mas de dos marcaciones generan la observacion *"N marcaciones registradas"*.
- Marcaciones duplicadas al segundo generan *"Marcaciones duplicadas detectadas"*.
- IDs sin asignacion vigente en la fecha muestran *"Sin asignar"* en DNI y
  apellidos, con la observacion *"ID de marcador no asignado a ningun trabajador
  en esta fecha"*.

### Limites

- No se modifican trabajadores, asignaciones de marcador ni turnos.
- No se calculan horas extras, tardanzas, faltas ni tareo definitivo.
- No se generan justificaciones, regularizaciones ni incidencias.

---

## 4. Estructura tecnica

### Entidades (Domain)

| Archivo | Entidad | Descripcion |
|---|---|---|
| `Domain/MarcacionesProvisionales/ImportacionProvisional.cs` | `ImportacionProvisional` | Registro de una carga: equipo, hash, usuario, conteos |
| `Domain/MarcacionesProvisionales/MarcacionProvisional.cs` | `MarcacionProvisional` | Marcacion individual: equipo, idMarcador, fechaHora |
| `Domain/MarcacionesProvisionales/ErrorImportacionProvisional.cs` | `ErrorImportacionProvisional` | Linea rechazada: numero, contenido, motivo |

### Tablas

| Tabla | Entidad | Indices |
|---|---|---|
| `prov_importacion` | `ImportacionProvisional` | hash (no unico), (equipo, fecha) |
| `prov_marcacion` | `MarcacionProvisional` | **(equipo, idMarcador, fechaHora) unico**, fechaHora, importacionId |
| `prov_error_importacion` | `ErrorImportacionProvisional` | importacionId |

Las tres heredan de `EntidadBase` (Id, CreadoEn, CreadoPor, ModificadoEn,
ModificadoPor, Activo).

### Configuracion Fluent API

`Infrastructure/Persistencia/Configuraciones/ConfiguracionesMarcacionesProvisionales.cs`
— tres clases `IEntityTypeConfiguration<T>`, una por entidad. FK a
`EquipoMarcador` con `DeleteBehavior.Restrict`.

### Servicios

| Archivo | Interfaz | Responsabilidad |
|---|---|---|
| `Infrastructure/MarcacionesProvisionales/ServicioImportacionProvisional.cs` | `IServicioImportacionProvisional` | Carga de archivo: hash, deduplicacion, transaccion |
| `Infrastructure/MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs` | `IServicioConsultaMarcacionProvisional` | Consulta con cruce de padron: marcador, turno, planta, area |
| `Infrastructure/MarcacionesProvisionales/ExportadorMarcacionesProvisionales.cs` | `IExportadorMarcacionesProvisionales` | Excel con ClosedXML: 13 columnas + Trazabilidad |

### DTOs (Application)

| Archivo | Records |
|---|---|
| `Application/MarcacionesProvisionales/Dto/ImportacionProvisionalDtos.cs` | `ResultadoCargaProvisional`, `ErrorCargaProvisional`, `ImportacionProvisionalListaDto` |
| `Application/MarcacionesProvisionales/Dto/ConsultaProvisionalDtos.cs` | `JornadaProvisionalDto`, `ResultadoConsultaProvisional` |

### Interfaces (Application)

| Archivo | Interfaz |
|---|---|
| `Application/MarcacionesProvisionales/IServicioImportacionProvisional.cs` | `IServicioImportacionProvisional` |
| `Application/MarcacionesProvisionales/IServicioConsultaMarcacionProvisional.cs` | `IServicioConsultaMarcacionProvisional` |
| `Application/MarcacionesProvisionales/IExportadorMarcacionesProvisionales.cs` | `IExportadorMarcacionesProvisionales` |

### Controlador

`Web/Controllers/MarcacionesProvisionalesController.cs` — 4 acciones:

| Accion | Metodo HTTP | Funcion |
|---|---|---|
| `Index` | GET | Carga la vista con equipos e historial |
| `Importar` | POST | Recibe archivo y equipo, carga marcaciones |
| `Consulta` | GET | Consulta por equipo y rango de fechas |
| `Exportar` | GET | Genera y descarga el Excel |

Autorizado para `COORDINADOR` y `ADMINISTRADOR`. Valida antiforgery en POST.
Limite de archivo: 20 MB.

### Vista

`Web/Views/MarcacionesProvisionales/Index.cshtml` — vista unica con cuatro
secciones: formulario de carga, formulario de consulta, tabla de resultados e
historial de cargas.

### Menu

`Web/Views/Shared/_Layout.cshtml` — enlace *Marcaciones Prov.* dentro del
bloque `@if (gestionaTareo)`.

### Inyeccion de dependencias

`Infrastructure/InyeccionDependencias.cs` — tres registros bajo el comentario
*"Modulo provisional"*:

```
IServicioImportacionProvisional -> ServicioImportacionProvisional
IServicioConsultaMarcacionProvisional -> ServicioConsultaMarcacionProvisional
IExportadorMarcacionesProvisionales -> ExportadorMarcacionesProvisionales
```

### Tests

| Archivo | Tests | Tipo |
|---|---|---|
| `Tests/MarcacionesProvisionales/ConsultaMarcacionProvisionalTests.cs` | 13 | SQLite en memoria |
| `Tests/MarcacionesProvisionales/ExportadorMarcacionesProvisionalesTests.cs` | 10 | Unitario (ClosedXML) |
| `Tests/MarcacionesProvisionales/ImportacionProvisionalTests.cs` | 8 | SQLite en memoria + NSubstitute |

Total: **31 tests**.

### Migraciones

| Migracion | Contenido |
|---|---|
| `MarcacionesProvisionales` | Crea las 3 tablas y 6 indices |
| `NombreTablasProvisionalesSnakeCase` | Renombra tablas de PascalCase a snake_case |

---

## 5. Decisiones tecnicas importantes

### Por que no hay FK directa a Trabajador

La `MarcacionProvisional` no guarda `TrabajadorId` porque el dueno de un
numero de marcador cambia con el tiempo. Si se guardara al cargar, un numero
reasignado despues de la carga quedaria atribuido a la persona equivocada para
siempre. Al cruzar al consultar — por `AsignacionMarcador` vigente en la fecha
de la marcacion — el resultado siempre refleja la asignacion correcta, igual
que hace el motor principal (ADR 0001).

### Por que se cruza por AsignacionMarcador

Es la unica fuente de verdad sobre quien tenia cada numero en cada fecha. Un
numero puede pasar de una persona a otra (por cese, por correccion), y la
asignacion registra las dos con sus rangos de vigencia.

### Por que se procesa todo el archivo antes de filtrar

El archivo del ZKTeco trae toda la memoria del equipo: semanas o meses de
marcaciones. Filtrar al cargar descartaria datos que despues se necesitarian
para otro rango. Cargar todo y filtrar al consultar permite reutilizar la misma
carga para cualquier rango sin volver a subir el archivo.

### Por que se bloquea exportacion mayor a 40 dias

Con 200 trabajadores y 40 dias, el Excel lleva ~8 000 filas. Mas alla de eso
el archivo crece sin aportar valor (este modulo no totaliza ni calcula), y la
generacion en memoria consume recursos en un equipo de planta que no es un
servidor.

### Por que se elimino la columna "Unidad"

CLAUDE.md confirma que *"Unidad es sinonimo de planta"*. La columna duplicaba
exactamente el contenido de "Planta" — 14 columnas se redujeron a 13 sin perder
informacion.

### Por que el modulo no toca el motor principal

El motor principal es el modulo mas delicado del sistema: calcula la planilla
que se paga. Acoplar un modulo provisional a el introduciria riesgo de regresion
en un flujo que ya tiene 50+ tests y reglas complejas (nocturnidad, sobretasas,
justificaciones, cuadrillas). Mantenerlo aislado permite retirarlo limpiamente
sin tocar nada del calculo.

---

## 6. Manual de usuario administrador

### Requisitos antes de cargar

1. El equipo marcador debe estar dado de alta en *Catalogos > Equipos* con su
   planta asignada.
2. Los trabajadores deben tener su numero de marcador asignado en la ficha de
   *Personal* (si no, apareceran como "Sin asignar").
3. El archivo debe ser `.txt` o `.dat` extraido del ZKTeco G3.

### Como elegir el equipo

En el desplegable *Equipo marcador*, seleccionar el aparato del que proviene el
archivo. Cada opcion muestra: codigo, nombre y planta. Si la planta tiene dos
aparatos (A-L y M-Z), elegir el que corresponde al archivo.

### Como cargar el archivo

1. Seleccionar el equipo.
2. Hacer clic en *Examinar* o arrastrar el archivo sobre el campo.
3. Hacer clic en *Cargar*.
4. Esperar el mensaje de confirmacion.

### Como saber si la carga fue exitosa

El sistema muestra un aviso verde con el conteo: *"Carga provisional
completada: N marcaciones insertadas"*. Si hubo lineas rechazadas, las indica
a continuacion. Si el archivo ya fue cargado, muestra un aviso rojo con la
fecha y equipo de la carga anterior.

### Como interpretar observaciones

| Observacion | Significado | Accion |
|---|---|---|
| *Solo una marcacion registrada* | El trabajador marco una sola vez ese dia | Verificar si falto la marca de salida o entrada |
| *N marcaciones registradas* | Hubo mas de 2 marcaciones | Normal si marca al salir a almuerzo; revisar si son rebotes |
| *Marcaciones duplicadas detectadas* | Dos marcas al mismo segundo | Posible doble fichada; no afecta el resultado |
| *ID de marcador no asignado...* | Nadie tenia ese numero ese dia | Asignar el numero en la ficha de Personal |

### Como exportar

1. Consultar el rango deseado.
2. Si aparece el boton *Exportar Excel*, hacer clic.
3. El archivo se descarga con el nombre
   `Marcaciones_{codigoEquipo}_{fechaInicio}_{fechaFin}.xlsx`.
4. La primera hoja tiene las marcaciones; la segunda, la trazabilidad.

### Que hacer si aparece "Sin asignar"

Ir a *Personal*, buscar al trabajador y asignarle el numero de marcador con el
equipo correcto. Luego volver a consultar el rango — no hace falta recargar el
archivo, el cruce se rehace en cada consulta.

### Que hacer si el archivo ya fue cargado

El sistema rechaza archivos con contenido identico (mismo hash). Si se necesita
recargar, verificar que no sea el mismo archivo renombrado. Si el archivo es
diferente (por ejemplo, una descarga posterior con marcaciones nuevas), se
cargara sin problema porque el hash sera distinto.

### Que hacer si el rango supera 40 dias

Reducir el rango a 40 dias o menos. Si se necesitan datos de un periodo mas
largo, hacer dos exportaciones con rangos consecutivos.

---

## 7. Checklist de pruebas manuales

### Carga

- [ ] Cargar archivo `.txt` valido — debe mostrar conteo de insertadas.
- [ ] Cargar archivo `.dat` (attlog) valido — mismo resultado.
- [ ] Cargar archivo `.dat` (respaldo binario) — mismo resultado.
- [ ] Cargar el mismo archivo dos veces — debe rechazar con mensaje de duplicado.
- [ ] Cargar archivo vacio — debe rechazar (0 marcaciones).
- [ ] Cargar sin seleccionar equipo — debe mostrar error.
- [ ] Cargar con equipo inactivo — debe rechazar.
- [ ] Verificar que el historial muestra la carga recien hecha.

### Consulta

- [ ] Consultar rango de 1 dia con marcaciones — debe mostrar jornadas.
- [ ] Consultar rango sin marcaciones — debe mostrar "No se encontraron...".
- [ ] Consultar rango de 31 dias — debe mostrar advertencia amarilla.
- [ ] Consultar rango de 41 dias — debe mostrar advertencia y bloquear exportacion.
- [ ] Consultar sin fechas — debe redirigir con error.
- [ ] Consultar sin equipo — debe redirigir con error.
- [ ] Verificar que trabajador asignado muestra DNI, apellidos, turno y planta.
- [ ] Verificar que trabajador sin asignar muestra fila amarilla con "Sin asignar".

### Exportacion

- [ ] Exportar rango de 15 dias — debe descargar `.xlsx`.
- [ ] Verificar 13 columnas en hoja Marcaciones.
- [ ] Verificar hoja Trazabilidad con equipo, rango, usuario y fecha.
- [ ] Verificar orden: fecha, planta, area, apellidos.
- [ ] Verificar que "Sin asignar" aparece en celdas de trabajadores no asignados.
- [ ] Verificar que un nombre con `=` o `+` al inicio no se interpreta como formula.

### Seguridad

- [ ] Acceder como COORDINADOR — debe ver el modulo.
- [ ] Acceder como ADMINISTRADOR — debe ver el modulo.
- [ ] Acceder como GERENCIA — no debe ver el enlace ni poder acceder por URL.
- [ ] Acceder como TRABAJADOR_SOCIAL — no debe ver el enlace ni poder acceder por URL.
- [ ] Acceder sin sesion — debe redirigir a login.

---

## 8. Checklist de entrega

### Compilacion y tests

- [x] `dotnet build` sin errores ni advertencias.
- [x] `dotnet test` con 673 tests en verde (289 Domain + 384 Infrastructure).
- [x] 31 tests del modulo provisional en verde.

### Migraciones

- [x] `MarcacionesProvisionales` — crea las 3 tablas.
- [x] `NombreTablasProvisionalesSnakeCase` — renombra a snake_case.

### Funcionalidad

- [x] Menu visible para COORDINADOR y ADMINISTRADOR.
- [x] Carga de archivo con deduplicacion por hash.
- [x] Historial de cargas.
- [x] Consulta con cruce de padron.
- [x] Exportacion Excel con 13 columnas + Trazabilidad.
- [x] Sanitizacion de formulas en el Excel.
- [x] Validacion de equipo, fechas y archivo.

### Aislamiento

- [x] No modifica `Trabajador`, `AsignacionMarcador` ni ningun dato maestro.
- [x] No escribe en `marcacion_cruda`, `tareo_diario` ni ninguna tabla del motor.
- [x] No referencia `CalculadoraTareo`, `IServicioCalculoTareo` ni nada de Asistencia.
- [x] Las tablas provisionales son identificables por el prefijo `prov_`.
- [x] Los servicios estan bajo el comentario "Modulo provisional" en DI.

### Documentacion

- [x] Este archivo (`docs/07-marcaciones-provisionales.md`).

---

## 9. Plan de retiro futuro

Cuando el motor principal cubra el flujo completo de carga, consulta y
exportacion de marcaciones, este modulo se retira siguiendo estos pasos:

### 1. Eliminar servicios e interfaces

| Capa | Archivo a eliminar |
|---|---|
| Application | `MarcacionesProvisionales/IServicioImportacionProvisional.cs` |
| Application | `MarcacionesProvisionales/IServicioConsultaMarcacionProvisional.cs` |
| Application | `MarcacionesProvisionales/IExportadorMarcacionesProvisionales.cs` |
| Application | `MarcacionesProvisionales/Dto/ImportacionProvisionalDtos.cs` |
| Application | `MarcacionesProvisionales/Dto/ConsultaProvisionalDtos.cs` |
| Infrastructure | `MarcacionesProvisionales/ServicioImportacionProvisional.cs` |
| Infrastructure | `MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs` |
| Infrastructure | `MarcacionesProvisionales/ExportadorMarcacionesProvisionales.cs` |

### 2. Eliminar entidades

| Capa | Archivo a eliminar |
|---|---|
| Domain | `MarcacionesProvisionales/ImportacionProvisional.cs` |
| Domain | `MarcacionesProvisionales/MarcacionProvisional.cs` |
| Domain | `MarcacionesProvisionales/ErrorImportacionProvisional.cs` |

### 3. Eliminar configuracion Fluent API

`Infrastructure/Persistencia/Configuraciones/ConfiguracionesMarcacionesProvisionales.cs`

### 4. Eliminar controlador y vista

| Capa | Archivo a eliminar |
|---|---|
| Web | `Controllers/MarcacionesProvisionalesController.cs` |
| Web | `Views/MarcacionesProvisionales/Index.cshtml` |
| Web | `Models/MarcacionesProvisionalesVm.cs` |

### 5. Retirar enlace del menu

En `Web/Views/Shared/_Layout.cshtml`, eliminar la linea:

```html
<a class="@Activo("MarcacionesProvisionales")" ...>Marcaciones Prov.</a>
```

### 6. Retirar registros de DI

En `Infrastructure/InyeccionDependencias.cs`, eliminar las tres lineas bajo el
comentario *"Modulo provisional"*.

### 7. Retirar DbSets

En `MarcacionDbContext`, eliminar los `DbSet` de `ImportacionProvisional`,
`MarcacionProvisional` y `ErrorImportacionProvisional` (si estan declarados
explicitamente).

### 8. Crear migracion de retiro

```bash
dotnet ef migrations add RetiroModuloProvisional \
    --project src/ControlMarcacion.Infrastructure \
    --startup-project src/ControlMarcacion.Web
```

La migracion debe contener:

```csharp
migrationBuilder.DropTable(name: "prov_error_importacion");
migrationBuilder.DropTable(name: "prov_marcacion");
migrationBuilder.DropTable(name: "prov_importacion");
```

En ese orden: primero las tablas hijas (por las FK), luego la padre.

### 9. Eliminar tests

| Archivo a eliminar |
|---|
| `Tests/MarcacionesProvisionales/ConsultaMarcacionProvisionalTests.cs` |
| `Tests/MarcacionesProvisionales/ExportadorMarcacionesProvisionalesTests.cs` |
| `Tests/MarcacionesProvisionales/ImportacionProvisionalTests.cs` |

### 10. Eliminar documentacion

Este archivo: `docs/07-marcaciones-provisionales.md`.

### Verificacion post-retiro

- `dotnet build` sin errores.
- `dotnet test` en verde.
- La aplicacion arranca sin errores.
- El menu no muestra *Marcaciones Prov.*
- La URL `/MarcacionesProvisionales` devuelve 404.
- Las tablas `prov_*` no existen en la base de datos.

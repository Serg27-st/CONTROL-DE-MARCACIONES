# Prueba final del PMV — Marcaciones provisionales

Guia paso a paso para validar el modulo provisional de marcaciones antes de
presentarlo como PMV. Cada seccion es un checklist ejecutable.

> **Regla:** esta guia no modifica codigo, no agrega funcionalidades, no cambia
> logica y no crea migraciones. Solo verifica lo que ya existe.

---

## 1. Preparacion

Antes de empezar, confirmar que el entorno esta listo.

### Base de datos

- [ ] La aplicacion arranca sin errores (`dotnet run --project src/ControlMarcacion.Web`).
- [ ] Las tablas `prov_importacion`, `prov_marcacion` y `prov_error_importacion`
      existen en la base de datos.
- [ ] Verificacion rapida: abrir la base con DB Browser u otra herramienta y
      confirmar las tres tablas con prefijo `prov_`.

### Tests

- [ ] Ejecutar `dotnet test` y confirmar que **todos** pasan (673 esperados).
- [ ] Anotar el conteo exacto de tests y el tiempo de ejecucion.
- [ ] **Captura de pantalla** del resultado en consola.

### Usuario

- [ ] Existe un usuario con rol `COORDINADOR`.
- [ ] Existe un usuario con rol `ADMINISTRADOR`.
- [ ] Existe un usuario con rol `GERENCIA` (para la prueba de acceso denegado).
- [ ] Si no existen, crearlos desde `/Usuarios` con el coordinador.

### Equipo marcador

- [ ] Existe al menos un equipo activo en `/Equipos`.
- [ ] Anotar: **codigo**, **nombre** y **planta** del equipo que se usara.
- [ ] Si la planta tiene dos aparatos, anotar ambos.

### Trabajadores

- [ ] Existen al menos 5 trabajadores activos en la planta del equipo elegido.
- [ ] Cada uno tiene asignacion de marcador **vigente** para ese equipo.
- [ ] Al menos 1 trabajador **no** tiene numero asignado (para probar "Sin asignar").
- [ ] Al menos 1 trabajador tiene turno asignado.
- [ ] Anotar: **documento**, **apellidos**, **ID de marcador** y **equipo** de
      3 trabajadores para verificacion cruzada.

### Archivo de prueba

- [ ] Disponer de un archivo `.dat` o `.txt` del ZKTeco con marcaciones reales
      o semirreales.
- [ ] El archivo debe contener marcaciones de al menos 2 dias distintos.
- [ ] El archivo debe contener al menos 1 ID de marcador que **no** este
      asignado a ningun trabajador.
- [ ] Anotar: **nombre del archivo**, **rango de fechas** que contiene y
      **cantidad aproximada de lineas**.
- [ ] Si se usa un archivo real, confirmar que no contiene datos personales
      fuera de los IDs de marcador y las fechas (Ley 29733).

---

## 2. Prueba de carga

### 2.1 Acceso al modulo

| # | Paso | Esperado | OK |
|---|---|---|---|
| 1 | Iniciar sesion como COORDINADOR | Login exitoso | [ ] |
| 2 | Buscar en el menu *Marcaciones Prov.* | El enlace es visible | [ ] |
| 3 | Hacer clic en *Marcaciones Prov.* | Se abre `/MarcacionesProvisionales` | [ ] |
| 4 | Verificar que aparece el formulario de carga | Zona de archivo + desplegable de equipo | [ ] |

### 2.2 Carga del archivo

| # | Paso | Esperado | OK |
|---|---|---|---|
| 5 | Seleccionar el equipo en el desplegable | El equipo aparece con codigo, nombre y planta | [ ] |
| 6 | Seleccionar o arrastrar el archivo `.dat`/`.txt` | El nombre del archivo aparece en el campo | [ ] |
| 7 | Hacer clic en *Cargar* | Procesamiento sin errores | [ ] |
| 8 | Leer el mensaje de resultado | Aviso verde: "N marcaciones insertadas" | [ ] |
| 9 | Anotar: marcaciones insertadas | ______ | [ ] |
| 10 | Anotar: marcaciones rechazadas | ______ | [ ] |
| 11 | Anotar: rango de fechas reportado | ______ a ______ | [ ] |

### 2.3 Historial de cargas

| # | Paso | Esperado | OK |
|---|---|---|---|
| 12 | Buscar la seccion de historial debajo del formulario | Tabla visible | [ ] |
| 13 | Verificar que la carga recien hecha aparece | Fila con fecha, archivo, equipo, insertadas, rechazadas, usuario | [ ] |
| 14 | Verificar que el nombre del archivo coincide | Coincide con el archivo subido | [ ] |
| 15 | Verificar que el usuario coincide | Coincide con el usuario actual | [ ] |

### 2.4 Archivo duplicado

| # | Paso | Esperado | OK |
|---|---|---|---|
| 16 | Seleccionar el **mismo** equipo | Equipo seleccionado | [ ] |
| 17 | Seleccionar el **mismo** archivo | Archivo seleccionado | [ ] |
| 18 | Hacer clic en *Cargar* | Aviso rojo: archivo ya cargado | [ ] |
| 19 | Verificar que el mensaje indica cuando y en que equipo se cargo | Fecha y equipo de la carga anterior | [ ] |
| 20 | Verificar que no se insertaron marcaciones adicionales | El historial no muestra una segunda carga | [ ] |

**Captura:** pantalla de carga exitosa (paso 8) y rechazo por duplicado (paso 18).

---

## 3. Prueba de consulta

### 3.1 Rango de 1 dia

| # | Paso | Esperado | OK |
|---|---|---|---|
| 21 | Seleccionar equipo | Equipo elegido | [ ] |
| 22 | Poner fecha inicio = fecha fin = un dia con marcaciones | Rango de 1 dia | [ ] |
| 23 | Hacer clic en *Consultar* | Tabla de resultados con jornadas de ese dia | [ ] |
| 24 | Verificar que aparecen trabajadores con marcaciones | Al menos 1 fila con datos | [ ] |

### 3.2 Rango de varios dias

| # | Paso | Esperado | OK |
|---|---|---|---|
| 25 | Cambiar fecha fin a 7 dias despues de la fecha inicio | Rango de 7 dias | [ ] |
| 26 | Hacer clic en *Consultar* | Resultados de varios dias | [ ] |
| 27 | Verificar que los dias sin marcaciones no generan filas | Solo dias con datos | [ ] |

### 3.3 Rango mayor a 30 dias

| # | Paso | Esperado | OK |
|---|---|---|---|
| 28 | Poner un rango de 35 dias | Rango > 30 | [ ] |
| 29 | Hacer clic en *Consultar* | Resultados + advertencia amarilla | [ ] |
| 30 | Verificar texto de la advertencia | Indica que el rango supera 30 dias | [ ] |
| 31 | Verificar que el boton *Exportar Excel* **sigue disponible** | Boton visible y habilitado | [ ] |

### 3.4 Rango mayor a 40 dias

| # | Paso | Esperado | OK |
|---|---|---|---|
| 32 | Poner un rango de 45 dias | Rango > 40 | [ ] |
| 33 | Hacer clic en *Consultar* | Resultados + advertencia | [ ] |
| 34 | Verificar que el boton *Exportar Excel* esta **bloqueado** | Boton deshabilitado o ausente | [ ] |

### 3.5 Rango sin marcaciones

| # | Paso | Esperado | OK |
|---|---|---|---|
| 35 | Poner un rango de fechas donde no haya marcaciones cargadas | Rango vacio | [ ] |
| 36 | Hacer clic en *Consultar* | Mensaje: "No se encontraron marcaciones" o tabla vacia | [ ] |

### 3.6 Sin fechas o sin equipo

| # | Paso | Esperado | OK |
|---|---|---|---|
| 37 | Dejar fechas vacias y consultar | Redireccion con mensaje de error | [ ] |
| 38 | Dejar equipo sin seleccionar y consultar | Redireccion con mensaje de error | [ ] |

**Captura:** consulta con resultados (paso 23), advertencia >30 dias (paso 30),
bloqueo >40 dias (paso 34).

---

## 4. Prueba de resultados

Sobre la consulta del paso 23 o 26, verificar manualmente cada campo.

### 4.1 Cruce con padron

Elegir 3 trabajadores de los que se anotaron en la preparacion y verificar fila
por fila:

| Campo | Trabajador 1 | Trabajador 2 | Trabajador 3 |
|---|---|---|---|
| DNI correcto | [ ] | [ ] | [ ] |
| Apellido paterno correcto | [ ] | [ ] | [ ] |
| Apellido materno correcto | [ ] | [ ] | [ ] |
| Nombres correctos | [ ] | [ ] | [ ] |
| ID de marcador correcto | [ ] | [ ] | [ ] |
| Planta correcta | [ ] | [ ] | [ ] |
| Turno correcto (si tiene) | [ ] | [ ] | [ ] |

### 4.2 Horas

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 39 | La hora de ingreso es la **primera** marca del dia | Coincide con la marca mas temprana | [ ] |
| 40 | La hora de salida es la **ultima** marca del dia | Coincide con la marca mas tardia | [ ] |
| 41 | Buscar un dia con una sola marca | Hora de ingreso presente, salida vacia | [ ] |
| 42 | Verificar observacion de marca unica | "Solo una marcacion registrada" | [ ] |

### 4.3 IDs sin asignacion

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 43 | Buscar una fila con ID no asignado a ningun trabajador | Fila resaltada en amarillo | [ ] |
| 44 | Verificar que DNI dice "Sin asignar" | Texto visible | [ ] |
| 45 | Verificar que apellidos dicen "Sin asignar" | Texto visible | [ ] |
| 46 | Verificar observacion | "ID de marcador no asignado a ningun trabajador en esta fecha" | [ ] |

### 4.4 Campos opcionales

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 47 | Area aparece si el trabajador la tiene | Nombre del area o vacio | [ ] |
| 48 | Centro de costo aparece si el trabajador lo tiene | Codigo del CECO o vacio | [ ] |
| 49 | Turno aparece si el trabajador lo tiene | Nombre del turno o vacio | [ ] |

**Captura:** consulta con "Sin asignar" visible (paso 43).

---

## 5. Prueba de exportacion

### 5.1 Generar Excel

| # | Paso | Esperado | OK |
|---|---|---|---|
| 50 | Consultar un rango de 1 dia con resultados | Resultados visibles | [ ] |
| 51 | Hacer clic en *Exportar Excel* | Descarga un archivo `.xlsx` | [ ] |
| 52 | Verificar nombre del archivo | `Marcaciones_{codigo}_{inicio}_{fin}.xlsx` | [ ] |
| 53 | Consultar un rango de 15 dias y exportar | Segundo archivo `.xlsx` | [ ] |
| 54 | Consultar un rango de 35 dias y exportar | Tercer archivo `.xlsx` (con advertencia, pero permitido) | [ ] |
| 55 | Consultar un rango de 45 dias | Boton *Exportar* bloqueado | [ ] |

### 5.2 Verificar hoja Marcaciones

Abrir el Excel del paso 51 o 53.

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 56 | Contar columnas del encabezado (fila 1) | Exactamente **13** | [ ] |
| 57 | Columna A = "Fecha" | Si | [ ] |
| 58 | Columna B = "ID marcador" | Si | [ ] |
| 59 | Columna C = "DNI" | Si | [ ] |
| 60 | Columna D = "Apellido paterno" | Si | [ ] |
| 61 | Columna E = "Apellido materno" | Si | [ ] |
| 62 | Columna F = "Nombres" | Si | [ ] |
| 63 | Columna G = "Hora ingreso" | Si | [ ] |
| 64 | Columna H = "Hora salida" | Si | [ ] |
| 65 | Columna I = "Turno" | Si | [ ] |
| 66 | Columna J = "Planta" | Si | [ ] |
| 67 | Columna K = "Area" | Si | [ ] |
| 68 | Columna L = "Centro de costo" | Si | [ ] |
| 69 | Columna M = "Observacion" | Si | [ ] |
| 70 | No existe columna N con datos | Columna N vacia | [ ] |
| 71 | No existe columna "Unidad" | No aparece en ningun encabezado | [ ] |

### 5.3 Verificar formato

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 72 | Formato de fecha | `dd/MM/yyyy` (ej. 15/07/2026) | [ ] |
| 73 | Formato de hora ingreso | `HH:mm` (ej. 07:00) | [ ] |
| 74 | Formato de hora salida | `HH:mm` (ej. 19:00) | [ ] |
| 75 | "Sin asignar" en DNI donde corresponde | Texto, no numero | [ ] |

### 5.4 Verificar hoja Trazabilidad

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 76 | La segunda hoja se llama "Trazabilidad" | Si | [ ] |
| 77 | Incluye el equipo usado | Codigo y nombre | [ ] |
| 78 | Incluye el rango consultado | Fechas correctas | [ ] |
| 79 | Incluye el usuario que genero | Nombre del usuario | [ ] |
| 80 | Incluye la fecha de generacion | Fecha de hoy | [ ] |
| 81 | Si el rango era >30 dias, incluye advertencia | Texto de advertencia presente | [ ] |

### 5.5 Verificar privacidad (Ley 29733)

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 82 | Buscar la palabra "telefono" en todo el Excel | No aparece como dato de trabajador | [ ] |
| 83 | Buscar la palabra "direccion" en todo el Excel | No aparece como dato de trabajador | [ ] |
| 84 | Buscar la palabra "correo" o "email" en todo el Excel | No aparece como dato de trabajador | [ ] |

### 5.6 Verificar anti-formulas

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 85 | Si algun campo empieza con `=`, `+`, `-` o `@` | La celda es texto, no se ejecuta como formula | [ ] |
| 86 | Si no hay datos con esos caracteres, crear un trabajador de prueba con apellido `=TEST` y exportar | La celda muestra el texto, no un error | [ ] |

**Captura:** Excel abierto mostrando las 13 columnas, hoja de trazabilidad.

---

## 6. Prueba de seguridad

### 6.1 Acceso con rol no autorizado

| # | Paso | Esperado | OK |
|---|---|---|---|
| 87 | Cerrar sesion del COORDINADOR | Sesion cerrada | [ ] |
| 88 | Iniciar sesion como GERENCIA | Login exitoso | [ ] |
| 89 | Buscar *Marcaciones Prov.* en el menu | **No** debe aparecer | [ ] |
| 90 | Navegar manualmente a `/MarcacionesProvisionales` | Redireccion a 403 o a login | [ ] |
| 91 | Cerrar sesion | Sesion cerrada | [ ] |

### 6.2 Acceso con rol autorizado (ADMINISTRADOR)

| # | Paso | Esperado | OK |
|---|---|---|---|
| 92 | Iniciar sesion como ADMINISTRADOR | Login exitoso | [ ] |
| 93 | Buscar *Marcaciones Prov.* en el menu | Enlace visible | [ ] |
| 94 | Hacer clic | Acceso normal al modulo | [ ] |

### 6.3 Integridad de datos maestros

| # | Verificacion | Esperado | OK |
|---|---|---|---|
| 95 | No hay boton para editar trabajadores en el modulo | No existe | [ ] |
| 96 | No hay boton para editar marcaciones en el modulo | No existe | [ ] |
| 97 | No hay boton para editar turnos en el modulo | No existe | [ ] |
| 98 | No hay boton para editar asignaciones en el modulo | No existe | [ ] |
| 99 | Verificar en la base: las tablas `trabajador`, `asignacion_marcador`, `turno` y `marcacion_cruda` no fueron modificadas despues de la carga provisional | Fechas de modificacion anteriores a la prueba | [ ] |

---

## 7. Evidencia esperada

Guardar las siguientes capturas o archivos como evidencia de la validacion:

| # | Evidencia | Paso de referencia | Guardado |
|---|---|---|---|
| E1 | Resultado de `dotnet test` en consola | Preparacion | [ ] |
| E2 | Pantalla de carga exitosa (aviso verde) | Paso 8 | [ ] |
| E3 | Rechazo por archivo duplicado (aviso rojo) | Paso 18 | [ ] |
| E4 | Historial de importacion con la carga | Paso 13 | [ ] |
| E5 | Consulta con resultados (tabla con jornadas) | Paso 23 | [ ] |
| E6 | Consulta con filas "Sin asignar" | Paso 43 | [ ] |
| E7 | Advertencia por rango >30 dias | Paso 30 | [ ] |
| E8 | Bloqueo de exportacion por rango >40 dias | Paso 34 | [ ] |
| E9 | Excel exportado (.xlsx) | Paso 51 | [ ] |
| E10 | Hoja de trazabilidad del Excel | Paso 76 | [ ] |
| E11 | Acceso denegado para GERENCIA | Paso 90 | [ ] |

---

## 8. Criterios de aceptacion

### El PMV se considera APROBADO cuando:

1. **Todos los tests pasan** — 673 tests en verde, 0 errores, 0 advertencias.
2. **La carga funciona** — el archivo se procesa, las marcaciones se insertan, el
   historial las muestra y el duplicado se rechaza.
3. **La consulta funciona** — los resultados cruzan correctamente con el padron:
   DNI, apellidos, ID de marcador, planta y turno coinciden con la ficha del
   trabajador.
4. **Los limites se respetan** — advertencia a los 31 dias, bloqueo de
   exportacion a los 41 dias.
5. **El Excel es correcto** — 13 columnas, formato de fecha `dd/MM/yyyy`,
   formato de hora `HH:mm`, hoja de trazabilidad presente, sin datos personales
   prohibidos, sin formulas ejecutables.
6. **La seguridad funciona** — COORDINADOR y ADMINISTRADOR acceden; GERENCIA y
   TRABAJADOR_SOCIAL no acceden ni por menu ni por URL directa.
7. **Los datos maestros no se modifican** — la carga provisional no altera
   ninguna tabla fuera de `prov_importacion`, `prov_marcacion` y
   `prov_error_importacion`.
8. **Todas las evidencias estan guardadas** — los 11 items de la seccion 7.

### El PMV se considera NO APROBADO cuando:

- Algun test falla.
- La carga inserta marcaciones en `marcacion_cruda` en vez de `prov_marcacion`.
- El cruce con el padron muestra un trabajador equivocado para un ID de marcador.
- El Excel tiene mas o menos de 13 columnas.
- El Excel contiene telefono, direccion o correo de trabajadores.
- Un valor con `=` o `+` se ejecuta como formula en el Excel.
- Un usuario con rol GERENCIA puede acceder al modulo.
- El boton de exportar esta habilitado con un rango mayor a 40 dias.
- La aplicacion no arranca despues de la prueba.

### Accion ante fallo

Si algun criterio falla:

1. Anotar el paso exacto que fallo y el resultado obtenido.
2. Tomar captura de pantalla del fallo.
3. **No corregir durante la prueba** — documentar y resolver despues.
4. Volver a ejecutar la guia completa despues de la correccion.

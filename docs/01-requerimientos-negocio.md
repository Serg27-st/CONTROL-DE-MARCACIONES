# Requerimientos de negocio — Sistema de Control de Marcación

> Estado: **borrador en discusión**. Las secciones marcadas con ⚠️ están pendientes de
> definición y bloquean el diseño del motor de cálculo.
> Última actualización: 2026-08-03

---

## 1. Contexto y actores

**GCA** presta servicios a su cliente **Intradevco**. El personal que opera en las plantas
está contratado a través de dos razones sociales de la corporación GCA:

| Razón social | Plantas |
|---|---|
| Consulting Advisers | Planta Detergentes, Planta 20 mil |
| CA Outsourcing | Planta 01, Planta Sulfonación |

Jerarquía organizativa a modelar:

```
Cliente (Intradevco)
└── Empresa / razón social (Consulting Advisers | CA Outsourcing)
    └── Planta (Detergentes, 20 mil, 01, Sulfonación)
        └── Área
            └── Sección
```

> **Supuesto adoptado:** aunque hoy el único cliente es Intradevco, `cliente` y `empresa`
> se modelan como dimensiones desde el inicio. El costo es marginal y evita una migración
> dolorosa si GCA suma otro cliente. Toda consulta filtra por empresa.

**Actor principal:** el *administrador* de RRHH. Registra trabajadores, carga el archivo del
marcador, edita justificaciones y cierra el periodo.

---

## 2. Tipos de trabajador

| Código | Tipo | Régimen de turnos |
|---|---|---|
| `EMP` | Empleado | Un solo turno fijo |
| `OBM` | Obrero | Uno o más turnos ⚠️ |

✅ **Resuelto:** "uno o más turnos" significa **rotación entre turnos según calendario** — una
temporada diurno, otra nocturno. No se doblan turnos el mismo día.

Consecuencia de diseño: el turno **no** es un campo de la ficha del trabajador, sino una
**asignación con vigencia por fecha** (`AsignacionTurno`). El motor resuelve el turno por la
asignación vigente en la fecha que calcula, nunca por "el último turno del trabajador" — ese
atajo produce tareos incorrectos justo en las semanas de cambio de rotación.

---

## 3. Catálogo de turnos

| Código | Turno | Días | Horario | Duración bruta | Restricción |
|---|---|---|---|---|---|
| `DIU` | Diurno | L–S | 07:00 → 19:00 | 12 h | |
| `NOC` | Nocturno | L–S | 19:00 → 07:00 | 12 h | Cruza medianoche |
| `SAB` | Especial sábado | S | 07:00 → 15:00 | 8 h | **Solo Planta 01** |
| `DOM1` | Dominical 1 | D | 07:00 → 15:00 | 8 h | |
| `DOM2` | Dominical 2 | D | 07:00 → 19:00 | 12 h | |

Observaciones de diseño:

- El turno `NOC` cruza medianoche. **La jornada se imputa al día en que inicia el turno.**
  Es la principal fuente de errores en este tipo de sistemas.
- `SAB` está restringido a una planta: la restricción va como dato del catálogo
  (turno ↔ plantas habilitadas), no como condición escrita en el código.
- Los turnos son configurables. Los horarios de arriba son el estado inicial, no constantes.

---

## 4. Marco legal peruano aplicable al cálculo

Base normativa: **D.S. 007-2002-TR** (TUO de la Ley de Jornada de Trabajo) y **D.S. 008-2002-TR**.

| Concepto | Regla legal (mínimo) |
|---|---|
| Jornada máxima | 8 h diarias **o** 48 h semanales |
| Horas extras: primeras 2 h | Sobretasa **+25 %** sobre el valor hora |
| Horas extras: desde la 3.ª h | Sobretasa **+35 %** sobre el valor hora |
| Trabajo nocturno (22:00–06:00) | Sobretasa **+35 %** sobre la RMV |
| Descanso semanal trabajado sin sustitutorio | Sobretasa **+100 %** |
| Feriado trabajado sin descanso sustitutorio | Sobretasa **+100 %** |
| Refrigerio | Mínimo 45 min; **no** forma parte de la jornada salvo pacto. ✅ **En GCA sí hay pacto: es tiempo pagado y se computa como labor** |

> ⚠️ Estos son **mínimos legales**. Si existe convenio colectivo o política interna de GCA
> que mejore estas tasas, manda. Las sobretasas deben ser **parámetros configurables en base
> de datos**, nunca constantes en el código, y con vigencia por fecha para poder recalcular
> periodos pasados con las reglas que estaban vigentes entonces.

### El punto crítico: la jornada de 12 horas

Los turnos `DIU` y `NOC` son de 12 horas de lunes a sábado. Eso da **72 h semanales**, muy por
encima del tope legal de 48 h. Solo hay dos lecturas posibles, y producen cálculos
completamente distintos:

1. **Jornada ordinaria de 8 h + horas extras diarias.** Cada día genera entre 3 y 4 h extra
   según cómo se trate el refrigerio. Es legal, pero de costo alto y sostenido.
2. **Jornada atípica o acumulativa** (art. 4 del D.S. 007-2002-TR). Permitida por rotación,
   con la condición de que el promedio de horas en el ciclo no supere las 48 h semanales.
   Esto implica que el trabajador **no** labora las seis jornadas de 12 h, sino un rol rotativo.

✅ **Resuelto: opción 1 — jornada ordinaria de 8 h más horas extras.**

Cada jornada de 12 h se descompone en 480 minutos ordinarios y el resto como horas extras.
Las dos primeras horas extra van al 25 % y las siguientes al 35 %.

✅ **El refrigerio no se descuenta** (confirmado por GCA): se computa como hora de labor en
los cinco turnos. Es el pacto en contrario que admite el D.S. 007-2002-TR. En consecuencia
una jornada de 12 h da **720 minutos efectivos: 480 ordinarios y 240 de sobretiempo** (120
al 25 % y 120 al 35 %) — no los 660 y 180 que salían mientras se descontaba una hora.

El catálogo de turnos conserva el campo `MinutosRefrigerio`, hoy en 0 en los cinco: la regla
es una política de GCA y no una ley, así que cambiarla debe seguir siendo un `UPDATE`.

> ⚠️ Queda una consecuencia que conviene validar con GCA: bajo esta lectura, un obrero que
> cubre las seis jornadas semanales de 12 h acumula del orden de 18 a 24 horas extras por
> semana de forma sostenida. Es legal, pero es un costo alto y permanente. Vale la pena
> confirmar que es el escenario real y no una jornada rotativa con días de descanso dentro
> del ciclo.

---

## 5. Origen de los datos: marcador ZKTeco G3

- Extracción **por USB**, no por red. El equipo entrega un archivo **TXT**.
- El sistema debe permitir cargar ese archivo y procesarlo.
- El vínculo entre el archivo y la persona es el **ID de marcador**, que se registra en la
  ficha del trabajador.

✅ **Formato confirmado** con el archivo real `AEH2200760069_attlog.txt` (el prefijo es el
número de serie del equipo). Muestra guardada en
`tests/ControlMarcacion.Infrastructure.Tests/Recursos/`.

| Aspecto | Valor observado |
|---|---|
| Separador | Tabulador (`0x09`) |
| Columnas | `id_usuario · fecha_hora · modo_verificación · tipo_marcación · workcode · reservado` |
| ID de usuario | Rellenado con espacios a la izquierda — hay que recortarlo |
| Fecha y hora | `yyyy-MM-dd HH:mm:ss` |
| Fin de línea | **LF** (`0x0A`) — verificado byte a byte sobre el archivo real: 2 508 saltos para 2 509 marcaciones, ningún `CR`, y sin salto final |
| Codificación | ASCII en esta muestra; sigue siendo configurable por si aparecen nombres |
| Encabezado | No tiene |

**Hallazgo que condiciona el motor de cálculo:** la columna de **tipo de marcación es
siempre `0`**. El equipo **no distingue entrada de salida**. El motor debe inferirlo
emparejando las marcaciones del día contra la ventana del turno, y no puede confiar en
que el archivo lo diga.

Lo que muestran los datos de ejemplo (2 trabajadores, 01 al 06 de julio de 2026):

- Jornadas de 07:00 a 19:06, coherentes con el turno `DIU`.
- **Sábado 04/07 trabajado** en horario de 12 h, no en el turno especial de sábado.
- **Domingo 05/07 trabajado** de 07:00 a 19:06, coherente con `DOM2`.
- **Lunes 06/07 con salidas a las 21:06 y 22:06**, dos y tres horas después del cierre del
  turno: es el caso de horas extras que debe cubrir el motor.

Reglas fijas de la carga:

- Las marcaciones crudas son **inmutables**: se insertan y nunca se editan ni se borran.
- Recargar el mismo archivo **no puede duplicar** registros.
- Una fila inválida no aborta la carga: se procesa todo y se devuelve un informe con las
  filas rechazadas y el motivo.

---

## 6. Módulos del sistema

### 6.1 Seguridad y acceso
Login con credenciales. Roles de operador. El proceso arranca con el administrador
autenticándose.

### 6.2 Mantenimiento de personal
Ficha del trabajador, vinculada al **ID de marcador**.

**Datos personales**

| Campo | Notas |
|---|---|
| Tipo de documento de identidad | Catálogo (DNI, CE, pasaporte, PTP) |
| Número de documento de identidad | Único por tipo. Texto, **nunca numérico** (conserva ceros a la izquierda) |
| Apellido paterno | |
| Apellido materno | |
| Nombres | |
| Sexo | |
| Teléfono | |
| Dirección | |
| Correo electrónico | |
| Tiene hijos | Sí / No |

**Datos laborales**

| Campo | Notas |
|---|---|
| Empresa | Consulting Advisers / CA Outsourcing |
| Unidad | ⚠️ Pendiente de definir |
| Tipo de planilla | ⚠️ ¿Equivale a OBM/EMP o es otra clasificación? |
| Planta | |
| Área | |
| Sección | |
| Fecha de ingreso | |
| Fecha de cese | Nulo si está activo |
| Puesto | |
| **ID de marcador** | Clave de cruce con el archivo del ZKTeco |
| Turno | |

> Nota de diseño: el ID de marcador puede reasignarse a otra persona con el tiempo. Debe
> guardarse con **vigencia por fecha**, o las marcaciones históricas se atribuirán a la
> persona equivocada.

### 6.3 Catálogos
Turnos, horarios, plantas, áreas, secciones y **periodos configurables**. Todo el catálogo es
mantenible desde la aplicación, sin tocar base de datos.

### 6.4 Solicitudes de ausencia
Tipos iniciales, **ampliables desde el catálogo**:

- Vacaciones
- Descanso médico
- Licencia con goce
- Licencia sin goce
- Permiso
- Licencia paternal
- Licencia por maternidad
- Subsidio por enfermedad
- Subsidio por accidente

Cada solicitud lleva **fecha de inicio** y **fecha de fin**. El tipo de ausencia es un dato
del catálogo, no un valor fijo en el código: agregar un tipo nuevo no debe requerir
recompilar.

### 6.5 Asistencias (tareo diario)
Precarga la información del marcador y la cruza con las solicitudes de ausencia registradas.

### 6.6 Justificaciones
Capa intermedia que se **alimenta del módulo de solicitudes** y que el administrador puede
**editar** para justificar ausencias o tardanzas.

**Restricción dura:** solo se puede editar si el **periodo está aperturado**. Cerrado el
periodo, la información queda congelada.

Ejemplo del flujo real: un trabajador figura como falta; después presenta un descanso médico;
el administrador lo registra en justificaciones y el resultado final cambia.

---

## 7. Regla central del sistema

```
Marcaciones del marcador (dato duro, inmutable)
              +
        Justificaciones (editable mientras el periodo esté abierto)
              =
        Tareo final (resultado)
```

El dato del marcador nunca se altera. Toda corrección vive en la capa de justificaciones,
con rastro de quién la hizo y cuándo. Así el tareo siempre es reconstruible y auditable.

---

## 8. Flujo operativo principal

1. El administrador recibe una vacante nueva.
2. Se autentica en el sistema.
3. Registra los datos del nuevo trabajador (personales + laborales + ID de marcador).
4. Extrae el TXT del marcador por USB.
5. Carga el TXT en el módulo de asistencias.
6. El sistema procesa las marcaciones cruzándolas con el módulo de solicitudes.
7. El resultado se cruza con la tabla de justificaciones, editable por el administrador
   para el cierre previo.
8. Se obtiene el **tareo final**.

---

## 9. Salida: tareo final

Columnas confirmadas hasta ahora:

`Número de documento de identidad · Apellidos y nombres · Planta · Área · Sección · Turno ·
Fecha de ingreso · Fecha de cese · Puesto · …`

⚠️ **Insumo faltante:** el archivo modelo del tareo. Las columnas de resultado (días
trabajados, horas ordinarias, horas extras al 25 % y al 35 %, tardanzas, faltas, ausencias
justificadas) se definen a partir de ese modelo.

---

## 10. Requisitos no funcionales

- **Seguro:** datos personales de trabajadores; aplica la Ley 29733 de Protección de Datos
  Personales del Perú.
- **Escalable** y preparado para integrarse con otras aplicaciones o módulos a futuro.
- **Autosuficiente:** debe operar por sí solo, sin depender de otra aplicación.
- **Auditable:** todo cálculo debe poder reconstruirse y explicarse.

---

## 11. Decisiones pendientes

### Resueltas

| # | Decisión | Resultado |
|---|---|---|
| 1 | Stack | ✅ **C# / .NET 10 LTS + ASP.NET Core MVC + EF Core + SQL Server** |
| 2 | Jornada de 12 h | ✅ **8 h ordinarias + horas extras** (25 % las 2 primeras, 35 % las siguientes) |
| 3 | Obreros y turnos | ✅ **Rotación por calendario**, modelada como asignación vigente por fecha |
| 4 | Marcaciones por día | ✅ **2: entrada y salida.** El refrigerio **no se descuenta**: se computa como hora de labor en todos los turnos |

### Abiertas

| # | Decisión | Bloquea |
|---|---|---|
| 5 | Definición de "unidad" y "tipo de planilla" | Ficha de personal |
| 6 | Duración del periodo y su corte (¿mes calendario? ¿del 26 al 25?) | Cierre y justificaciones |
| 8 | Un turno nocturno de sábado termina el domingo: ¿esas horas son del sábado o cuentan como dominicales? | Motor de cálculo |
| 9 | El sábado se trabajó 12 h, no en el turno `SAB` de 8 h. ¿Cuándo aplica entonces el turno especial de sábado? | Asignación de turnos |

**Resueltas por GCA:**

| # | Decisión | Resolución |
|---|---|---|
| 3 | Horario real de los turnos | **07:00 a 19:00** (diurno) y 19:00 a 07:00 (nocturno). Los turnos sembrados ya eran correctos. |
| 4 | Marcar antes del inicio del turno | **La jornada se computa desde la hora del turno**, no desde la marca, tanto en diurno como en nocturno. Llegar temprano no genera sobretiempo. Concuerda con el D.S. 007-2002-TR art. 9: el sobretiempo es voluntario y debe ser autorizado. |
| 7 | Trabajo en domingo | **Sobretasa del 100 %**, sin descanso sustitutorio. Implementado con el concepto `DescansoSemanalTrabajado` (D. Leg. 713, art. 3). |

> La 5 y la 6 no bloquean el arranque: el modelo ya las soporta como configuración
> (`Unidad` es texto libre, y el periodo guarda fecha de inicio y fin explícitas en vez de
> derivarlas de un mes). La 8 y la 9 deben resolverse antes de dar por cerrado el motor:
> ambas cambian cuánto se paga.

## 12. Insumos pendientes de entrega

1. ~~Archivo TXT real exportado del ZKTeco G3~~ — ✅ **entregado**, formato confirmado.
2. **Archivo modelo del tareo final** — sigue bloqueando la fase de reportes.
3. Convenio colectivo o política interna de sobretasas, si mejora los mínimos legales.
4. **Estructura real de áreas, secciones y puestos** de cada planta. Las sembradas son
   provisionales; ya se pueden cargar desde el módulo de catálogos.

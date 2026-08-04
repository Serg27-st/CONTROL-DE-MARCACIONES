# Roadmap por fases

Orden de construcción del sistema. Cada fase deja algo verificable; las que dependen de un
insumo externo están marcadas para que no se arranquen a ciegas.

---

## Fase 0 — Estructura y dominio ✅ *completada*

Solución .NET de 4 capas, modelo de dominio, agentes por rol y documentación de requerimientos.

---

## Fase 1 — Persistencia ✅ *completada*

- `MarcacionDbContext` con conversión automática a snake_case en español.
- 21 configuraciones `IEntityTypeConfiguration<T>`, con las entidades de `Domain` limpias
  de atributos de EF.
- Migraciones `EsquemaInicial` y `CatalogosProvisionales`, aplicadas sobre SQL Server 2025.
- **Índices críticos creados y verificados:**
  - `uq_marcacion_cruda_trabajador_fecha_marcador`
  - `uq_tareo_diario_trabajador_fecha`
  - Índices de vigencia en `asignacion_turno` y `asignacion_marcador`
- Datos semilla cargados: Intradevco, las 2 empresas, las 4 plantas, los 5 turnos (con SAB
  restringido a Planta 01), 4 tipos de documento, 9 tipos de ausencia y las 5 tasas legales.

---

## Fase 2 — Seguridad y personal ⚠️ *parcial*

Hecho:

- Mantenimiento de trabajadores completo: listado con filtros y paginación, alta y edición.
- Asignación de ID de marcador y de turno **con vigencia por fecha**: al cambiarlas se cierra
  la anterior y se abre una nueva, en vez de reescribir la existente.
- Desplegables encadenados empresa → planta → área → sección, y planta → turnos habilitados
  (el turno SAB solo aparece al elegir Planta 01).
- Reglas verificadas contra la aplicación en ejecución: documento duplicado rechazado,
  ID de marcador solapado rechazado, caso válido aceptado.

- **Mantenimiento de catálogos** funcionando: puestos, plantas, áreas, secciones y tipos de
  documento, con código único por padre, contador de uso y baja lógica bloqueada si el
  catálogo está en uso.

Pendiente:

- **ASP.NET Core Identity** con los cuatro roles `ADMINISTRADOR`, `RRHH`, `SUPERVISOR`,
  `CONSULTA`. Hoy no hay autenticación: la auditoría registra `"sistema"` como usuario.
- Pantallas dedicadas para **turnos**, **periodos** y **tipos de ausencia**: no son código
  más nombre y la pantalla genérica los dejaría mal cargados.

---

## Fase 3 — Ingesta del marcador ✅ *desbloqueada*

> El archivo TXT real ya fue entregado y su formato está confirmado y documentado en los
> requerimientos. La muestra vive en
> `tests/ControlMarcacion.Infrastructure.Tests/Recursos/AEH2200760069_attlog.txt`.
>
> **Ojo con esto:** la columna de tipo de marcación viene siempre en `0`. El equipo no
> distingue entrada de salida, así que el emparejamiento lo tiene que resolver el motor
> contra la ventana del turno.

- Lector del TXT como adaptador en `Infrastructure`, detrás de `IFuenteDeMarcaciones`.
- Plantilla de importación configurable (separador, formato de fecha, codificación).
- Resolución ID de marcador → trabajador **por vigencia a la fecha de la marcación**.
- Deduplicación y detección de archivo ya cargado por hash.
- Pantalla de resultado con filas insertadas, duplicadas y rechazadas **con línea y motivo**.

**Se puede verificar:** se carga un TXT, se recarga el mismo y no se duplica nada.

---

## Fase 4 — Motor de cálculo del tareo ⚠️

> **Parcialmente bloqueada:** faltan las decisiones 7 y 8 de los requerimientos (trabajo en
> día de descanso y feriado, y a qué día pertenecen las horas del nocturno de sábado).
> El resto se puede construir.

- Emparejamiento de las 2 marcaciones diarias con el turno vigente.
- Descuento del refrigerio fijo por turno.
- Jornada ordinaria de 480 minutos; excedente a horas extras 25 % / 35 %.
- Minutos nocturnos caídos en la franja 22:00–06:00.
- Tardanza con tolerancia inclusiva.
- Los 13 casos borde listados en el agente `motor-asistencia`.
- Recálculo idempotente por periodo.

**Se construye con tests primero.** Es el módulo donde un error se paga en soles.

---

## Fase 5 — Solicitudes, justificaciones y cierre

- Solicitudes de ausencia con validación de solapamiento.
- Generación automática de justificaciones desde solicitudes aprobadas.
- Edición manual de justificaciones durante el cierre previo.
- Apertura, cierre y reapertura de periodo con motivo obligatorio.
- Aplicación de la precedencia **justificación > solicitud > marcación** en el cálculo.

**Se puede verificar:** una falta se convierte en ausencia justificada al cargar un descanso
médico, y el periodo cerrado rechaza toda escritura.

---

## Fase 6 — Tareo final y reportes 🔒

> **Bloqueada:** requiere el **archivo modelo del tareo** para definir las columnas de salida.

- Tareo final exportable a Excel.
- Reportes: diario, semanal, mensual, por planta/área/sección, excepciones, ausencias y
  horas extras por trabajador.
- Cada exportación con periodo, filtros, fecha de generación, usuario y estado del periodo.

---

## Fase 7 — Endurecimiento

- Auditoría completa de las acciones sensibles.
- Revisión de seguridad con el agente `seguridad`.
- Rendimiento con volumen real (varios miles de marcaciones por carga).
- Despliegue y respaldo de la base.

---

## Insumos pendientes de GCA

Los dos primeros bloquean fases completas:

1. **Archivo TXT real** del ZKTeco G3 → desbloquea la fase 3.
2. **Archivo modelo del tareo final** → desbloquea la fase 6.
3. Definición de "unidad" y "tipo de planilla".
4. Corte del periodo (mes calendario o del 26 al 25).
5. Convenio colectivo o política de sobretasas, si mejora los mínimos legales.

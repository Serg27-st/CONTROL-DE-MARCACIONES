# QA-E2E-001 — ejecución guiada Hotel Equo

Fecha de inicio: 2026-10-26  
Estado general: **EN EJECUCIÓN**  
Producción: **NO TOCADA**

## Ambiente aislado

| Elemento | Valor |
|---|---|
| Aplicación | `src/ControlMarcacion.Web` |
| URL reservada | `http://127.0.0.1:5317` |
| SQLite | `artifacts/QA-E2E-001-20261026/ControlMarcacion-QA.db` |
| Base operativa/ProgramData | No utilizada |
| OP-03 | No ejecutado |

## Matriz de ejecución

| ID | Etapa | Requisito | Acción | Esperado | Obtenido | Estado | Evidencia |
|---|---|---|---|---|---|---|---|
| QA-E2E-01-01 | Preparación | Ambiente aislado | Completar primer arranque, crear coordinador QA y autenticar | Instancia accesible, setup inicial y SQLite fuera de ProgramData | Usuario creado y autenticado; 1 usuario activo y 1 asignación de rol | APROBADO | Confirmación manual del usuario y verificación SQLite; URL `http://127.0.0.1:5317/` |
| QA-E2E-02-01 | Configuración | Empresa y planta | Verificar la empresa de la plantilla y las plantas disponibles | `CA OUTSOURCING SAC` visible; no crear una planta sin confirmar el Hotel Equo | Empresa y plantas existentes confirmadas visualmente por el usuario | APROBADO | Pantalla `Catalogos/Lista?clave=plantas`; confirmación manual del usuario |
| QA-E2E-02-02 | Configuración | Identidad organizativa | Resolver contradicción de empresa y confirmar código/nombre de la planta Hotel Equo | Razón social, código y nombre definidos por el responsable | Empresa real: `CAPLANI`; sustitución QA autorizada: `CA OUTSOURCING SAC`; código `HOTEL`; nombre definitivo aclarado después: `INVERSIONES E INMOBILIARIA` | APROBADO CON DESVIACIÓN QA | La aclaración posterior del usuario supera el nombre inicial `HOTEL`; la sustitución de empresa no es válida para producción |
| QA-E2E-02-03 | Configuración | Alta de planta | Crear la planta de prueba bajo la empresa sustituta | Planta activa con código `HOTEL`, asociada a `CA OUTSOURCING SAC` | Registro creado inicialmente como `HOTEL / HOTEL`; requiere reconciliar el nombre con la unidad confirmada | APROBADO, AJUSTE PENDIENTE | Integridad SQLite `ok`, sin violaciones FK |
| QA-E2E-02-04 | Configuración | Equipo biométrico | Crear un único equipo de prueba para la planta `HOTEL` | Equipo QA activo, asociado a `HOTEL`, sin serie inventada | Equipo `HOTEL-QA`, nombre registrado `HOTEL`, descripción `HOTEL EQUO`, planta `HOTEL`, activo y sin serie | APROBADO CON OBSERVACIÓN | Confirmación manual y consulta de solo lectura; el nombre/descripcion difieren de la guía pero no alteran la identidad funcional del equipo |
| QA-E2E-02-05 | Configuración | Nombre de planta | Reconciliar el catálogo con la unidad confirmada por el responsable | Código `HOTEL`, nombre `INVERSIONES E INMOBILIARIA`, empresa sustituta `CA OUTSOURCING SAC` | Una fila activa `HOTEL / INVERSIONES E INMOBILIARIA / CA OUTSOURCING SAC`; integridad SQLite `ok` | APROBADO | Confirmación manual y consulta de solo lectura; coincidencias del código `HOTEL` = 1 |
| QA-E2E-03-01 | Personal | Previsualización negativa de fuente original | Analizar la plantilla original sin persistir | 10 filas detectadas y rechazo explicable por unidad `INVERSIONES E INMOBILIARIA` inexistente como planta | Análisis completado y confirmado manualmente; 0 trabajadores, 0 marcadores HOTEL y 0 puestos CAO persistidos; integridad SQLite `ok` | APROBADO | Confirmación del usuario y consulta de solo lectura posterior |
| QA-E2E-03-02 | Personal | Copia de entrada QA | Crear una copia no productiva que conserve la unidad confirmada y mapee el equipo QA | Original intacto; copia con `unidad=INVERSIONES E INMOBILIARIA` y `equipo=HOTEL-QA` | Original: unidad confirmada y equipo vacío; copia: misma unidad y 10/10 filas con `HOTEL-QA`; única diferencia entre ambos = columna `equipo` | APROBADO | Verificación de solo lectura: 10 filas, 24 columnas y 2 hojas en ambos archivos |
| QA-E2E-03-03 | Personal | Previsualización positiva | Analizar la copia QA sin persistir | 10 filas aceptables, 5 puestos nuevos propuestos, 10 marcadores nuevos propuestos, 0 ambigüedades | Previsualización completada y confirmada visualmente por el usuario; 0 trabajadores, 0 marcadores HOTEL y 0 puestos CAO persistidos; integridad SQLite `ok` | APROBADO | Confirmación manual y consulta de solo lectura posterior; la previsualización no alteró la base |
| QA-E2E-03-04 | Personal | Confirmación de carga | Confirmar una sola vez la previsualización aprobada | 10 trabajadores, 5 puestos y 10 marcadores creados sin rechazos | 10 trabajadores, 10 periodos laborales, 10 asignaciones de planta, 10 asignaciones de marcador, 10 marcadores y 5 puestos; sin duplicados; integridad SQLite `ok` | APROBADO | Confirmación manual y reconciliación de solo lectura sobre la base QA |
| QA-E2E-03-05 | Personal | Listado y filtros | Verificar la proyección visible de la carga masiva | 10 trabajadores vigentes bajo `INVERSIONES E INMOBILIARIA`, con equipo y marcador | Listado filtrado completado y confirmado visualmente por el usuario | APROBADO | Filtros aplicados: planta 5, equipo 2, con número, solo vigentes; respaldo estructural previo: 10 asignaciones de marcador |
| QA-E2E-04-01 | Marcaciones | Importación TXT | Cargar el archivo Hotel octubre en el equipo QA | 636 leídas, 636 insertadas, 0 duplicadas, 0 rechazadas y 0 sin asignar | Importación 1: 636 leídas e insertadas, 10 IDs, 0 duplicadas, 0 rechazadas, 0 errores y 0 sin asignación vigente; rango correcto; integridad SQLite `ok` | APROBADO | Resultado manual y reconciliación de solo lectura; origen `NoVerificado`, carga vigente y no anulada |
| QA-E2E-04-02 | Marcaciones | Detección de recarga | Volver a seleccionar el mismo archivo sin autorizar reprocesamiento | Advertencia de archivo previamente cargado; 0 cambios en base | Advertencia confirmada por el usuario; 1 importación, 636 marcas y 0 duplicados físicos después del intento | APROBADO | Confirmación manual y consulta SQLite de solo lectura; integridad `ok` |
| QA-E2E-04-03 | Marcaciones | Recarga idempotente | Autorizar explícitamente el reprocesamiento del mismo archivo | Segunda importación auditada, 0 insertadas, 636 duplicadas y 0 rechazadas; total físico permanece 636 | Importación 2: 636 leídas, 0 insertadas, 636 duplicadas, 0 rechazadas; total físico 636, 0 duplicados físicos, 0 errores; integridad SQLite `ok` | APROBADO | Confirmación manual y reconciliación de solo lectura |
| QA-E2E-05-01 | Turnos | Contrato horario Hotel Equo | Obtener la configuración oficial antes de crear turnos o calcular tareo | Horarios, jornada, refrigerio, días, ventanas, tolerancias y vigencia confirmados | 9 horarios confirmados y creados; 540 min de presencia, 60 min de refrigerio descontable y 480 min efectivos; operación L-D con descanso rotativo; tolerancia de 10 min recuperable | APROBADO | INC-003/004/005 cerradas; falta programar las rotaciones concretas antes de calcular el tareo real |
| QA-E2E-05-02 | Turnos | Normalización de duración | Homogeneizar todos los turnos a 540 min brutos, 60 min de refrigerio y 480 min efectivos | Nueve turnos con duración uniforme y códigos sin conflicto | Creados `REC1` 07:00–16:00, `REC2` 14:00–23:00, `REC3` 22:00–07:00, `COC` 06:00–15:00, `LAV` 08:00–17:00, `HK1` 07:00–16:00, `HK2` 09:00–18:00, `HK3` 07:00–16:00 y `ADM` 09:00–18:00; todos con 480/60 min | APROBADO | Verificación SQLite: 9/9 activos, tolerancia 10, anticipaciones 15/5, gracia 0, días `Todos`, integridad `ok` y 0 violaciones FK |
| QA-E2E-05-03 | Turnos | Restricción por planta | Habilitar cada turno únicamente para `HOTEL / INVERSIONES E INMOBILIARIA` | Nueve relaciones activas en `turno_planta`, una por turno | Corregido el enlace MVC y reeditados los nueve turnos mediante el flujo web; cada turno conserva exactamente una relación activa con la planta HOTEL (`planta_id=5`) | APROBADO | Prueba MVC alta/edición/limpieza verde; reconciliación SQLite 9/9, integridad `ok` y 0 violaciones FK |
| QA-E2E-05-04 | Turnos/Tareo | Trabajo rotativo y recuperación | Tratar sábado/domingo programados como jornada ordinaria y recuperar hasta 10 min a la salida sin duplicar HE | 480 ordinarios en trabajo rotativo; descanso explícito conserva HE100; recuperación consume primero la cola y el remanente conserva umbral de 15 min | Señal explícita `EsTrabajoProgramadoEnRotacion`; jornada limitada a 480; recuperación minuto a minuto hasta tolerancia; regla legacy preservada fuera de rotación | APROBADO | Contratos de fin de semana, tolerancia, aislamiento del régimen industrial e integración persistida; suite Release 1028/1028 |

## Incidencias

| Incidencia | Etapa | Severidad | Reproducción | Esperado | Obtenido | Evidencia | Estado |
|---|---|---|---|---|---|---|---|
| QA-E2E-INC-001 | 3 | Media | Preparación manual de la copia QA | Original intacto y copia adaptada separada | Se modificó temporalmente `equipo` en el original; después fue restaurado. La unidad se confirmó como `INVERSIONES E INMOBILIARIA` en ambos archivos | Verificación final: original con 0 equipos; copia con 10 `HOTEL-QA`; única diferencia = columna `equipo` | CERRADA |
| QA-E2E-INC-002 | 2 | Media | Editar solamente el nombre de la planta `HOTEL` | Código `HOTEL`, nombre `INVERSIONES E INMOBILIARIA` | Código cambiado temporalmente a `INVERSIONES E INMOBI`; luego restaurado | Consulta SQLite de solo lectura: una coincidencia `HOTEL`, nombre y empresa correctos | CERRADA |
| QA-E2E-OBS-001 | 4 | Baja | Cargar TXT sin serie declarada | Origen verificable cuando el formato y la configuración aportan serie | La importación queda con origen `NoVerificado`; el TXT no contiene serie y el equipo QA se creó sin ella | Importación 1, campo `origen_verificado=NoVerificado` | DOCUMENTADA; no bloquea QA funcional |
| QA-E2E-INC-003 | 5 | Alta | Configurar operación Hotel Equo de lunes a domingo con un descanso rotativo | El día programado como trabajo se calcula ordinario independientemente de ser sábado o domingo; el descanso programado trabajado sigue su regla propia | Corregido mediante señal explícita de trabajo programado en rotación; la regla semanal legacy solo opera fuera de esa programación | Contratos rojo→verde para sábado, domingo, cruce nocturno y umbral; integración del servicio verde; suite Release 1028/1028 | CERRADA |
| QA-E2E-INC-004 | 5 | Alta | Configurar tolerancia de 10 minutos recuperable a la salida | Dentro de la regla autorizada, la permanencia al final compensa la tardanza sin generar simultáneamente tardanza y HE | Corregido: recuperación limitada por tardanza, tolerancia y cola real; consume primero la cola y el remanente se somete al umbral de HE | 7 contratos rojo→verde, observación auditable e integración 07:08–16:08; regla de tolerancia cero legacy preservada | CERRADA |
| QA-E2E-INC-005 | 5 | Alta | Marcar `HOTEL / INVERSIONES E INMOBILIARIA` en el alta de un turno y guardar | La selección se enlaza a `Datos.PlantasHabilitadas` y se crea una fila activa en `turno_planta` | Causa: el model binder no poblaba de forma fiable `IReadOnlyList<long>`; corregido a `List<long>`, con adaptación del controlador | Reproducción roja seguida de prueba MVC verde; suite web 40/40; nueve relaciones HOTEL verificadas en SQLite | CERRADA |
| QA-E2E-OBS-002 | 5 | Baja | Informar `Rige desde = 2026-09-22` al crear un turno nuevo | La vigencia solicitada queda registrada o la interfaz explica que en altas se usa vigencia original | El alta ya no solicita una fecha que será ignorada: explica la vigencia histórica desde el origen; `RigeDesde` aparece solamente al editar | Prueba MVC de alta/edición verde; suite web 41/41 y suite Release 1028/1028 | CERRADA |

## Inventario preliminar de entradas

| Control | Resultado |
|---|---:|
| Trabajadores listos para carga | 10 |
| Documentos duplicados | 0 |
| Identificadores biométricos duplicados | 0 |
| Documento distinto del identificador biométrico | 1 |
| Marcaciones válidas | 636 |
| Identificadores únicos en marcaciones | 10 |
| Marcaciones duplicadas exactas | 0 |
| Identificadores sin trabajador en la plantilla | 0 |
| Trabajadores de la plantilla sin marcaciones | 0 |
| Eventos con código `1` | 614 |
| Eventos con código `15` | 22 |
| Rango de marcaciones | 2026-09-22 22:29:18 a 2026-10-26 08:32:36 |

Pendientes de configuración: empresa productiva definitiva, restricción de turnos por planta,
áreas y centros de costo. No se inferirán desde reglas industriales ajenas al Hotel Equo.

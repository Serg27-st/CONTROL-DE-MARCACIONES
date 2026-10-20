# DATA_RELATIONSHIPS — matriz física y relaciones lógicas

Fecha de corte: 2026-10-16. Fuente: `PRAGMA foreign_key_list`, snapshot EF Core y
configuraciones `IEntityTypeConfiguration`. Se inventariaron **47 FK físicas** y no se hallaron
diferencias de columna, destino, nulabilidad u `ON DELETE` entre EF y SQLite.

## FK físicas

`N:1` significa que cada fila hija exige padre; `N:0..1`, que la FK hija es nullable. La
cardinalidad inversa es `1:0..N` salvo que se indique una tabla puente N:M.

| Origen | FK | Destino | Cardinalidad hija→padre | ON DELETE | Uso funcional |
|---|---|---|---|---|---|
| area | planta_id | planta.id | N:1 | RESTRICT | Catálogo organizativo |
| asignacion_area | trabajador_id | trabajador.id | N:1 | CASCADE | Historia de área |
| asignacion_area | area_id | area.id | N:1 | RESTRICT | Área histórica |
| asignacion_marcador | trabajador_id | trabajador.id | N:1 | CASCADE | Atribución temporal |
| asignacion_marcador | equipo_id | equipo_marcador.id | N:1 | RESTRICT | Identidad equipo+código |
| asignacion_planta | periodo_laboral_id | periodo_laboral.id | N:1 | CASCADE | Planta dentro del vínculo |
| asignacion_planta | planta_id | planta.id | N:1 | RESTRICT | Planta histórica |
| asignacion_sueldo | trabajador_id | trabajador.id | N:1 | CASCADE | Historia salarial |
| asignacion_turno | turno_id | turno.id | N:0..1 | RESTRICT | Turno directo, XOR |
| asignacion_turno | trabajador_id | trabajador.id | N:1 | CASCADE | Historia de turno/cuadrilla |
| asignacion_turno | cuadrilla_id | cuadrilla.id | N:0..1 | RESTRICT | Cuadrilla, XOR |
| asp_net_role_claims | role_id | asp_net_roles.id | N:1 | CASCADE | Identity |
| asp_net_user_claims | user_id | asp_net_users.id | N:1 | CASCADE | Identity |
| asp_net_user_logins | user_id | asp_net_users.id | N:1 | CASCADE | Identity |
| asp_net_user_roles | user_id | asp_net_users.id | N:1 | CASCADE | Puente N:M Identity |
| asp_net_user_roles | role_id | asp_net_roles.id | N:1 | CASCADE | Puente N:M Identity |
| asp_net_user_tokens | user_id | asp_net_users.id | N:1 | CASCADE | Identity |
| centro | planta_id | planta.id | N:0..1 | RESTRICT | Centro opcionalmente localizado |
| centro | empresa_id | empresa.id | N:1 | RESTRICT | Centro de empresa |
| centro_costo | centro_id | centro.id | N:1 | RESTRICT | Centro contable |
| cuadrilla | planta_id | planta.id | N:1 | RESTRICT | Cuadrilla de planta |
| empresa | cliente_id | cliente.id | N:1 | RESTRICT | Jerarquía organizativa |
| equipo_marcador | planta_id | planta.id | N:1 | RESTRICT | Equipo físico de planta |
| error_importacion | importacion_id | importacion_marcador.id | N:1 | CASCADE | Errores de carga oficial |
| importacion_marcador | equipo_id | equipo_marcador.id | N:1 | RESTRICT | Origen del archivo |
| justificacion | solicitud_ausencia_id | solicitud_ausencia.id | N:0..1 | RESTRICT | Origen opcional de justificación |
| marcacion_cruda | importacion_id | importacion_marcador.id | N:1 | RESTRICT | Evidencia de carga |
| marcacion_cruda | equipo_id | equipo_marcador.id | N:1 | RESTRICT | Aparato origen |
| marcador | planta_id | planta.id | N:1 | RESTRICT | Proyección de catálogo por planta |
| marcador | equipo_id | equipo_marcador.id | N:1 | RESTRICT | Código dentro del equipo |
| periodo_laboral | trabajador_id | trabajador.id | N:1 | CASCADE | Historia del vínculo |
| periodo_laboral | empresa_id | empresa.id | N:1 | RESTRICT | Empresa histórica |
| planta | empresa_id | empresa.id | N:1 | RESTRICT | Jerarquía organizativa |
| programacion_cuadrilla | cuadrilla_id | cuadrilla.id | N:1 | RESTRICT | Secuencia finita de cuadrilla |
| prov_error_importacion | importacion_provisional_id | prov_importacion.id | N:1 | CASCADE | Errores PMV |
| prov_importacion | equipo_id | equipo_marcador.id | N:1 | RESTRICT | Origen PMV |
| prov_marcacion | importacion_provisional_id | prov_importacion.id | N:1 | RESTRICT | Evidencia PMV |
| prov_marcacion | equipo_id | equipo_marcador.id | N:1 | RESTRICT | Aparato PMV |
| seccion | area_id | area.id | N:1 | RESTRICT | Jerarquía organizativa |
| solicitud_ausencia | tipo_ausencia_id | tipo_ausencia.id | N:1 | RESTRICT | Clasificación de ausencia |
| tramo_rotacion | turno_id | turno.id | N:0..1 | RESTRICT | Nulo solo en descanso |
| tramo_rotacion | programacion_id | programacion_cuadrilla.id | N:1 | RESTRICT | Tramo de secuencia |
| turno_planta | turno_id | turno.id | N:1 | CASCADE | Puente N:M turno/planta |
| turno_planta | planta_id | planta.id | N:1 | RESTRICT | Puente N:M turno/planta |
| usuario_planta | planta_id | planta.id | N:1 | RESTRICT | Puente N:M alcance usuario |
| usuario_planta | usuario_id | asp_net_users.id | N:1 | CASCADE | Puente N:M alcance usuario |
| version_turno | turno_id | turno.id | N:1 | CASCADE | Historia del contenido del turno |

## Relaciones lógicas no materializadas como FK

Hay **24 relaciones lógicas** relevantes: 22 columnas escalares que nombran otro agregado sin
constraint y dos atribuciones temporales sin columna destino. No son errores de PRAGMA; son el
modelo físico vigente.

| Origen | Clave o regla | Destino lógico | Cardinalidad/semántica | Protección efectiva |
|---|---|---|---|---|
| trabajador | tipo_documento_id | tipo_documento.id | N:1 | Servicio/dominio |
| trabajador | empresa_id | empresa.id | N:1 actual | Servicio; proyección actual |
| trabajador | planta_id | planta.id | N:1 actual | Servicio; proyección actual |
| trabajador | area_id | area.id | N:0..1 actual | Servicio |
| trabajador | seccion_id | seccion.id | N:0..1 actual | Servicio |
| trabajador | puesto_id | puesto.id | N:1 actual | Servicio |
| trabajador | centro_costo_id | centro_costo.id | N:0..1 actual | Servicio |
| feriado | planta_id | planta.id | N:0..1; null=nacional | ServicioFeriados |
| puesto | empresa_id | empresa.id | N:0..1 | Catálogo/semilla |
| tasa_sobretasa | empresa_id | empresa.id | N:0..1; null=general | Semilla/servicio; consumidor no filtra hoy |
| solicitud_ausencia | trabajador_id | trabajador.id | N:1 | Servicio/alcance |
| justificacion | trabajador_id | trabajador.id | N:1 | Servicio/índice funcional |
| justificacion | periodo_id | periodo.id | N:1 | Servicio |
| justificacion | tipo_ausencia_id | tipo_ausencia.id | N:0..1 | Servicio |
| tareo_diario | periodo_id | periodo.id | N:1 | Orquestador de cálculo |
| tareo_diario | trabajador_id | trabajador.id | N:1 | Orquestador; unique por fecha |
| tareo_diario | turno_id | turno.id | N:0..1 | Proyección calculada |
| tareo_diario | justificacion_id | justificacion.id | N:0..1 | Proyección calculada |
| reintegro_tareo | periodo_id | periodo.id | N:1 | Servicio; unique compuesto |
| reintegro_tareo | trabajador_id | trabajador.id | N:1 | Servicio; unique compuesto |
| asp_net_users | tipo_documento_id | tipo_documento.id | N:0..1 | Seguridad/aplicación |
| asp_net_users | empresa_id | empresa.id | N:0..1 | Seguridad/aplicación |
| marcacion_cruda | equipo+id_marcador+fecha_hora dentro de vigencia | trabajador vía asignacion_marcador | N:0..1 por marca | Resolución temporal; no `trabajador_id` persistido |
| prov_marcacion | equipo+id_marcador+fecha_hora dentro de vigencia | trabajador vía asignacion_marcador | N:0..1 por marca | Consulta PMV; no persistido |

Además, el cálculo consume marcas para producir `tareo_diario`, pero no existe una relación
persistida marca↔tareo ni una tabla puente. Esto impide reconstruir solo con FK el conjunto
exacto de marcas reclamadas por cada resultado.

## Cardinalidades e invariantes que no se deducen solo de la FK

- `trabajador 1:N periodo_laboral`; un índice parcial limita a uno abierto.
- `periodo_laboral 1:N asignacion_planta`; un índice parcial limita a una planta abierta.
- `turno 1:N version_turno`; un índice parcial limita a una versión abierta y activa.
- `asignacion_turno` referencia exactamente un turno o una cuadrilla por check XOR.
- `tramo_rotacion` de trabajo exige turno; el descanso exige turno nulo y un día.
- `asp_net_users N:M asp_net_roles` mediante `asp_net_user_roles`.
- `turno N:M planta` mediante `turno_planta`.
- `asp_net_users N:M planta` mediante `usuario_planta`.


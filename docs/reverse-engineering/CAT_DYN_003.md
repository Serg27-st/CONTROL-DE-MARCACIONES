# CAT-DYN-003 — Preparación automática para tareo

Fecha de cierre técnico: 2026-10-29  
Estado: **COMPLETADO EN CÓDIGO Y PRUEBAS**

## 1. Propósito

CAT-DYN-003 agrega un diagnóstico histórico, explicable y de solo lectura que responde si la
configuración de un trabajador permite calcular tareo para una fecha o intervalo. No calcula
asistencia, no genera tareos, no interpreta la ausencia de marcas como un problema de catálogo
y no completa datos automáticamente.

La distinción autoritativa es:

```text
TRABAJADOR_IMPORTADO
≠
TRABAJADOR_LISTO_PARA_TAREO
≠
TRABAJADOR_SIN_INCIDENCIAS_DE_ASISTENCIA
```

## 2. Modelo del verificador

El contrato público es `IValidadorPreparacionTareo` y admite:

- evaluación de una fecha;
- evaluación de un intervalo para un trabajador;
- evaluación masiva de los trabajadores autorizados;
- filtros por periodo, unidad y estado;
- contexto de operación: consulta histórica, cálculo, recálculo o cierre.

Cada resultado conserva trabajador, documento, intervalo, estado agregado, conteos diarios,
detalle por fecha e incidencias deduplicadas. Cada incidencia incluye un código estable,
explicación, acción recomendada y, cuando existe, referencia técnica.

La precedencia es determinista:

```text
CONFLICTO > INCOMPLETO > LISTO
```

Un conflicto nunca se degrada a dato faltante ni se resuelve escogiendo una relación al azar.

## 3. Reglas CATREADY

| Código | Preparación comprobada |
|---|---|
| CATREADY-01 | `PeriodoLaboral` vigente en la fecha |
| CATREADY-02 | empresa histórica existente |
| CATREADY-03 | `AsignacionPlanta` histórica única y existente |
| CATREADY-04 | equipo existente y perteneciente a la unidad histórica |
| CATREADY-05 | asignación vigente y catálogo activo del par equipo + ID marcador |
| CATREADY-06 | turno fijo o programación de cuadrilla resoluble |
| CATREADY-07 | día laborable o descanso determinable, incluido descanso rotativo explícito |
| CATREADY-08 | periodo que cubre la fecha y admite la operación solicitada |
| CATREADY-09 | ausencia de relaciones históricas solapadas o contradictorias |
| CATREADY-10 | unidad dentro del alcance efectivo del usuario |

Los periodos cerrados siguen siendo válidos para consulta histórica, pero no se anuncian como
aptos para cálculo, recálculo o cierre. Los días sin vínculo se reportan como configuración
incompleta; no se inventa vigencia laboral.

## 4. Evaluación histórica

La resolución usa las autoridades ya aprobadas:

- `PeriodoLaboral`: vínculo y empresa históricos;
- `AsignacionPlanta`: unidad histórica dentro del vínculo;
- `AsignacionMarcador`: vigencia del par trabajador/equipo/ID;
- `Marcador`: existencia activa del par equipo/código;
- `AsignacionTurno`: vigencia del turno fijo o cuadrilla;
- `ProgramacionCuadrilla` y sus tramos: trabajo o descanso rotativo;
- `Periodo`: cobertura y compatibilidad de la operación.

Los cambios de equipo, turno, vínculo o unidad dentro de un periodo se resuelven por cada fecha.
El servicio no usa las proyecciones actuales de `Trabajador` como sustituto de la historia.

## 5. Configuración frente a asistencia

El verificador no consulta `MarcacionCruda` ni `TareoDiario`. Por ello:

- un trabajador puede estar `LISTO` y no haber marcado todavía;
- un trabajador `LISTO` puede luego tener falta, tardanza, salida anticipada u otra incidencia;
- una marcación ausente no convierte por sí sola la configuración en `INCOMPLETO`;
- no se calculan ordinarias, extras, nocturnidad, ausencias, feriados ni importes.

Los algoritmos económicos y de asistencia no fueron modificados.

## 6. Carga masiva y flujo previo al tareo

La carga masiva conserva su contrato: una fila válida puede importar al trabajador aunque falte
su turno. Después de una importación confirmada se ofrece el enlace **Revisar preparación para
tareo**, sin convertir el diagnóstico en parte de la transacción de carga.

La pantalla de tareo ofrece **Ver preparación** para el periodo abierto y declara la operación
como recálculo. El diagnóstico informa afectados y acciones, pero no cambia silenciosamente el
alcance ni bloquea a los trabajadores correctos porque otros estén incompletos.

## 7. Interfaz y seguridad

La vista `PreparacionTareo/Index` está limitada a `COORDINADOR` y `ADMINISTRADOR`. Muestra:

- trabajador y documento;
- unidad histórica;
- periodo;
- estado y conteo de días;
- problemas detectados y acciones recomendadas.

Los filtros son periodo, unidad, estado y tipo de operación. Tanto el conjunto evaluado como el
catálogo visible de unidades respetan `IProveedorAlcancePlantas`:

- `null`: usuario sin restricción por unidad;
- colección vacía: ningún acceso;
- colección con IDs: solo esas unidades.

No hay botones de autorreparación ni escrituras desde esta vista.

## 8. Pruebas

Los contratos `CATREADY-01..20` usan SQLite real y cubren configuración completa, faltantes,
solapamientos, descanso, consulta histórica, cambios de equipo y turno, cese, reingreso, alcance
de seguridad, solo lectura, independencia entre periodos y aislamiento respecto de marcas y
tareos.

La prueba integral contiene trabajadores listos, sin turno, sin marcador, con conflicto de
vigencias y con cambio de turno dentro del periodo. Los tres estados quedan diferenciados con
motivos verificables.

Resultado final:

```text
Pruebas dirigidas CATREADY: 20/20 verdes
Suite completa:              1087/1087 verdes
Omitidas:                    0
Build Release:               0 errores / 0 advertencias
```

## 9. Limitaciones explícitas

- Es un diagnóstico bajo demanda; no existe todavía una proyección persistida de readiness.
- No reemplaza la validación del motor al calcular cada jornada.
- No bloquea globalmente cálculo, recálculo o cierre.
- No repara catálogos, vigencias ni programaciones.
- No evalúa la calidad de las marcaciones ni el resultado económico.
- No modifica PMV, FER-005, ASIS-001 ni REP-004.

## 10. Impacto operativo

No se creó migración. No se tocó producción, no se ejecutó OP-03, no se desplegó y no se creó
commit. Los cambios preexistentes del repositorio fueron preservados.


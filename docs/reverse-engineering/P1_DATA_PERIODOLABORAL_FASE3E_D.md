# P1-DATA-PERIODOLABORAL — Fase 3E-D

## Regresión integrada y cierre técnico

Estado: **COMPLETADA**  
Fecha: 2026-10-15  
Alcance: validación integrada de `PeriodoLaboral`, `AsignacionPlanta`, consumidores
históricos, cuadrillas, PMV, reingreso, reportes, tareo y atribución de marcaciones.

No se desarrollaron funcionalidades ni se modificó código productivo.

## 1. Baseline real

Se ejecutó sobre el working tree acumulado de las fases 3B–3E-C:

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 419/419 |
| Infrastructure | 490/490 |
| Web | 28/28 |
| **Total** | **937/937 verdes; 0 fallos; 0 omitidos** |

El primer intento de `dotnet test` dentro del sandbox fue anulado antes de ejecutar
pruebas porque `vstest` no pudo conectar con `testhost` en 90 segundos. La misma suite se
ejecutó fuera de ese aislamiento, sin acceso a producción, y produjo el baseline válido
anterior.

## 2. Base de prueba

La regresión que ejerce escrituras, reingreso, contexto laboral, cuadrillas, PMV, tareo y
atribución utiliza conexiones SQLite temporales en memoria con `EnsureCreated`. Cada clase
crea y destruye su propia base.

Los contratos históricos de reportes se complementan con el proveedor EF InMemory ya
existente. No se usó la base productiva ni una copia de ella.

El conjunto representa:

- trabajador activo;
- trabajador cesado;
- trabajador reingresado;
- cambio de planta dentro de un vínculo;
- cambio de empresa entre vínculos, que es el caso permitido por el modelo;
- dos periodos laborales históricos;
- gap laboral entre cese y reingreso.

## 3. Matriz de regresión

| Secuencia | Contrato ejecutable principal | Resultado |
|---|---|---|
| Alta | `PL3D_WRITE_01`, creación usada por `PL3EC_APP_*` | periodo abierto, planta inicial y proyección sincronizados |
| Cambio de planta | `PL3C_REP_06`, `PL3EB_CUA_01`, `PL3EB_PMV_01` | tramo anterior preservado; resolución anterior/posterior correcta |
| Cese | `PL3D_WRITE_01`, `PL3C_TAREO_02`, `PL3C_REP_03` | periodo cerrado, sin periodo abierto y proyección de cese sincronizada |
| Reingreso | `PL3EC_APP_01`, `PL3EC_APP_04`, `PL3EC_APP_05` | segundo periodo abierto, nueva planta, empresa y proyección actualizadas |
| Gap laboral | `PL3C_TAREO_03`, `PL3C_REP_05` | `SinVinculo`/sin filas; no se inventa vigencia |
| Historia inmutable | `PL3D_WRITE_03`, `PL3EC_APP_04` | periodo y asignación cerrados permanecen intactos |
| Atomicidad | `PL3D_WRITE_04`, `PL3EC_APP_06` | fallo intermedio revierte raíz, periodo y planta |
| Concurrencia | `PL3EC_APP_07` | doble reingreso se rechaza con error funcional controlado |

La unicidad del periodo abierto y la ausencia de solapamientos también permanecen cubiertas
por `PL3D_WRITE_02`, `PL3EC_APP_02` y `PL3EC_APP_03`.

## 4. Cuadrillas

Se validaron conjuntamente:

- `AsignarCuadrillaAsync`: usa el contexto laboral de la fecha y no
  `Trabajador.PlantaId` (`PL3EB_CUA_01`);
- fecha anterior al ingreso y posterior al cese: rechazadas (`PL3EB_CUA_02/03`);
- reingreso posterior: no reemplaza el contexto del vínculo anterior
  (`PL3EB_CUA_04`);
- `IntegrantesDeCuadrillaAsync`: conserva al integrante en su planta histórica y aplica la
  planta nueva después del traslado (`PL3EB_INT_01/02`);
- `CandidatosParaCuadrillaAsync`: respeta ingreso, cese y planta histórica
  (`PL3EB_CAN_01..03`).

## 5. PMV

`PL3EB_PMV_01` confirma el caso requerido:

```text
marcación histórica en Planta 1
+ trabajador actualmente en Planta 2
→ PMV muestra Planta 1
```

`PL3EB_PMV_02` confirma además que la ausencia de contexto histórico no cae en
`Trabajador.PlantaId`: devuelve planta nula y una observación explícita.

## 6. Reportes

`ResumenTareoAsync`, `ExcepcionesAsync` y `TareoFinalAsync` llaman al mismo
`ResolverTrabajadoresAsync`. Ese resolvedor obtiene población, empresa, planta y vigencia
desde `ConsultaContextoLaboral`; después, cada fila se recorta con `IncluyeFecha`.

Contratos relevantes:

- empresa histórica y no actual: `PL3C_REP_01`;
- planta histórica y no actual: `PL3C_REP_02`;
- cesado visible solo en su rango: `PL3C_REP_03`;
- preingreso excluido: `PL3C_REP_04`;
- reingreso sin fusionar episodios: `PL3C_REP_05`;
- cambio de planta representado y filtrado por día: `PL3C_REP_06`;
- resumen, excepciones y tareo final: cubiertos también por
  `AlcanceYVigenciaReportesTests`, `FiltroPorAparatoEnReportesTests` y
  `GeneracionTareoFinalEndToEndTests`.

La validación no encontró fallback a campos planos de empresa/planta en esos tres flujos.

## 7. Tareo

Resultado por posición temporal:

| Fecha consultada | Resultado |
|---|---|
| antes del ingreso | `SinVinculo`; cero minutos y cero incidencias |
| durante vínculo | cálculo normal |
| gap entre vínculos | `SinVinculo`; cero minutos y cero incidencias |
| después del cese | `SinVinculo`; cero minutos y cero incidencias |
| nuevo vínculo | cálculo normal |

Lo prueban `PL3C_TAREO_01..03`. `PL3C_TAREO_04` y `PL3C_TAREO_06` añaden
feriado y alcance por planta histórica. Las justificaciones fuera de vínculo se conservan
como evidencia, pero no reinterpretan el día (`PL3C_TAREO_05`).

## 8. Marcaciones

No se modificó la importación. Los contratos `AtribucionAlCalcularTests` y
`ConsultasDeAtribucionEnSqliteTests` siguen resolviendo al trabajador por la combinación:

```text
EquipoId + IdMarcador + vigencia de AsignacionMarcador
```

La coincidencia de `IdMarcador` en otro equipo no atribuye la marca al trabajador.

## 9. Ejecución dirigida

Además de la suite completa, se ejecutaron filtros de regresión sobre los consumidores
afectados:

| Grupo | Resultado |
|---|---:|
| Infrastructure: historia, escrituras, PMV, reportes, tareo y atribución | 83/83 |
| Domain/Application: cambio de turno y cuadrillas | 25/25 |
| **Total dirigido** | **108/108; 0 fallos; 0 omitidos** |

## 10. Resultado técnico

- Regresiones demostradas: **0**.
- Cambios de código productivo: **0**.
- Tests nuevos: **0**; se reutilizaron los contratos ejecutables ya incorporados por las
  fases anteriores.
- Migraciones nuevas o aplicadas: **0**.
- Base productiva: **no accedida**.
- Producción y OP-03: **no tocados**.
- Commit: **no creado**.

## 11. Cierre

`PeriodoLaboral` permanece como autoridad de vigencia y empresa; `AsignacionPlanta`, como
autoridad de planta en el tiempo. Alta, traslado, cese, gap y reingreso conviven con
cuadrillas, PMV, reportes, tareo y atribución sin regresiones observadas.

Estado final: **P1-DATA-PERIODOLABORAL / FASE 3E-D COMPLETADA**.

Este cierre técnico no autoriza despliegue, migraciones productivas ni ejecución de OP-03.

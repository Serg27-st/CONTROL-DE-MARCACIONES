# Mapa de contexto para IA

Para decisiones funcionales, cargar primero `BUSINESS_RULES_FINAL.md`.
`BUSINESS_RULES.md` es evidencia histórica de Fase 6.1, no la autoridad vigente.

## Regla principal

No vuelvas a realizar ingeniería inversa completa del repositorio salvo que exista evidencia de que la documentación quedó obsoleta. Para cualquier tarea sigue esta ruta:

`PROJECT_STATE.md` → `MASTER_DOSSIER.md` → este mapa → ficha del módulo → archivos específicos.

Abre `BUSINESS_RULES_FINAL.md` para reglas vigentes; usa `BUSINESS_RULES.md`,
`ACCEPTANCE_RULES.md`, `DATABASE.md` o documentos de fase cuando se necesite trazabilidad,
evidencia histórica o resolver una contradicción.

## Enrutamiento por tarea

| Tarea | Leer primero | Código mínimo | Pruebas/evidencia |
|---|---|---|---|
| Horas extra, sábado, domingo | [ATTENDANCE_TAREO](modules/ATTENDANCE_TAREO.md), [ACCEPTANCE_RULES](ACCEPTANCE_RULES.md) | `Domain/Asistencia/CalculoTareo.cs`, `Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs` | `ReglasConfirmadasHorasExtraAcceptanceTests.cs` |
| Feriados y 1 de mayo | [cierre P0-FER](P0_FER_002_007_HOLIDAY_ECONOMICS.md), [ACCEPTANCE_RULES](ACCEPTANCE_RULES.md) | `CalculoTareo.cs`, `SobretasaDtos.cs`, `ServicioCalculoTareo.cs`, vista de Asistencia | `FeriadosEconomiaAcceptanceTests.cs`, `LiquidacionPeriodoAcceptanceTests.cs` |
| Tardanza | [ATTENDANCE_TAREO](modules/ATTENDANCE_TAREO.md) | `CalculoTareo.cs`, `VersionTurno.cs` | `TardanzaPlanillaAcceptanceTests.cs` |
| Turnos o rotaciones | [SHIFTS_ROTATIONS](modules/SHIFTS_ROTATIONS.md), [ADR descanso](ADR_ROTATION_REST_MODEL.md), D10 | `Turno.cs`, `VersionTurno.cs`, `ProgramacionCuadrilla.cs`, `ServicioCuadrillas.cs` | `RotacionTurnosTests.cs`, `ProgramacionCuadrillaTests.cs`, `RotacionesVariablesAcceptanceTests.cs` |
| PMV | [PROVISIONAL_MARKINGS](modules/PROVISIONAL_MARKINGS.md), D14 | `MarcacionesProvisionales/*`, controlador PMV | pruebas `MarcacionesProvisionales/*` |
| Reportes/Excel | [REPORTS](modules/REPORTS.md), D12 | `ServicioReportes.cs`, `ExportadorReportesExcel.cs` | pruebas `Reportes/*` |
| Personal, cese, reingreso, padrón o historia laboral | [PERSONNEL](modules/PERSONNEL.md), [cierre 3E-D](P1_DATA_PERIODOLABORAL_FASE3E_D.md) | `ServicioTrabajadores.cs`, `PeriodoLaboral.cs`, `AsignacionPlanta.cs`, `ConsultaContextoLaboral.cs`; carga masiva solo si aplica | contratos `PL3C`, `PL3D`, `PL3EB`, `PL3EC` y dossier 3E-D |
| Importación principal | [MARKINGS](modules/MARKINGS.md), D07 | `ServicioImportacion.cs`, lectores ZKTeco | pruebas `Marcaciones/*` |
| Comparar importación principal y PMV | [MARKINGS](modules/MARKINGS.md) + [PROVISIONAL_MARKINGS](modules/PROVISIONAL_MARKINGS.md) | ambos servicios de importación; lectores compartidos | suites Marcaciones y PMV |
| Seguridad/plantas | [SECURITY](modules/SECURITY.md) | proveedor de alcance HTTP, `AlcancePlantasConsultas.cs`, servicios afectados | `AislamientoPorPlantaTests.cs`, `AislamientoPmvAcceptanceTests.cs` (SEC-01..03 rojos) |
| SQLite, modelo o migraciones | [DATABASE](DATABASE.md), D06 | `MarcacionDbContext.cs`, configuración EF y migración puntuales | Fase 3B; no abrir snapshots salvo necesidad EF |
| Solicitudes/justificaciones | [REQUESTS_JUSTIFICATIONS](modules/REQUESTS_JUSTIFICATIONS.md), D11 | servicios y entidades del módulo | pruebas Solicitudes/Justificaciones |
| Deuda o priorización | [TECHNICAL_DEBT](TECHNICAL_DEBT.md), [REMEDIATION_PLAN](REMEDIATION_PLAN.md) | Solo si se implementará una entrada | aceptación asociada |

## Para cambios de reglas laborales

Antes de inferir una regla desde el código, leer en este orden:

1. [ACCEPTANCE_RULES.md](ACCEPTANCE_RULES.md).
2. [REQUIREMENTS_TRACEABILITY.md](REQUIREMENTS_TRACEABILITY.md).
3. [BUSINESS_RULES_FINAL.md](BUSINESS_RULES_FINAL.md).

Después abrir la ficha modular, 2–6 archivos específicos y los contratos asociados. El código describe el comportamiento actual; no prevalece sobre un requisito confirmado posterior.

## Archivos críticos globales

- `src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs`
- `src/ControlMarcacion.Infrastructure/Marcaciones/ServicioImportacion.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioReportes.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/MarcacionDbContext.cs`
- `src/ControlMarcacion.Web/Program.cs`
- `docs/reverse-engineering/ACCEPTANCE_RULES.md`
- `docs/reverse-engineering/REMEDIATION_PLAN.md`

## Exclusiones específicas por ruta

- Horas extra o tardanza: no cargar importación, PMV ni reportes completos salvo impacto demostrado.
- Rotaciones: añadir D05 y D10; no cargar todas las migraciones.
- Descanso de rotación: R-P1-05 ya implementó la opción B (tipo explícito + FK nullable),
  secuencia finita, resolución discriminada y delimitación BD-01 en dos pasadas. Leer el ADR,
  BD-01, BD-02 y los contratos ROT-DESC/ROT-MARK/ROT-FER antes de modificarla; no volver a
  introducir repetición por módulo ni un turno ficticio. La migración existe, pero no se ha
  aplicado a la base canónica.
- PMV: limitarse a su ficha, `IFuenteDeMarcaciones`/lectores si aplica, asignaciones, alcance por planta y pruebas PMV.
- Base/migración: leer `DATABASE.md`, D06, configuración EF y migración específicas; no cargar el snapshot completo por defecto.

## Contexto que debe excluirse normalmente

- `.git/`, `bin/`, `obj/`.
- Ejecutables o carpetas de despliegue.
- ZIP, RAR y otros comprimidos.
- Bases de respaldo, `.bak` y copias previas.
- Archivos `*.Designer.cs` y `MarcacionDbContextModelSnapshot.cs`.
- Toda la carpeta de migraciones cuando solo interesa una entidad o regla.
- Todos los documentos de fases antiguas en bloque.

## Cuándo ampliar contexto

- Contradicción negocio/código: `ACCEPTANCE_RULES.md` y caso de prueba exacto.
- Linaje vigente: `BUSINESS_RULES_FINAL.md`; IDs históricos: `BUSINESS_RULES.md`; flujos: D07–D12.
- Problema físico: `DATABASE.md`, configuración EF concreta y migración asociada.
- Decisión arquitectónica discutida: `INDEPENDENT_ARCHITECTURE_REVIEW.md`.
- Auditoría histórica: documento de fase correspondiente.

## Límites de interpretación

- No promover 480 minutos a regla vigente; es el comportamiento defectuoso reproducido.
- No usar cinco minutos como tolerancia de planilla; la regla vigente es 0.
- No generalizar 14+1+7; es un patrón observado.
- No asumir que PMV está protegido porque el flujo principal sí aplica alcance.
- No reducir domingo trabajado a “como feriado”: el requisito vigente asigna todo el tiempo al 100 %.
- No usar cinco minutos para salida anticipada de planilla; su tolerancia también es 0.
- No asumir que un reporte recalcula: consume tareo persistido.
- Para una fecha histórica, `PeriodoLaboral` es autoridad de vigencia/empresa y
  `AsignacionPlanta` de planta en reportes y tareo; los planos de `Trabajador` son proyección actual.
- Esa autoridad también rige cuadrillas y la planta mostrada por PMV. No usar
  `Trabajador.PlantaId`, `EmpresaId`, `FechaIngreso` o `FechaCese` como fallback histórico.
- Cambiar empresa durante un vínculo activo no reescribe historia: requiere cese y reingreso.
- Los gaps entre vínculos son válidos y se resuelven como `SinPeriodoLaboral`/`SinVinculo`.

## Tamaño de contexto recomendado

- Tarea localizada: estado + dossier + una ficha + 2–5 archivos de código + pruebas específicas.
- Tarea transversal: lo anterior + uno o dos diagramas + reglas relevantes.
- Auditoría: añadir documento de fase y evidencia física concreta; evitar cargar todo el repositorio.
# Contexto local Windows

Para hosting/instalador consultar `P1_DESKTOP_LOCAL_INSTALLER.md` y
`../LOCAL_INSTALLATION_GUIDE.md`. Actual: Windows local loopback. Futuro: servidor Web.

# Contexto ZKTeco SDK

Para BKtransaction y SDK leer primero `ZKTECO_SDK_INTEGRATION.md` y
`BKTRANSACTION_SDK_COMPARISON.md`. Estado: `Interno` es autoritativo; `Sdk` y `Comparar` son
diagnóstico/desarrollo. No asumir que reconocer 981 filas significa decodificarlas: el PULL SDK
2.2.0.220 produjo 0 marcaciones válidas para el corpus ZAM70. No agregar DLL al instalador ni
afirmar redistribución autorizada mientras no exista licencia explícita.

# Contexto PeriodoLaboral — cierre 3E-E

Estado vigente: **P1-DATA-PERIODOLABORAL CERRADO**. La antigua indicación de Fase 3C
“LISTO PARA FASE 3D” está **SUPERADA**.

Para trabajo normal leer primero `P1_DATA_PERIODOLABORAL_FASE3E_D.md`; ampliar al dossier de
la fase exacta solo por trazabilidad:

- 3B: saneamiento/backfill sobre copia controlada;
- 3C y matriz: resolución central y consumidores históricos;
- 3D: invariantes y persistencia selectiva;
- 3E-A: semántica y matriz legacy;
- 3E-B: cuadrillas y PMV;
- 3E-C: reingreso Application/UI;
- 3E-D: regresión integrada.

`ConsultaContextoLaboral` resuelve vínculo, empresa y planta por fecha; ausencia de periodo no
admite fallback silencioso y una ambigüedad es error de configuración. No sustituir
`AsignacionMarcador` para atribuir marcas. Los cuatro campos planos de `Trabajador` son
proyección actual/compatibilidad, no historia.

Deuda residual: dual-write y UI/importación actuales, constraints físicos incompletos para
solapamientos cerrados/rango y eliminación futura de columnas. Producción, migraciones
productivas y OP-03 no fueron ejecutados.


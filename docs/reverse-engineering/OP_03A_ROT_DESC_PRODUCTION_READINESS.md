# OP-03A — Production Readiness ROT-DESC

Fecha: 2026-10-09  
Resultado: **NO VALIDADO**  
Listo para programar ventana: **NO**  
OP-03 ejecutado: **NO**  
Migración productiva: **NO**

## 1. Decisión

OP-03A creó y congeló un release nuevo, ligó build y 807 pruebas al mismo estado de código,
confirmó en modo solo lectura que la base candidata termina en
`20260923140604_RotacionPorCuadrilla`, inventarió siete migraciones pendientes y ensayó la
cadena completa con éxito sobre una copia consistente.

La calendarización sigue bloqueada. No pudo demostrarse que la base candidata sea la ruta
efectiva de una instancia productiva activa; faltan responsables nominales, destino definitivo
de backup, aprobación del piloto/período y una cuenta `ADMINISTRADOR` restringida por backend.
El smoke autenticado del ZIP tampoco quedó cerrado porque no existe una credencial autorizada
que coincida con la copia y OP-03A no permite crearla.

No se detuvo ninguna aplicación productiva, no se escribió en la base de ProgramData, no se
recalculó tareo y no se creó programación productiva.

## 2. Release

### 2.1 ZIP anterior

El archivo histórico se conservó sin cambios:

| Campo | Valor |
|---|---|
| Ruta | `despliegue/ControlMarcaciones.zip` |
| Fecha | 2026-09-24 09:43:16 |
| SHA-256 | `10120A42A30F5716E5F5EFFA8B580D14A9451A1D61D2B50E1DB697883A1AE994` |
| Clasificación | **RECHAZADO PARA PRODUCCIÓN** |

No se sobrescribió.

### 2.2 Release nuevo congelado

| Campo | Valor |
|---|---|
| Nombre | `ControlMarcacion_ROT_DESC_20261009_op03a01` |
| ZIP | `artifacts/op03a-rot-desc-20261009/release/ControlMarcacion_ROT_DESC_20261009_op03a01.zip` |
| Fecha freeze | 2026-10-09 09:07:37 America/Lima |
| Rama | `sergio4` |
| Commit base | `92d9177b1fad4a78f639d6ce7dc1dd77b08ef489` |
| Estado Git | dirty; identidad exacta fijada por manifiesto de fuentes |
| Source manifest SHA-256 | `93D90D9CD7DCFE2C7ACEA81D7430E81FCDCEC4FA033F2FAF0B626FC8B4B35391` |
| ZIP SHA-256 | `29B8F78BD7041A6C82F649799DD034EBBE29352447DC98E3C7712532DCA4C1DE` |
| EXE SHA-256 | `0A5B17D93286A820AAFF710A06379A6F7A59FA31938FE5C5EA52542DEA2EEA2C` |
| Estado | **FROZEN**; read-only |

El commit por sí solo no identifica el release porque el árbol tiene cambios no confirmados.
La identidad reproducible para esta intervención es `commit base + source-manifest.json + hash
del ZIP`. Cualquier cambio de fuente invalida el release y obliga a repetir build, tests,
publish, smoke y hashes.

### 2.3 Publicación real

```text
dotnet publish src/ControlMarcacion.Web/ControlMarcacion.Web.csproj
  -c Release
  -r win-x64
  --self-contained true
  -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:EnableCompressionInSingleFile=true
  -p:DebugType=none
```

- Configuration: `Release`.
- Runtime: `win-x64`.
- Self-contained y single-file: sí.
- Output original: `artifacts/op03a-rot-desc-20261009/release/package/`.
- Views y assemblies Web/Application/Infrastructure/Domain quedan compilados dentro del EXE.
- `wwwroot` se publica separado.
- No se incluyeron `obj`, tests, artifacts anidados, `.git`, `.vs`, logs ni bases SQLite.
- El `appsettings.json` del artefacto se saneó: no contiene la credencial encontrada en el
  archivo de desarrollo. `appsettings.Production.json` es la plantilla sin credencial real.

### 2.4 Build y tests ligados

| Control | Resultado |
|---|---|
| Build Release | 0 errores, 0 warnings |
| Domain | 368/368 |
| Infrastructure | 435/435 |
| Web | 4/4 |
| Total | **807/807** |
| Fallos | 0 |
| Omitidos | 0 |

El primer build dentro del sandbox falló por acceso al compilador/Service Control Manager; no
fue un fallo de código. La ejecución válida fuera de esa restricción terminó limpia. Ambos logs
se conservaron para no ocultar el intento inicial.

### 2.5 Archivos críticos

| Archivo | SHA-256 |
|---|---|
| `ControlMarcacion.Domain.dll` | `DEFA782FB118C97BEC870BBD1D677EC97ECD7B0FB43418CCB6B113A5598371CB` |
| `ControlMarcacion.Application.dll` | `B1CAD3D586DDA90AAF0E0C771F5D92CD7A71F3EE94A62C4DFCB436C23D1EB9D8` |
| `ControlMarcacion.Infrastructure.dll` | `7E98F49C61720221A5C21A711F7BCFA142F4A89EBA4509CAD29B4B09A368F112` |
| `ControlMarcacion.Web.dll` | `96ABC25B5B08A2F101BD6677F3311ED6AF1ABC28C808B0A90CBD146BFCD050DD` |
| `programacion-cuadrilla.js` | `F079281FAA8E598DAC84642CDBFCC9BD56D557ECC655E9F3475AF8849CB731B6` |
| Migración fuente ROT-DESC | `EBBFE00483B726CCCCF645628A7992F4FD2C80B2676DD7800FFBBFAAFAD20DB8` |

Las DLL se registran desde los outputs Release usados por publish; el modelo single-file las
empaqueta dentro de `ControlMarcacion.Web.exe`.

### 2.6 Validación del ZIP y smoke

- extracción limpia: 52 archivos esperados, 0 faltantes, 0 hashes distintos, 0 extras;
- 0 bases de desarrollo y 0 archivos prohibidos;
- EXE y `programacion-cuadrilla.js` presentes;
- el EXE extraído respondió `200 ControlMarcaciones-OK`;
- GET de login respondió 200;
- una ruta protegida anónima respondió 302 hacia login;
- el EXE aplicó sus migraciones embebidas sobre una copia de ocho migraciones: history 15,
  target una vez, integridad `ok`, FK 0;
- el login autenticado no se validó: la credencial de desarrollo no coincide con la copia.
  Las respuestas 200 posteriores eran redirects seguidos hasta login, no sesión autenticada.
  No se creó ni modificó ninguna cuenta.

El primer smoke detectó un argumento de DB sin quoting correcto y creó una base local en una
ruta truncada. No tocó ProgramData. Los tres archivos se movieron a
`release/path-drift-evidence/`, el comando se corrigió y el historial de la ruta exacta se
verificó antes de aceptar el segundo smoke.

Actualización de contención 2026-10-09: la base `CONTROL` y sus sidecars fueron retirados del
árbol después de copia y verificación SHA-256 en la cuarentena privada de PHASE 7A. El
`FINAL_ARTIFACT_MANIFEST.csv` conserva las rutas y hashes como evidencia histórica; los tres
binarios están registrados en `manifest-addendum-path-drift-evidence-20261009.csv/json`.

## 3. Migraciones

### 3.1 Fuente de evidencia productiva

Se consultó exclusivamente con `sqlite3 -readonly` y `PRAGMA query_only=ON`:

```text
C:\ProgramData\ControlMarcaciones\ControlMarcacion.db
SHA-256 = 0E91899A86D8534A1D3770056AC5D7B8BED5C8A6A242CCDB80E3BC552F3A0734
integrity_check = ok
foreign_key_check = 0 filas
```

La ruta coincide con el default del código y con evidencia previa. Sin embargo, en el entorno
actual no se observó tarea `ControlMarcaciones`, directorio `C:\ControlMarcaciones`, proceso Web
activo ni override de máquina. Por eso se clasifica como **base productiva candidata**, no como
ruta efectiva de una instancia confirmada. R4 permanece pendiente.

### 3.2 Última migración observada

```text
20260923140604_RotacionPorCuadrilla
```

El historial contiene ocho migraciones, todas con EF `10.0.10`.

### 3.3 Cadena pendiente exacta

1. `20260924194824_JustificacionUnicaPorDiaYLicenciaSinGoce`
2. `20260924201125_IncidenciasYRegularizaciones`
3. `20260925142617_VersionDelPeriodo`
4. `20260925143248_VigenciaDelHorarioDeTurno`
5. `20260925150116_VigenciaDelAreaDelTrabajador`
6. `20261001212252_MarcacionesProvisionales`
7. `20261007033130_DescansoProgramadoEnRotaciones`

El análisis individual está en `artifacts/op03a-rot-desc-20261009/migration-risk-analysis.md`.
La primera migración puede fallar por justificaciones duplicadas; la consulta previa devolvió
cero filas. Las migraciones 12 y 13 hacen backfill. ROT-DESC reconstruye la tabla SQLite y emite
el warning no transaccional `PRAGMA foreign_keys=0`.

### 3.4 Ensayo de la cadena completa

Se creó mediante SQLite Backup API una copia consistente del archivo candidato en su estado de
ocho migraciones. Sobre esa copia se aplicaron las siete migraciones, en orden.

| Control | Resultado |
|---|---|
| Comando EF | exit 0 |
| History final | 15 migraciones |
| ROT-DESC | exactamente una vez |
| `integrity_check` | `ok` |
| `foreign_key_check` | 0 filas |
| Conteos de tablas preexistentes | idénticos |
| Turnos/versiones | 6/6 |
| Trabajadores con área/asignaciones | 330/330 |
| Tablas provisionales | creadas, 0 filas |
| Warning ROT-DESC | observado y conservado |

Las copias SQLite derivadas de producción se eliminaron después de validar y registrar hashes,
para no conservar PII ni hashes de credenciales en artifacts. Se guardaron solo logs,
snapshots agregados y resultados.

## 4. Piloto

### 4.1 Plantas analizadas

| Id | Planta | Activa | Trabajadores activos | Marcas últimos 30 días | Última marca | Tareos período abierto |
|---:|---|---:|---:|---:|---|---:|
| 1 | PLANTA DETERGENTES LURÍN | Sí | 292 | 0 | — | 8,760 |
| 2 | PLANTA INTRACAN/FEM | Sí | 38 | 0 | — | 1,140 |
| 3 | PLANTA 1 | Sí | 1 | 376 | 2026-09-26 19:19:47 | 30 |
| 4 | Planta Tumi | Sí | 0 | 0 | — | 0 |

La planta 3 tiene una cuadrilla activa, un turno habilitado, dos equipos, cero programaciones y
cero tramos. Su único trabajador activo tiene asignación de marcador, asignación de turno o
cuadrilla y 30 tareos en el período propuesto.

### 4.2 Recomendación técnica

```text
PlantaId: 3
Nombre: PLANTA 1
PeriodoId: 2
Periodo: 2026-09 / “octubre”
Estado: Abierto
Desde: 2026-09-01
Hasta: 2026-09-30
```

Razón: es el menor alcance con datos recientes, tareo suficiente y estructura de cuadrilla ya
existente. Reduce riesgo sin escoger una planta vacía.

No queda aprobada automáticamente. El código `2026-09` y el nombre `octubre` son inconsistentes;
negocio debe confirmar que el período es correcto antes de usarlo. R6 y R7 permanecen pendientes
de esa aprobación.

## 5. Responsables

| Rol | Responsable | Función |
|---|---|---|
| Release | **PENDIENTE DESIGNACIÓN** | entrega/custodia del paquete |
| Backup | **PENDIENTE DESIGNACIÓN** | genera y verifica respaldo |
| Migración | **PENDIENTE DESIGNACIÓN** | ejecuta cadena aprobada |
| DB validation | **PENDIENTE DESIGNACIÓN** | checks SQLite |
| Smoke funcional | **PENDIENTE DESIGNACIÓN** | UI y tareo |
| Negocio | **PENDIENTE DESIGNACIÓN** | valida período y resultados |
| GO/NO-GO | **PENDIENTE DESIGNACIÓN** | autoriza continuidad |
| Rollback | **PENDIENTE DESIGNACIÓN** | ordena/restaura |

Los roles están definidos; faltan personas concretas. R8 está pendiente.

## 6. Backup storage

Opción accesible probada:

```text
C:\Users\stafur\Documents\CONTROL DE MARCACIONES\artifacts\op03a-rot-desc-20261009\backup-storage
```

| Control | Resultado |
|---|---|
| Fuera del directorio activo de aplicación | Sí |
| Volumen | C:; mismo volumen que la DB candidata |
| DB candidata | 4,988,928 bytes |
| Libre al medir | 234,033,336,320 bytes |
| Ratio libre/DB | 46,910.55× |
| Write/read/delete | OK/OK/OK |
| Restore SQLite sintético | `integrity_check=ok`, marcador recuperado |
| Compatible con restore | Sí |

`F:` aparece enumerada pero deniega acceso y no pudo validarse. El candidato local prueba la
mecánica, pero no es un dominio de fallo independiente ni un destino definitivo designado. R9
permanece pendiente; R10 queda técnicamente validado para el destino ensayado.

## 7. Cuenta piloto y alcance

### 7.1 Modelo real

- `CuadrillasController` y `AsistenciaController` admiten `COORDINADOR` y `ADMINISTRADOR`.
- `ProveedorAlcancePlantasHttp` devuelve alcance global para `COORDINADOR`.
- Para un `ADMINISTRADOR`, carga `usuario_planta`; sin asignaciones devuelve conjunto vacío
  (fail-closed).
- `ServicioCalculoTareo.RecalcularPeriodoAsync` filtra trabajadores por las plantas devueltas.
- El período es la unidad de recálculo; el filtro de planta proviene del backend, no de la UI.

Prueba focalizada Release:

```text
El_recalculo_solo_procesa_los_trabajadores_de_las_plantas_del_administrador
Resultado: 1/1 verde
```

La prueba procesa el trabajador de la planta permitida y no crea tareo para el trabajador de la
otra planta. Esto demuestra el control backend.

### 7.2 Estado productivo candidato

La consulta read-only encontró dos usuarios `COORDINADOR`, ambos globales, y cero usuarios
`ADMINISTRADOR`. No existe hoy una cuenta productiva que cumpla el criterio del piloto.

```text
Rol requerido: ADMINISTRADOR
Usuario: PENDIENTE / NO EXISTE
Plantas autorizadas: únicamente PlantaId 3
Backend restringe alcance: SÍ, si rol y usuario_planta están configurados correctamente
Prueba planta piloto: PASA en prueba focalizada de servicio
Prueba otra planta: EXCLUIDA en prueba focalizada de servicio
Prueba autenticada con cuenta real equivalente: NO EJECUTADA
```

No se crearon ni modificaron credenciales. R11 permanece pendiente y requiere una intervención
separada autorizada, seguida de prueba autenticada sobre copia.

## 8. Readiness R1–R11

| Control | Estado | Evidencia/pendiente |
|---|---|---|
| R1 Release nuevo | **PARCIAL** | ZIP congelado y arranca; falta smoke autenticado con credencial autorizada |
| R2 Hash release | **COMPLETO** | ZIP/EXE/componentes/manifiestos |
| R3 Tests ligados al release | **COMPLETO** | 807/807 + source manifest |
| R4 Historial real | **PENDIENTE** | historial leído; ruta efectiva de instancia no confirmada |
| R5 Cadena migraciones | **COMPLETO** | siete migraciones ensayadas; integrity/FK conformes |
| R6 Piloto | **PENDIENTE** | Planta 3 propuesta; falta aprobación de negocio |
| R7 Período | **PENDIENTE** | período 2 abierto; falta resolver código/nombre inconsistente |
| R8 Responsables | **PENDIENTE** | roles definidos, ninguna persona asignada |
| R9 Backup storage | **PENDIENTE** | candidato local probado; falta destino definitivo/independiente |
| R10 Restore | **COMPLETO** | write/read/delete y restore sintético conformes; OP-02B complementa |
| R11 Cuenta restringida | **PENDIENTE** | backend soporta alcance; no existe cuenta ADMINISTRADOR productiva |

## 9. Bloqueantes restantes

1. confirmar desde la instancia/tarea/configuración real que ProgramData es la DB efectiva;
2. ejecutar smoke autenticado del ZIP con una credencial autorizada, sin guardarla en evidencia;
3. obtener aprobación de negocio para PlantaId 3 y resolver la inconsistencia del período 2;
4. asignar personas concretas a los ocho roles operativos;
5. designar y probar el destino final de backup, preferiblemente en otro volumen;
6. crear la cuenta `ADMINISTRADOR` limitada a PlantaId 3 mediante intervención separada y
   probar en copia que la planta piloto pasa y otra planta queda fuera de alcance.

## 10. Evidencia

Raíz: `artifacts/op03a-rot-desc-20261009/`.

Principales archivos:

- `release/RELEASE_MANIFEST.json` y `RELEASE_MANIFEST.sha256`;
- `release/ControlMarcacion_ROT_DESC_20261009_op03a01.zip`;
- `source-manifest.json`;
- `logs/build-release-elevated.log` y `logs/tests-release.log`;
- `migration-inventory.txt`, `migration-pending.csv` y `migration-risk-analysis.md`;
- `production-readonly/migration-history.csv` y `plant-analysis.csv`;
- `chain-rehearsal/migration-chain.log` y `validation-summary.json`;
- `pilot/pilot-recommendation.json`;
- `backup-storage/storage-validation.json`;
- `permissions/backend-scope-test.log`;
- `readiness-checklist.json` y `responsibilities.csv`.

No se guardaron contraseñas. Las copias efímeras derivadas de producción se retiraron al
terminar el ensayo; `ephemeral-database-handling.json` registra sus hashes y disposición.
La evidencia SQLite de path drift también fue retirada posteriormente por PHASE 7A; solo sus
metadatos y hashes permanecen en este árbol.

## 11. Salida final

```text
OP-03A — Production Readiness ROT-DESC

RELEASE

ZIP anterior:
RECHAZADO

Release nuevo:
Ruta: artifacts/op03a-rot-desc-20261009/release/ControlMarcacion_ROT_DESC_20261009_op03a01.zip
Fecha: 2026-10-09 09:07:37 America/Lima
Commit/tag: 92d9177b1fad4a78f639d6ce7dc1dd77b08ef489 + source manifest (árbol dirty)
SHA-256: 29B8F78BD7041A6C82F649799DD034EBBE29352447DC98E3C7712532DCA4C1DE

Build: 0 errores / 0 warnings
Tests: Release
Total: 807
Verdes: 807
Fallos: 0
Omitidos: 0

Archivos críticos:
- Domain: DEFA782FB118C97BEC870BBD1D677EC97ECD7B0FB43418CCB6B113A5598371CB
- Application: B1CAD3D586DDA90AAF0E0C771F5D92CD7A71F3EE94A62C4DFCB436C23D1EB9D8
- Infrastructure: 7E98F49C61720221A5C21A711F7BCFA142F4A89EBA4509CAD29B4B09A368F112
- Web: 96ABC25B5B08A2F101BD6677F3311ED6AF1ABC28C808B0A90CBD146BFCD050DD
- JS: F079281FAA8E598DAC84642CDBFCC9BD56D557ECC655E9F3475AF8849CB731B6

MIGRACIONES

Última migración en producción candidata: 20260923140604_RotacionPorCuadrilla
Fuente de evidencia: C:\ProgramData\ControlMarcaciones\ControlMarcacion.db, sqlite3 -readonly;
ruta efectiva de instancia pendiente de confirmar

Migraciones pendientes:
1. 20260924194824_JustificacionUnicaPorDiaYLicenciaSinGoce
2. 20260924201125_IncidenciasYRegularizaciones
3. 20260925142617_VersionDelPeriodo
4. 20260925143248_VigenciaDelHorarioDeTurno
5. 20260925150116_VigenciaDelAreaDelTrabajador
6. 20261001212252_MarcacionesProvisionales
7. 20261007033130_DescansoProgramadoEnRotaciones

Cadena completa ensayada: SÍ
Resultado: EXIT 0; 15 migraciones finales; conteos preservados
Integrity: ok
FK: 0

PILOTO

Plantas analizadas: 4
Planta propuesta: PLANTA 1
PlantaId: 3
Periodo: Id 2; 2026-09 / “octubre”; Abierto
Desde: 2026-09-01
Hasta: 2026-09-30
Razón: un trabajador, datos recientes, 30 tareos, cuadrilla existente y alcance mínimo

RESPONSABLES

Release: PENDIENTE DESIGNACIÓN
Backup: PENDIENTE DESIGNACIÓN
Migración: PENDIENTE DESIGNACIÓN
DB validation: PENDIENTE DESIGNACIÓN
Smoke: PENDIENTE DESIGNACIÓN
Negocio: PENDIENTE DESIGNACIÓN
GO/NO-GO: PENDIENTE DESIGNACIÓN
Rollback: PENDIENTE DESIGNACIÓN

BACKUP

Destino: candidato local probado; destino final pendiente
Espacio: 234,033,336,320 bytes libres; ratio 46,910.55×
Escritura: OK
Lectura: OK
Restore compatible: SÍ en candidato ensayado

CUENTA PILOTO

Rol: ADMINISTRADOR
Usuario: NO EXISTE / PENDIENTE INTERVENCIÓN AUTORIZADA
Plantas autorizadas: PlantaId 3 exclusivamente
Backend restringe alcance: SÍ
Prueba planta piloto: PASA en test focalizado
Prueba otra planta: EXCLUIDA en test focalizado
Prueba autenticada equivalente: NO EJECUTADA

READINESS

R1 Release nuevo: PARCIAL
R2 Hash: COMPLETO
R3 Tests: COMPLETO
R4 Historial real: PENDIENTE RUTA EFECTIVA
R5 Cadena migraciones: COMPLETO
R6 Piloto: PENDIENTE APROBACIÓN
R7 Periodo: PENDIENTE CONFIRMACIÓN
R8 Responsables: PENDIENTE
R9 Backup storage: PENDIENTE DESTINO FINAL
R10 Restore: COMPLETO
R11 Cuenta restringida: PENDIENTE

OP-03A:
NO VALIDADO

¿LISTO PARA PROGRAMAR VENTANA?:
NO

Bloqueantes restantes:
ruta efectiva; smoke autenticado; aprobación piloto/período; responsables; backup final;
cuenta ADMINISTRADOR limitada y prueba autenticada de alcance

OP-03 EJECUTADO:
NO

MIGRACIÓN PRODUCTIVA:
NO

Siguiente paso:
cerrar los seis bloqueantes en intervenciones autorizadas y repetir R1/R4/R6/R7/R8/R9/R11
antes de calendarizar
```

# Fase 7A — Contención de seguridad y datos

Fecha: 2026-10-09  
Estado del corte 2026-10-09: **P0-DATA-01 CONTENIDO; P0-SEC-01 CONTENIDO LOCALMENTE Y ABIERTO POR ROTACIÓN**  
Actualización 2026-10-19: **P0-SEC-01 CERRADO; véase §11**  
Producción: **pausada**  
OP-03: **no ejecutado**  
Migración productiva: **no**

> El encargo recibido termina literalmente en `# 19. CLASIFICACIÓN`. No se presumieron
> instrucciones posteriores ausentes.

## 1. Resultado ejecutivo

La credencial fue retirada de la configuración fuente y de las salidas normales reconstruidas.
El bootstrap usa fuentes externas ya soportadas por .NET y falla explícitamente cuando la base
está vacía y no recibe ambas credenciales. Se añadieron exclusiones futuras para SQLite y un
guard automatizado sobre `appsettings.json` distribuido.

No se rotó ninguna credencial, no se tocó producción y no se reescribió historia Git.

Tras autorización humana explícita se copiaron 75 archivos SQLite/sidecar/backup y un ZIP
histórico a la cuarentena privada indicada. Los 76 hashes origen/copia coincidieron; después se
retiraron los 76 originales. Los cuatro archivos Git se retiraron del índice actual y quedaron
como eliminaciones preparadas, sin modificar commits anteriores. Por ello:

```text
P0-SEC-01 = CONTENIDO LOCALMENTE; ABIERTO HASTA ROTAR/INVALIDAR LA CREDENCIAL (CORTE HISTÓRICO; SUPERADO EN §11)
P0-DATA-01 = CONTENIDO; EXPOSICIÓN HISTÓRICA DOCUMENTADA
```

## 2. Inventario de credenciales

Todos los valores se muestran enmascarados.

| Archivo/fuente | Tipo | Entorno | Versionado | Generado | Productivo | Vigencia | Severidad | Estado |
|---|---|---|---:|---:|---|---|---|---|
| `src/ControlMarcacion.Web/appsettings.json` | correo `sis****.pe` + clave `Sis****027` | configuración base/desarrollo | Sí | No | Uso productivo no demostrado | Desconocida | Crítica | Valores vacíos en working tree; históricamente expuesto |
| `src/**/bin/**/appsettings.json`, `tests/**/bin/**/appsettings.json` | copia del par fuente | build Debug/Release/RID | No | Sí | No demostrado | Derivada | Alta | Limpieza ejecutada; rebuild sin secreto |
| `despliegue/exe/appsettings*.json` | copia histórica de clave | despliegue local ignorado | No | Sí | No demostrado | Desconocida | Crítica | Saneados en sitio con valores vacíos |
| `despliegue/ControlMarcaciones.zip` | dos entradas con la credencial histórica | artefacto histórico | No, ignorado | Sí | Candidato histórico, no autorizado | Desconocida | Crítica | Retirado y conservado solo en cuarentena como `HISTORICO_COMPROMETIDO_NO_UTILIZAR_PARA_DESPLIEGUE__ControlMarcaciones.zip` |
| `artifacts/op03a-rot-desc-20261009/release/*.zip` | placeholders, no credencial encontrada | release OP-03A | No, ignorado | Sí | No desplegado | No aplica | Informativa | Conservado; no coincide con el secreto expuesto |
| `tests/.../ProgramacionCuadrillaMvcTests.cs` | credencial sintética `co****al` / `We****26` | test aislado | Sí | No | No | Sintética | Informativa | Permitida como fixture; DB temporal |
| User Secrets `controlmarcacion-gca-2026` | ninguna entrada | desarrollo local | No | No | No | No configurado | Informativa | No se creó secreto real |
| Variables de proceso `Seguridad__AdminCorreo` / `Seguridad__AdminClave` | ninguna entrada | proceso auditado | No | No | No determinado | No configurado | Informativa | No se configuró producción |

No se encontraron tokens/API keys adicionales mediante la revisión dirigida de los tipos de
archivo autorizados. Los `PasswordHash` de Identity son hashes persistidos y no se clasificaron
como credenciales en texto claro.

## 3. Fuente y propagación

```text
A. Fuente versionada
   src/ControlMarcacion.Web/appsettings.json

B. Copias bin/obj/RID
   derivadas de A; limpiadas y reconstruidas

C. Artefacto histórico
   retirado del árbol y preservado, renombrado y clasificado en cuarentena

D. Secreto solo local
   ninguno detectado en user-secrets o variables del proceso auditado
```

La corrección se hizo en A. Las copias B dejaron de contener el valor tras limpiar/reconstruir.
Sanear B sin cambiar A no habría contenido la siguiente compilación.

## 4. Configuración segura aplicada

El proyecto Web ya tenía:

```xml
<UserSecretsId>controlmarcacion-gca-2026</UserSecretsId>
```

No se añadió un almacén nuevo. Las fuentes admitidas son:

### Desarrollo

Desde `src/ControlMarcacion.Web`, con valores suministrados por el desarrollador:

```powershell
dotnet user-secrets set "Seguridad:AdminCorreo" "<correo-bootstrap>"
dotnet user-secrets set "Seguridad:AdminClave" "<clave-bootstrap>"
```

Retiro después de crear el primer coordinador:

```powershell
dotnet user-secrets remove "Seguridad:AdminCorreo"
dotnet user-secrets remove "Seguridad:AdminClave"
```

### Producción futura

La cuenta/servicio deberá proporcionar temporalmente:

```text
Seguridad__AdminCorreo
Seguridad__AdminClave
```

No se definieron valores productivos. Una vez creado el coordinador y validado el acceso, ambas
variables deben retirarse de la configuración del servicio.

## 5. Fail-safe

Si la base ya contiene un usuario, el bootstrap sigue sin necesitar secretos externos.

Si la base no contiene usuarios y falta el correo o la clave:

- se registra un mensaje sin PII ni secreto;
- se lanza `CredencialesBootstrapFaltantesException`;
- el arranque no continúa silenciosamente;
- no existe contraseña por defecto, `admin/admin` ni fallback vacío.

Contratos añadidos:

- `Base_sin_usuarios_y_sin_bootstrap_externo_falla_explicito`;
- `Appsettings_distribuido_no_contiene_credenciales_de_bootstrap`.

Los tests MVC existentes arrancan la aplicación completa con configuración en memoria, crean el
coordinador temporal e inician sesión, demostrando que el flujo funciona sin secreto versionado.

## 6. Procedimiento de rotación pendiente

**ROTACIÓN OBLIGATORIA ANTES DE PRODUCCIÓN.** Retirar el valor del archivo no invalida una
credencial que estuvo en Git y en un ZIP.

1. El dueño de seguridad identifica la cuenta creada o potencialmente creada con el bootstrap.
2. Genera una credencial nueva mediante el canal aprobado; no se registra en tickets ni docs.
3. Si la base está vacía, configura temporalmente las dos variables del servicio. Si la cuenta ya
   existe, cambia su clave mediante el flujo administrativo aprobado, no mediante el bootstrap.
4. Reinicia el servicio dentro de una ventana autorizada.
5. Valida login y rol con una cuenta de prueba autorizada.
6. Invalida la clave antigua y cierra sesiones si la política lo exige.
7. Retira ambas variables del entorno cuando ya exista el primer coordinador.
8. Registra fecha, responsable, cuenta enmascarada y resultado; nunca el secreto.
9. Verifica que fuente, build, ZIP nuevo y logs den cero coincidencias.

No se ejecutó ninguno de estos pasos contra producción.

## 7. Inventario SQLite

El detalle por base, tamaño, SHA-256, estado Git, tablas y clasificación está en
`PHASE_07A_SQLITE_INVENTORY.md`.

Resumen:

| Clase | Cantidad | Estado |
|---|---:|---|
| Bases/backups primarios | 27 | 24 con tablas de aplicación y datos; 3 sintéticos sin tablas app |
| Sidecars WAL/SHM | 48 | Asociados a los archivos inventariados; no se abrieron en escritura |
| Total SQLite/sidecar/backup | **75** | 75/75 copiados, verificados y retirados del árbol |
| Archivos SQLite versionados | 4 | Retirados del índice actual; eliminaciones preparadas |
| ZIP histórico con secreto | 1 | Conservado solo en cuarentena, renombrado y marcado como comprometido |

La consulta de clasificación usó URI SQLite `immutable=1`; el número de sidecars antes/después
permaneció 48.

## 8. Exclusión futura

`.gitignore` ahora cubre:

```text
*.db
*.sqlite
*.sqlite3
*.db-wal
*.db-shm
*.db-journal
*.bak
*.backup
```

Esto evita incorporaciones futuras. Los cuatro archivos previamente versionados fueron retirados
del índice mediante `git rm --cached -f` después de verificar la cuarentena; luego se retiraron
sus originales locales. No se borró ni reescribió historia.

Una comprobación posterior de P1-STARTUP-HEALTH detectó dos sidecars regenerados al haber abierto
el backup histórico durante la inspección: `*.db.bak-shm` (32 KiB) y `*.db.bak-wal` (0 bytes).
Ambos estaban libres, fueron copiados a la misma cuarentena, verificados por SHA-256 y retirados
del árbol. La evidencia está separada en
`manifest-addendum-regenerated-sidecars-20261009.csv/json`. Se añadieron además los patrones
generales `*-wal`, `*-shm` y `*-journal`, porque `*.db-shm` no cubre un nombre terminado en
`.db.bak-shm`.

La misma auditoría por firma de archivo, no solo por extensión, detectó la evidencia de path
drift de OP-03A: una base SQLite llamada simplemente `CONTROL` y sus sidecars `CONTROL-shm` y
`CONTROL-wal`. Los tres archivos fueron copiados, verificados y retirados con autorización
específica. Su evidencia está en
`manifest-addendum-path-drift-evidence-20261009.csv/json`. El manifiesto histórico de OP-03A
conserva sus rutas y hashes, pero los binarios ya no están en el árbol operativo.

## 9. Verificación

| Control | Resultado |
|---|---|
| Build Release | 0 errores, 0 warnings |
| Domain.Tests | 368/368 |
| Infrastructure.Tests | 436/436 |
| Web.Tests | 5/5 |
| Total | **809/809, 0 fallos, 0 omitidos** |
| Coincidencias exactas activas en fuente | 0 |
| Coincidencias exactas activas en bin/obj/publish | 0 |
| Coincidencias en texto de artifacts/despliegue extraído | 0 |
| Coincidencias en ZIP histórico dentro del árbol | 0; el ZIP afectado está solo en cuarentena |
| Bases con identificador de la cuenta expuesta | 3; correo solamente, no clave en texto claro |
| Historia Git reescrita | No |
| Producción modificada | No |
| Copia y hashes de cuarentena | 76/76 del lote inicial + 2/2 sidecars regenerados + 3/3 path drift contra sus manifiestos |
| Originales retirados | 76/76 iniciales + 5/5 addenda; 0 bloqueados; 0 sensibles restantes en el árbol |
| Archivos sensibles rastreados actualmente | 0; cuatro eliminaciones preparadas |

## 10. Estado y condición restante

P0-SEC-01 permanece parcialmente abierto y solo podrá cerrarse cuando:

- el dueño confirme la rotación/invalidez de la credencial antigua;
- un escaneo posterior dé cero coincidencias fuera de historia Git aceptada.

P0-DATA-01 queda **CONTENIDO**: destino aprobado, 76/76 hashes iniciales y 5/5 hashes de los
addenda coincidentes, originales retirados, cero archivos sensibles rastreados en el índice
actual y patrones futuros ignorados.
La exposición de commits anteriores permanece documentada. El cierre definitivo puede exigir
una decisión separada sobre historia Git y retención, pero no bloquea la contención local actual.

No usar `git filter-repo`, `filter-branch` ni force-push sin autorización separada.

## 11. Actualización de cierre SEG-006 — 2026-10-19

La condición pendiente de las líneas anteriores quedó **RESUELTA** mediante
`P0-SEG-006-FINAL`. En la instalación local conocida se creó y validó un coordinador sustituto;
la cuenta comprometida quedó inactiva, bloqueada indefinidamente y con `SecurityStamp` rotado.
Un único intento posterior con la credencial histórica fue rechazado. El escaneo posterior dio
cero coincidencias en configuración, ZIP y logs activos.

`P0-SEC-01 = CERRADO` para las instancias activas conocidas. La exposición histórica de Git y
las copias offline continúa contenida y documentada; no se reescribió historia ni se autorizó
reutilizar esos artefactos. Producción, OP-03 y migraciones productivas permanecieron intactos.

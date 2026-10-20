# P1-LOCAL-FIRST-RUN — primer arranque local seguro

Fecha de diagnóstico: 2026-10-13. Revalidación y empaquetado: 2026-10-15. Cierre UAC real:
2026-10-16.

## Causa real demostrada

La instalación afectada estaba en `C:\Program Files\ControlMarcacion` y sus datos en
`C:\ProgramData\ControlMarcacion`. En el primer arranque la migración terminaba, la tabla
`asp_net_users` quedaba vacía y no existían `Seguridad__AdminCorreo` ni
`Seguridad__AdminClave`. El registro de aplicación de Windows conserva el evento .NET Runtime
1026 del 13/10/2026 08:45:09: `CredencialesBootstrapFaltantesException` no controlada en
`InicializadorSeguridad.InicializarAsync`. El proceso terminaba con `-532462766` (`0xE0434352`)
antes de abrir el navegador.

No fue corrupción de SQLite, migración pendiente, working directory, ACL ni puerto. La causa
era representar una configuración inicial pendiente como fallo fatal y exigir credenciales
externas que el instalador no persistía. `CAUSA_REAL = bootstrap externo ausente`.

## Solución y estados

Se adoptó un wizard Web de un solo uso en `/setup`, reutilizando ASP.NET Core Identity y el rol
`COORDINADOR`. El instalador no pide ni empaqueta credenciales. La clave se recibe por POST con
antiforgery, se valida mediante Identity y solo se persiste como hash.

| Estado | `/vivo` | `/estado` | Acción del launcher |
|---|---:|---|---|
| `Starting` | no disponible todavía | iniciando | espera acotada |
| `SetupRequired` | 200 | 503 + `CONFIGURACION_INICIAL_REQUERIDA` | abre `/setup` |
| `Ready` | 200 | 200 + `ControlMarcaciones-OK;LISTO` | abre `/` |
| `Failed` | 200 si Kestrel inició | 503 + `ERROR` | diálogo nativo con ruta de logs |

Después de crear el primer coordinador, `/setup` redirige al login. Un semáforo, transacción
serializable y segunda comprobación dentro de la exclusión impiden que dos POST concurrentes
creen dos coordinadores. El mutex y el descriptor validado permiten que una segunda ejecución
abra la instancia existente tanto en `SetupRequired` como en `Ready`.

## Contratos FIRST-RUN

- `FIRST-RUN-01/02`: DB nueva sin bootstrap mantiene el proceso vivo, anuncia SetupRequired y
  sirve `/setup`.
- `FIRST-RUN-03/04/05`: el alta crea un solo coordinador con hash, pasa a Ready, bloquea la
  reutilización del wizard y sobrevive al reinicio.
- `FIRST-RUN-06`: una migración o preparación fallida queda en Failed y no habilita `/setup`.
- `FIRST-RUN-07`: dos POST concurrentes y una segunda ejecución durante setup conservan un solo
  coordinador y un solo servidor.
- `FIRST-RUN-08`: la clave temporal no aparece en respuesta, configuración, logs ni archivos
  del ensayo portable.
- `FIRST-RUN-09`: Inno Setup concede `users-modify` a `data`, `logs`, `config` y `runtime`; las
  ACL reales instaladas se verificaron con `BUILTIN\Usuarios: Modify`.
- `FIRST-RUN-10`: el ciclo UAC limpio real validó instalación, primer setup, reinicio, instancia
  única, reinstalación, desinstalación con `ProgramData` preservado y reinstalación posterior.

## Evidencia de regresión y publish

La suite final quedó en Domain 419 + Infrastructure 497 + Web 32 = **948/948**, con cero fallos
y cero omitidos. El build Release terminó con cero errores y cero warnings.

El publish `win-x64`, self-contained y por carpeta (no single-file), pasó el smoke real:

```text
puerto preferido 15000 ocupado → fallback 15001
SetupRequired → /setup disponible
segunda ejecución durante SetupRequired → termina, servidor original preservado
setup → LISTO
segunda ejecución durante Ready → termina, servidor original preservado
clave plaintext en archivos temporales → NO
bind LAN → NO
reinicio con la misma DB → LISTO
```

El pipeline fija el nombre local `ControlMarcacion.exe` solo para el proyecto Web, preserva el
instalador anterior antes de reemplazarlo y no incluye SQLite ni backups en el publish.

## Instalador 1.0.2

Ruta: `artifacts/local-installer/installer/ControlMarcacion-Setup.exe`.

SHA-256: `FB354DA1D37D1716323C72D65657D94966B1612B6E2EE325BE15AE147C5A65BB`.

Los instaladores 1.0.0 y 1.0.1 se conservaron en `artifacts/local-installer/history`. La
instalación existente se verificó desde el acceso directo público con su base configurada:
`/vivo=200`, `/estado=LISTO`; una segunda ejecución terminó con código 0 y no reemplazó el PID.

## Ciclo UAC real y preservación

El 16/10/2026 el ciclo elevado terminó `VALIDO`. Antes de alterar la instalación se creó una
copia independiente de `ProgramData` y se comprobó que la base fuente y la copia tenían
6,873,088 bytes y SHA-256
`3448B2B2024F710BBCD0471D86F00F079F425E8CADC7C1FDE4105E7C93DC9B1C`.

El ensayo confirmó:

```text
instalación limpia → OK
primer arranque → CONFIGURACION_INICIAL_REQUERIDA
/setup + coordinador temporal → LISTO
reutilización de /setup → bloqueada
login temporal → OK
segundo arranque → OK
instancia única → OK
reinstalación preservando DB → OK
desinstalación preservando ProgramData y DB → OK
reinstalación posterior reutilizando DB → OK
clave temporal en texto plano → NO
excepciones de bootstrap/no controladas en logs → NO
```

Dos falsos negativos del runbook fueron corregidos sin tocar código productivo: la lectura de
un descriptor residual debía exigir que su PID siguiera activo y la búsqueda de la frase
acentuada de SetupRequired debía ser neutral a la codificación de Windows PowerShell 5.1.

Al finalizar se restauró la base original y se volvió a comprobar el mismo tamaño y SHA-256.
La aplicación 1.0.2 quedó instalada, con accesos directos, y sin proceso activo. Las carpetas de
ensayo temporal fueron eliminadas; las copias de preservación se conservaron como evidencia.

Estado formal: **COMPLETADO**. Producción, migraciones productivas y OP-03 no fueron tocados. No
se creó commit.

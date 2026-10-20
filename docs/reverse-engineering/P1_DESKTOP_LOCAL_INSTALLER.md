# P1-DESKTOP-INSTALLER — instalación local Windows

Estado: **COMPLETADO**. Es una medida temporal; la arquitectura objetivo sigue siendo Web
alojada en servidor.

## Decisión

Se eligió Inno Setup frente a WiX y MSIX. Para esta aplicación interna ofrece el menor coste
de mantenimiento, publica un runtime .NET autocontenido en carpeta, crea accesos directos,
trabaja con Program Files/ProgramData y soporta actualización/desinstalación. WiX añade una
cadena MSI innecesaria y MSIX complica las escrituras y el bootstrap local.

## Contratos

- `LOCAL-HOST-01`: Kestrel enlaza únicamente `127.0.0.1`.
- `LOCAL-HOST-02`: `AddressInUse` real relanza en el puerto consecutivo.
- `LOCAL-HOST-03`: el navegador se abre tras `/vivo`; `/estado` decide entre `/` cuando está
  `LISTO` y `/setup` cuando requiere configuración inicial. Un `ERROR` nunca se presenta listo.
- `LOCAL-HOST-04`: mutex por usuario; el segundo lanzamiento valida PID, `/vivo` y readiness,
  abre el puerto registrado y termina sin iniciar otro servidor.
- `LOCAL-HOST-05`: SQLite vive fuera de binarios, en ProgramData/data.
- `LOCAL-HOST-06`: reinstalar o desinstalar conserva datos y configuración.
- `LOCAL-HOST-07`: no hay bind LAN ni regla de firewall.
- `LOCAL-INSTALL-01`: accesos funcionales en escritorio y menú Inicio.
- `LOCAL-INSTALL-02`: la opción final inicia el exe. Las credenciales del primer coordinador se
  introducen exclusivamente en el wizard Web de un solo uso y no pasan por el instalador.

## Diseño operativo

Binarios: `C:\Program Files\ControlMarcacion`. Datos: `C:\ProgramData\ControlMarcacion`, con
`data`, `logs`, `backups`, `config` y `runtime`. El descriptor `runtime\instancia-<usuario>.json` contiene
PID, puerto y timestamp. El cierre del navegador no detiene el servidor. Para detenerlo se usa
Administrador de tareas o `Stop-Process -Name ControlMarcacion`.

HTTP es aceptable exclusivamente en loopback durante esta fase; no se añaden certificados
autofirmados. No hay autostart. El log reutiliza la salida existente, rota diariamente o al
alcanzar 20 MB (`-001`, `-002`, etc.) y elimina automáticamente solo logs mayores a 30 días.
La política no alcanza backups, SQLite ni la auditoría persistida.

El publish es `win-x64`, self-contained y en carpeta. No se usa single-file porque conservar
Razor, contenido estático y dependencias nativas explícitas simplifica diagnóstico y upgrades.

## Evidencia de cierre

El 2026-10-12 se ejecutó con UAC el ciclo real instalación → ejecución → reinstalación
→ desinstalación → reinstalación. Se verificaron binarios, accesos directos, readiness
en `127.0.0.1:5000`, persistencia de ProgramData y conservación SHA-256 de SQLite antes de
los arranques que debían reutilizarla. Resultado: `VALIDO`.

Build Release: 0 errores/0 warnings. Suite: Domain 398, Infrastructure 441, Web 16; total
855/855, 0 fallos, 0 omitidos. SHA-256 del instalador:
`D55AAF92A64E9E45042D456C5D52DC6E50CA510354250F0B20E082A00ED88B9E`.
No se tocó producción, OP-03, migraciones productivas ni reglas de negocio.

## Actualización 1.0.1 — primer arranque

El 2026-10-13 se retiró la captura de credenciales de Inno Setup y se incorporó `/setup`.
`/estado` diferencia `CONFIGURACION_INICIAL_REQUERIDA`, `LISTO` y `ERROR`; el launcher abre la
ruta correspondiente y los fallos pre-HTTP muestran un diálogo local. El publish aislado validó
setup, login, segunda instancia, fallback de puerto y persistencia SQLite. Suite: 862/862;
build 0/0. Instalador vigente SHA-256:
`2F7DE84936E0F240B9BD462E26BD5C92EA933F5566D41025078FD4B483350D9C`.
El ciclo elevado sobre la instalación real permanece pendiente de la entrada interactiva de la
credencial elegida por el usuario. Detalle: `P1_LOCAL_FIRST_RUN.md`.

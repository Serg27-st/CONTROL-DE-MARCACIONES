# Despliegue en una PC

Instalación del sistema en una sola PC con Windows, arrancando solo con el equipo y
**sin instalar nada**.

El destino es **una PC, no un servidor**: la aplicación escucha en `http://localhost:5000`
y solo se usa desde esa máquina. Nada de esto sirve para publicarla en red — para eso
harían falta certificado, HTTPS y revisar `AllowedHosts`.

## La forma del despliegue

| Pieza | Cómo llega |
|---|---|
| La aplicación | Un `.exe` autocontenido — lleva .NET dentro |
| La base de datos | Un archivo SQLite que la aplicación crea sola |
| Arranque automático | Tarea programada al iniciar Windows |

**No se instala SQL Server, ni .NET, ni IIS, ni herramientas de desarrollo.** Se copia una
carpeta, se edita un archivo de texto y se ejecuta un script. Eso es todo.

La base vive en `C:\ProgramData\ControlMarcaciones\ControlMarcacion.db`, **no** junto al
ejecutable ni en `AppData`:

- `AppData` es **por usuario**. La tarea corre como `SYSTEM` y la persona trabaja en su
  propia sesión: cada uno abriría una base distinta, y el fallo sería silencioso — el
  sistema se vería vacío, no roto.
- Junto al ejecutable exigiría permiso de escritura en la carpeta de programas y la base se
  perdería al reemplazarla en cada actualización.

---

## Antes de empezar: la contraseña publicada

⚠️ La contraseña del coordinador (`sistemas3@gca.pe`) estuvo escrita en
`src/ControlMarcacion.Web/appsettings.json` y **se publicó en GitHub** en el commit
`793baf1`, en las ramas `sergio2` y `sergio3`.

Esa contraseña está quemada. Da acceso total a un sistema con datos personales de
trabajadores (Ley 29733). Antes de desplegar:

1. **Elige una contraseña nueva** — no una variación de la anterior.
2. Si se reutiliza en otro sistema (correo, SAP, la PC), **cámbiala también ahí**.
3. Ponla solo en `appsettings.Production.json` de la PC destino, que no se versiona.

Reescribir la historia de Git y hacer *force-push* borra el rastro público pero **no
descompromete la contraseña**: cualquiera pudo clonarla ya. Cambiarla es lo que importa.

---

## 1. Copiar

Descomprime el paquete en `C:\ControlMarcaciones\`. Debe quedar así:

```
C:\ControlMarcaciones\
    ControlMarcacion.Web.exe          la aplicación entera
    appsettings.json                  valores por defecto
    appsettings.Production.json       la configuración de ESTA PC
    wwwroot\                          hojas de estilo y scripts
    instalar.ps1                      registra el arranque automático
```

## 2. Poner la contraseña

Edita `appsettings.Production.json` y reemplaza `PENDIENTE-PONER-CONTRASENA-NUEVA` por la
contraseña nueva. Requisitos de Identity: **8 caracteres o más, con mayúscula, minúscula y
dígito**.

Ese archivo también fija:

- `Urls`: `http://localhost:5000` — solo esta PC.
- `Despliegue:ExigirHttps`: `false` — sin esto la aplicación redirige a `https://localhost`,
  donde no escucha nadie, y queda inaccesible.
- `Despliegue:RutaBaseDatos`: vacío usa `C:\ProgramData`. Se puede apuntar a otra unidad.
- `Despliegue:AbrirNavegador`: `true` — el ejecutable abre el sistema en el navegador al
  hacerle clic. La tarea programada lo arranca con `false`; ver abajo.

### Un clic abre el sistema

El ejecutable distingue dos situaciones, porque el puerto solo lo puede tomar un proceso:

- **El sistema ya está corriendo** (la tarea lo levantó al encender la PC): abre la pestaña
  y termina, en menos de un segundo. Sin esto, un segundo proceso moriría con
  «address already in use» y el usuario vería una consola con un error.
- **No está corriendo**: lo arranca y abre la pestaña cuando el servidor ya escucha, no
  antes — si no, el navegador llegaría a un puerto todavía cerrado.

La tarea programada arranca con `--Despliegue:AbrirNavegador=false` porque corre como
`SYSTEM`, que no tiene escritorio interactivo: allí abrir un navegador no le muestra nada a
nadie y deja un proceso colgado en la sesión 0.

Por eso el acceso directo del escritorio apunta al **ejecutable** y no a la dirección: así
funciona en los dos casos, incluso el día que la tarea no haya arrancado.

## 3. Instalar

En PowerShell **como Administrador**:

```bash
powershell -ExecutionPolicy Bypass -File C:\ControlMarcaciones\instalar.ps1
```

Registra la tarea, la arranca, espera a que responda y deja un acceso directo en el
escritorio. El primer arranque tarda más: es cuando se crea la base y se cargan los
catálogos.

## 4. Comprobar

1. Abre `http://localhost:5000` — debe salir la pantalla de ingreso.
2. Entra con el correo y la contraseña que pusiste en `Seguridad` dentro de
   `appsettings.Production.json`.
3. **Cambia la contraseña desde la aplicación** y luego vacía `Seguridad:AdminClave` en
   `appsettings.Production.json`. Una vez que el usuario existe ese valor ya no se usa:
   dejarlo escrito solo mantiene una contraseña en claro en el disco.
4. Reinicia la PC y confirma que responde sin que nadie inicie sesión.

---

## Qué trae la base recién creada

Cero datos operativos: sin trabajadores, sin marcaciones, sin tareos, sin solicitudes.

Sí trae los **catálogos de configuración**, que son los que permiten que el sistema
funcione: empresas, plantas, áreas, secciones, puestos, los 106 centros de costo, turnos,
tipos de documento, tipos de ausencia con sus topes, tasas de sobretasa, parámetros legales
(RMV, asignación familiar, movilidad) y los cuatro roles.

Tres valores sembrados conviene corregirlos desde la aplicación antes de empezar:

- Hay un **periodo "Julio 2026"** abierto. Crea el periodo real desde `/Periodos`.
- Los **RUC de las dos empresas** son marcadores de posición (`00000000001`, `00000000002`).
- El **cliente Intradevco** tiene el RUC vacío.

---

## Respaldos

La base es lo único del despliegue que no se puede volver a generar.

⚠️ **No basta con copiar `ControlMarcacion.db`.** SQLite trabaja en modo WAL: mientras la
aplicación corre, los datos recientes están en `ControlMarcacion.db-wal`, que puede ser
mucho más grande que el `.db`. Copiar solo el `.db` da un respaldo que parece correcto y
está incompleto.

Lo correcto es detener la tarea y copiar los tres archivos:

```bash
Stop-ScheduledTask -TaskName ControlMarcaciones; Start-Sleep 5; Copy-Item 'C:\ProgramData\ControlMarcaciones\ControlMarcacion.db*' -Destination 'D:\Respaldos\'; Start-ScheduledTask -TaskName ControlMarcaciones
```

Al detenerse limpiamente, SQLite consolida el WAL dentro del `.db`. Conviene dejarlo como
tarea programada diaria, **a otra unidad**: un respaldo en el mismo disco que la base no
protege del fallo más probable, que es que ese disco muera.

## Actualizar el sistema

1. `Stop-ScheduledTask -TaskName ControlMarcaciones`
2. Respalda la base (arriba).
3. Reemplaza el `.exe` y `wwwroot\`, **conservando** `appsettings.Production.json`.
4. `Start-ScheduledTask -TaskName ControlMarcaciones`

Las migraciones pendientes se aplican solas al arrancar. No hay script de base que correr.

Para regenerar el paquete desde el repositorio:

```bash
dotnet publish src/ControlMarcacion.Web -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o despliegue/exe
```

## Si algo falla

**El sistema no está en el 5000.** Es normal y no hay que hacer nada: si ese puerto lo tiene
otra aplicación —en Windows está muy disputado (UPnP/SSDP dentro de `svchost`, Docker
Desktop, IIS Express)—, el sistema arranca en el primero libre de `PuertosRespaldo` y lo
avisa al iniciarse:

```
El puerto 5000 está ocupado por otra aplicación. El sistema se inicia en http://localhost:5080
```

El acceso directo del escritorio sigue funcionando: apunta al ejecutable, que busca la
instancia en todos los puertos previstos y abre la pestaña en el correcto. Para saber qué
ocupaba el puerto:

```bash
Get-NetTCPConnection -LocalPort 5000 -State Listen | Select-Object OwningProcess,@{n='Proceso';e={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

Solo hay que tocar la configuración si estuvieran ocupados **todos** los de la lista; en ese
caso el arranque lo dice y basta con agregar otro a `Despliegue:PuertosRespaldo`.

**No responde en ningún puerto.** Ejecuta el `.exe` a mano desde una consola: en consola el
error se ve, en la tarea programada no.

**Arranca pero no deja iniciar sesión.** Casi siempre es la contraseña: si no cumple los
requisitos de Identity, el coordinador no se crea y el registro lo dice explícitamente
(«No se pudo crear el coordinador inicial: ...»). El arranque tolera que la base falle —la
aplicación levanta igual y lo registra— para que un problema puntual no obligue a
reiniciarla a mano.

**Dice que no hay usuarios.** `Seguridad:AdminClave` está vacío.

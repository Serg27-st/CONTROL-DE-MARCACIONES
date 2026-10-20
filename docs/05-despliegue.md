# Despliegue en una PC

Instalación del sistema en una sola PC con Windows, arrancando solo con el equipo y
**sin instalar nada**.

El destino es **una PC, no un servidor**: la aplicación escucha solo en
`http://127.0.0.1:5000`
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

- `Despliegue:PuertoPreferido`: `5000` — primer puerto que intenta Kestrel.
- `Urls`: `http://127.0.0.1:5000` — solo esta PC; el modo local fuerza loopback IPv4.
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

1. Abre `http://127.0.0.1:5000` — debe salir la pantalla de ingreso. Si el puerto estaba
   ocupado, usa el puerto mostrado por la aplicación o el acceso directo.
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

### Generar el paquete nuevo (en la PC de desarrollo)

Se arma con un solo script, desde la raíz del repositorio:

```bash
powershell -ExecutionPolicy Bypass -File tools\despliegue\publicar.ps1
```

Requiere el SDK de .NET 10 (`winget install Microsoft.DotNet.SDK.10`) **solo en esa PC**; la
PC destino no necesita nada. El resultado queda en `despliegue\ControlMarcaciones.zip`.

Hace siete cosas y se detiene en la primera que falle:

1. **Comprueba que el `.exe` anterior no esté en uso.** Mientras corre, Windows lo bloquea y
   la publicación muere a mitad de camino.
2. **Corre la suite de pruebas.** Si alguna falla, no publica.
3. **Publica** el `.exe` autocontenido en `despliegue\exe\`.
4. **Conserva `appsettings.Production.json`** —la configuración con la contraseña, que no
   está en el código fuente— y copia la guía (`LEEME-despliegue.md`) y el instalador.
5. **Arranca el `.exe` contra una base temporal** y espera a que responda. Prueba lo mismo
   que vivirá la PC destino el primer día —base nueva, migraciones desde cero, catálogos
   sembrados— sin tocar la base real de esta PC.
6. **Arma el ZIP.**
7. **Lo revisa**: que tenga el `.exe`, las dos configuraciones, el instalador, la guía y
   `wwwroot\`. Al final avisa si lleva la contraseña real o la de relleno.

`-SinPruebas` y `-SinArranque` saltan los pasos 2 y 5; úsalos solo para reempaquetar algo
que ya se probó.

#### Qué hace `dotnet publish`, opción por opción

| Opción | Qué hace | Por qué |
|---|---|---|
| `-c Release` | Compila optimizado | Es la versión que va a producción |
| `-r win-x64` | Genera para Windows de 64 bits | Un paquete autocontenido es de un sistema operativo concreto |
| `--self-contained true` | Mete el runtime de .NET dentro | La PC destino no instala .NET |
| `PublishSingleFile=true` | Junta todo en un solo `.exe` | Un archivo que copiar, no cientos de `.dll` |
| `IncludeNativeLibrariesForSelfExtract=true` | Mete también las librerías nativas (el motor de SQLite) | Sin esto quedarían `.dll` sueltas al lado |
| `EnableCompressionInSingleFile=true` | Comprime el contenido del `.exe` | Hoy queda en ~57 MB |
| `DebugType=none` | No genera `.pdb` | Nadie depura en la PC destino |
| `-o despliegue\exe` | Carpeta de salida | Fuera de git: ahí vive la contraseña |

Lo que **no** entra en el `.exe` queda al lado: `wwwroot\` (estilos y scripts, que el
navegador pide como archivos), los dos `appsettings` (se leen al arrancar y se editan sin
recompilar) y `web.config` (lo genera el SDK para IIS; aquí no se usa).

#### Dónde vive cada pieza

| Pieza | Dónde | ¿Se versiona? |
|---|---|---|
| `publicar.ps1`, `instalar.ps1` | `tools\despliegue\` | Sí |
| Plantilla de configuración | `tools\despliegue\appsettings.Production.plantilla.json` | Sí, con la contraseña de relleno |
| Configuración real | `despliegue\exe\appsettings.Production.json` | **No**: lleva la contraseña |
| `.exe` y ZIP | `despliegue\` | **No**: se regeneran |

Si `despliegue\exe\appsettings.Production.json` no existe —en un clon nuevo del
repositorio—, el script copia la plantilla y avisa. **Sin ese archivo el paquete no
funcionaría**: la aplicación arrancaría con los valores por defecto, redirigiría a HTTPS y
quedaría inaccesible.

#### Sin el script

Son los mismos pasos a mano, desde la raíz del repositorio y con el sistema cerrado:

```bash
dotnet test
```

```bash
dotnet publish src/ControlMarcacion.Web -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o despliegue/exe
```

```bash
Copy-Item docs\05-despliegue.md despliegue\exe\LEEME-despliegue.md; Copy-Item tools\despliegue\instalar.ps1 despliegue\exe\; Compress-Archive -Path despliegue\exe\* -DestinationPath despliegue\ControlMarcaciones.zip -Force
```

⚠️ El ZIP lleva `appsettings.Production.json` tal como esté en `despliegue\exe\`. Si ahí
está la contraseña real, el ZIP también: no lo mandes por correo y bórralo del USB después
de instalar.

## Si algo falla

**El sistema no está en el 5000.** Es normal y no hay que hacer nada: si Kestrel recibe
`AddressInUse` —el 5000 está muy disputado en Windows—, el ejecutable se relanza en 5001,
5002, 5003 y así sucesivamente hasta conseguir el bind real. No se basa en una prueba previa
que abra y cierre el puerto.

```
El puerto 5000 fue rechazado por Kestrel (AddressInUse). Se reintentará en http://127.0.0.1:5001
```

El acceso directo del escritorio sigue funcionando: apunta al ejecutable, que busca la
instancia en todos los puertos previstos y abre la pestaña en el correcto. Para saber qué
ocupaba el puerto:

```bash
Get-NetTCPConnection -LocalPort 5000 -State Listen | Select-Object OwningProcess,@{n='Proceso';e={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

El navegador solo se abre después de que `/vivo` identifica la aplicación y `/estado` responde
HTTP 200 con `ControlMarcaciones-OK;LISTO`. Si readiness devuelve 503, el proceso sigue vivo,
no abre el navegador y muestra el motivo seguro devuelto por `/estado`.

**No responde en ningún puerto.** Ejecuta el `.exe` a mano desde una consola: en consola el
error se ve, en la tarea programada no.

**Arranca pero no deja iniciar sesión.** Casi siempre es la contraseña: si no cumple los
requisitos de Identity, el coordinador no se crea y el registro lo dice explícitamente
(«No se pudo crear el coordinador inicial: ...»). El arranque tolera que la base falle —la
aplicación levanta igual y lo registra— para que un problema puntual no obligue a
reiniciarla a mano.

**Dice que no hay usuarios.** `Seguridad:AdminClave` está vacío.

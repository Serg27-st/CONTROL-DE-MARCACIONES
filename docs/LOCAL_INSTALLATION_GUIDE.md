# Guía de instalación local

1. Ejecute `ControlMarcacion-Setup.exe` como administrador.
2. Mantenga seleccionada **Abrir Control de Marcación**.
3. En una instalación nueva, el navegador abrirá `/setup`. Indique allí nombre, correo y una
   contraseña de al menos ocho caracteres para el coordinador inicial.
4. Después de crear la cuenta, inicie sesión. En los siguientes arranques el navegador abrirá
   el sistema normal; `/setup` ya no permitirá crear otra cuenta inicial.

El instalador no solicita ni almacena credenciales. La contraseña viaja únicamente en el POST
protegido del wizard local y se guarda hasheada mediante ASP.NET Core Identity.

Los accesos **Control de Marcación** del escritorio y menú Inicio apuntan al ejecutable. Si ya
está corriendo, otro doble clic abre la instancia existente; no crea otro servidor.

La base y la configuración están en `C:\ProgramData\ControlMarcacion`; una reinstalación o
desinstalación normal las conserva. Cerrar el navegador no detiene el servidor. Para detenerlo:

```powershell
Stop-Process -Name ControlMarcacion
```

No se crea inicio automático ni regla de firewall. Si el navegador no abre, revise
`C:\ProgramData\ControlMarcacion\logs`; ante un fallo previo al HTTP, la aplicación muestra un
diálogo con acceso a esa carpeta. Confirme también que el proceso aparece en el Administrador
de tareas. La eliminación de datos es siempre manual y separada de la desinstalación.

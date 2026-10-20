using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Infrastructure;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Operaciones;
using ControlMarcacion.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

DialogoInicioWindows.RegistrarManejadorGlobal();
var builder = WebApplication.CreateBuilder(args);

// La instalación local conserva su configuración fuera de Program Files. Se vuelve a
// agregar entorno y línea de comandos para que sigan teniendo precedencia sobre el JSON.
var rutaConfiguracionLocal = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "ControlMarcacion",
    "config",
    "appsettings.Local.json");
builder.Configuration
    .AddJsonFile(rutaConfiguracionLocal, optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
using var registroArchivoLocal = RegistroArchivoLocal.Inicializar(
    builder.Environment,
    builder.Configuration);

// Los TXT del marcador ZKTeco vienen en Windows-1252 / Latin-1, no en UTF-8.
// Sin este registro, Encoding.GetEncoding(1252) lanza excepción al leerlos.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Modo local de un clic. El bind se confirma con Kestrel y, si Windows devuelve
// AddressInUse, el proceso se relanza en el puerto consecutivo. El navegador solo se abre
// después de verificar /vivo y /estado=200 con la marca LISTO.
var hostingLocal = builder.Configuration.GetValue("Despliegue:HostingLocal", true);
var abrirNavegador = builder.Configuration.GetValue("Despliegue:AbrirNavegador", true);
var puertoPreferido = HostingLocalWindows.ResolverPuertoPreferido(builder.Configuration);
var puertoActual = HostingLocalWindows.ResolverPuertoActual(builder.Configuration, puertoPreferido);
var administrarHostingLocal = hostingLocal && puertoActual != 0 && !EF.IsDesignTime;
var urlAplicacion = administrarHostingLocal
    ? HostingLocalWindows.CrearUrl(puertoActual)
    : builder.Configuration["Urls"]?.Split(';')[0] ?? "http://127.0.0.1:5000";
var rutaEstadoInstancia = InstanciaLocalWindows.ResolverRutaEstado(
    builder.Environment,
    builder.Configuration);

if (administrarHostingLocal)
{
    // En modo local la dirección no es configurable: solo loopback IPv4. El modo servidor
    // futuro puede desactivar HostingLocal y recuperar la configuración normal de ASP.NET.
    builder.WebHost.UseUrls(urlAplicacion);
}

builder.Services.AddControllersWithViews(opciones =>
{
    // Exigir sesión iniciada es la regla; CuentaController marca con [AllowAnonymous]
    // las dos acciones que de verdad deben quedar abiertas (ingresar y el aviso de
    // acceso denegado). Sin este filtro global, cada controlador nuevo empezaría
    // abierto por defecto, y olvidar un [Authorize] es un error silencioso.
    opciones.Filters.Add(new AuthorizeFilter());
});

// Fuera de desarrollo la base vive en C:\ProgramData\ControlMarcacion\data, y no:
//
//  - en AppData, que es POR USUARIO. La tarea programada corre como SYSTEM y la persona
//    trabaja en su propia sesión: cada uno abriría una base distinta y el fallo sería
//    silencioso —el sistema se vería vacío, no roto—.
//  - junto al ejecutable, porque exigiría permiso de escritura en la carpeta de programas
//    y la base se perdería al reemplazar esa carpeta en cada actualización.
//
// Despliegue:RutaBaseDatos permite moverla (por ejemplo a una unidad con respaldo) sin
// recompilar.
var cadenaConexion = builder.Configuration.GetConnectionString("BaseDatos");

if (!builder.Environment.IsDevelopment())
{
    var rutaBase = builder.Configuration["Despliegue:RutaBaseDatos"];

    if (string.IsNullOrWhiteSpace(rutaBase))
    {
        rutaBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ControlMarcacion",
            "data",
            "ControlMarcacion.db");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(rutaBase)!);
    cadenaConexion = $"Data Source={rutaBase}";
}

if (string.IsNullOrWhiteSpace(cadenaConexion))
{
    throw new InvalidOperationException(
        "No se configuró la cadena de conexión 'BaseDatos'.");
}

builder.Services.AgregarInfraestructura(cadenaConexion, builder.Configuration);

// Quién firma cada cambio en la bitácora de auditoría. Va DESPUÉS de la infraestructura
// a propósito: esta registración reemplaza al respaldo "sistema" que ella deja puesto
// para poder arrancar sin una capa web delante.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IProveedorUsuarioActual, ProveedorUsuarioActualHttp>();

// Aislamiento por planta del ADMINISTRADOR: lee su rol y sus plantas de la sesión. Sustituye
// al respaldo «sin restricción» que deja puesto la infraestructura. Va aquí, en Web, por la
// misma razón que el proveedor de usuario: es la única capa que conoce HttpContext.
builder.Services.AddScoped<IProveedorAlcancePlantas, ProveedorAlcancePlantasHttp>();

// ASP.NET Core Identity con los cuatro roles del sistema. Se registra en Web y no en
// Infrastructure porque configura el pipeline HTTP (cookie, rutas de login): Identity
// es, por convención de este proyecto, responsabilidad de la capa de presentación.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opciones =>
    {
        // Política de contraseña: se exige longitud y variedad de caracteres, pero sin
        // símbolo obligatorio — se escribe desde un teclado de planta, no siempre cómodo.
        opciones.Password.RequiredLength = 8;
        opciones.Password.RequireNonAlphanumeric = false;
        opciones.Password.RequireUppercase = true;
        opciones.Password.RequireLowercase = true;
        opciones.Password.RequireDigit = true;

        opciones.User.RequireUniqueEmail = true;

        // Cinco intentos fallidos bloquean la cuenta 15 minutos. Protege contra fuerza
        // bruta sin exigir un segundo factor que este despliegue no tiene.
        opciones.Lockout.MaxFailedAccessAttempts = 5;
        opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<MarcacionDbContext>()
    .AddDefaultTokenProviders();

// Cada cuánto se revalida la cookie contra el sello de seguridad del usuario. El valor por
// omisión son 30 minutos; se baja a 2 para que desactivar una cuenta (o forzar su salida)
// corte su sesión activa casi enseguida en vez de dejarla viva media hora. Ver
// ServicioUsuarios.CambiarEstadoAsync, que rota el sello. En SQLite local, revalidar cada 2
// min es una consulta trivial: no hay red ni servidor que se resienta.
builder.Services.Configure<SecurityStampValidatorOptions>(opciones =>
    opciones.ValidationInterval = TimeSpan.FromMinutes(2));

// ¿Se sirve de verdad por HTTPS? Fuera de desarrollo y salvo que el despliegue de una sola PC
// lo apague con Despliegue:ExigirHttps=false (en appsettings.Production.json, por HTTP en
// localhost). Una SOLA decisión gobierna tres cosas que tienen que ir siempre juntas: HSTS, la
// redirección a HTTPS y el flag Secure de la cookie de sesión. Marcar la cookie Secure sobre un
// sitio que sirve HTTP haría que el navegador no la enviara —y no habría sesión—; servir HTTPS
// con la cookie sin Secure la deja viajar en claro. En desarrollo (HTTP en localhost) nunca se
// exige, por lo mismo.
var exigirHttps = !builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("Despliegue:ExigirHttps", true);

builder.Services.ConfigureApplicationCookie(opciones =>
{
    opciones.LoginPath = "/Cuenta/Ingresar";
    opciones.LogoutPath = "/Cuenta/Salir";
    opciones.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
    opciones.SlidingExpiration = true;
    opciones.Cookie.Name = "ControlMarcacion.Auth";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Strict;

    // Secure solo cuando de verdad se sirve HTTPS: en el despliegue de una sola PC por HTTP en
    // localhost la cookie tiene que viajar igual, o no habría sesión. Atado a exigirHttps, la
    // misma clave que gobierna HSTS y la redirección, para que las tres no puedan divergir.
    opciones.Cookie.SecurePolicy = exigirHttps
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});

// La preparación escribe (migraciones + bootstrap); el verificador de readiness solo lee.
// Separarlos evita que una llamada anónima a /estado pueda modificar la base.
builder.Services.AddScoped<IPreparadorAplicacion, PreparadorAplicacion>();
builder.Services.AddScoped<IVerificadorEstadoOperativo, VerificadorEstadoOperativo>();
builder.Services.AddSingleton<EstadoPreparacionAplicacion>();

// Cultura fija es-PE. No se deja al azar de la configuración regional del servidor:
// un servidor en inglés interpretaría "07/08/2026" como 8 de julio en vez de 7 de agosto,
// y ese es un error silencioso que se descubre recién en la planilla.
var culturaPeru = new CultureInfo("es-PE");
builder.Services.Configure<RequestLocalizationOptions>(opciones =>
{
    opciones.DefaultRequestCulture = new RequestCulture(culturaPeru);
    opciones.SupportedCultures = [culturaPeru];
    opciones.SupportedUICultures = [culturaPeru];
});

var app = builder.Build();

// WebApplicationFactory reemplaza Kestrel por TestServer después de configurar el builder.
// Detectarlo aquí mantiene las pruebas HTTP dentro del ciclo estándar y evita lanzar procesos
// de fallback desde un test. No se referencia el paquete TestHost desde producción.
var servidor = app.Services.GetRequiredService<IServer>();
if (string.Equals(
        servidor.GetType().Assembly.GetName().Name,
        "Microsoft.AspNetCore.TestHost",
        StringComparison.Ordinal))
{
    administrarHostingLocal = false;
}

InstanciaLocalWindows? instanciaLocal = null;
if (administrarHostingLocal)
{
    var instanciaComprobacion = InstanciaLocalWindows.IntentarAdquirir();
    if (!instanciaComprobacion.EsPropietaria)
    {
        using (instanciaComprobacion)
        {
        // El mutex, no el archivo, decide que existe otra instancia. El descriptor se valida
        // además con PID y liveness antes de abrir el navegador.
        var registrada = InstanciaLocalWindows.Leer(rutaEstadoInstancia);
        if (registrada is not null && InstanciaLocalWindows.ProcesoExiste(registrada))
        {
            var urlRegistrada = HostingLocalWindows.CrearUrl(registrada.Puerto);
            using var clienteExistente = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var estadoExistente = await HostingLocalWindows
                .EsperarEstadoAsync(
                    clienteExistente,
                    urlRegistrada,
                    TimeSpan.FromSeconds(15),
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (estadoExistente.Vivo && estadoExistente.Listo)
            {
                if (abrirNavegador)
                {
                    AbrirEnNavegador(urlRegistrada);
                }
            }
            else if (estadoExistente.Vivo
                     && estadoExistente.RequiereConfiguracion)
            {
                if (abrirNavegador)
                {
                    AbrirEnNavegador($"{urlRegistrada}/setup");
                }
            }
            else
            {
                MostrarFalloInicio();
            }
        }
        else
        {
            MostrarFalloInicio();
        }

            return;
        }
    }

    // Conserva la propiedad hasta el apagado. El objeto anterior se transfiere aquí para
    // que no libere el mutex al salir del bloque.
    instanciaLocal = instanciaComprobacion;
    InstanciaLocalWindows.RetirarStale(rutaEstadoInstancia);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Inicio/Error");

    // HSTS y la redirección a HTTPS se pueden apagar con Despliegue:ExigirHttps para el
    // despliegue de una sola PC, donde el sistema se sirve por HTTP en localhost y no hay
    // certificado que presentar: con ambos activos el navegador termina en https://localhost,
    // donde no escucha nadie, y la aplicación queda inaccesible.
    //
    // El valor por omisión es true a propósito: un despliegue en red que olvide configurar
    // esta clave queda cifrado, no en claro. Apagarlo tiene que ser una decisión escrita.
    if (exigirHttps)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
}

// Cabeceras de seguridad en TODA respuesta (incluidos los estáticos y la página de error). Se
// fijan al entrar, antes de que nada de la respuesta empiece a escribirse.
//  - nosniff: el navegador respeta el Content-Type declarado y no "adivina" un ejecutable.
//  - CSP: todo el JS y el CSS son locales (sin CDN), así que 'self' alcanza y corta la
//    ejecución de cualquier script inyectado en el HTML. Los style="" de las vistas obligan a
//    'unsafe-inline' SOLO en estilos —riesgo bajo—; el único script en línea (el init del
//    layout) se movió a un archivo para no tener que abrir esa puerta en script-src.
//  - Referrer-Policy no-referrer: las URL internas llevan IDs; no se filtran a ningún externo.
//  - X-Frame-Options: complementa frame-ancestors para navegadores viejos (anti-clickjacking).
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["X-Frame-Options"] = "SAMEORIGIN";
    cabeceras["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; " +
        "frame-ancestors 'self'; form-action 'self'";

    await siguiente().ConfigureAwait(false);
});

// Mientras la preparación crítica no termine, solo se exponen liveness y readiness. Así una
// migración fallida no deja accesibles controladores sobre un esquema parcial. /estado puede
// rehabilitar el tráfico cuando una reparación externa deja la DB compatible.
app.Use(async (contexto, siguiente) =>
{
    var ruta = contexto.Request.Path;
    var esEndpointTecnico = ruta.Equals("/vivo", StringComparison.OrdinalIgnoreCase)
                            || ruta.Equals("/estado", StringComparison.OrdinalIgnoreCase);
    var estadoPreparacion = contexto.RequestServices
        .GetRequiredService<EstadoPreparacionAplicacion>();

    if (!esEndpointTecnico && !estadoPreparacion.PermiteTraficoFuncional)
    {
        contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await contexto.Response.WriteAsJsonAsync(
                new RespuestaEstado(Marcas.Sistema, "ERROR", "INICIALIZACION_NO_COMPLETA"))
            .ConfigureAwait(false);
        return;
    }

    await siguiente().ConfigureAwait(false);
});

app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();

// La autenticación resuelve QUIÉN es (lee la cookie); la autorización resuelve QUÉ puede
// hacer. Van en ese orden y las dos son obligatorias — sin UseAuthentication, el filtro
// [Authorize] global no tendría de dónde sacar el usuario y rechazaría todo.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Index}/{id?}");

// Liveness identifica el proceso aunque la base esté caída. El ejecutable usa esta ruta para
// no levantar una segunda instancia sobre otro puerto cuando la primera todavía vive.
app.MapGet(
        "/vivo",
        (HttpContext contexto) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Text(Marcas.Sistema);
        })
    .AllowAnonymous();

// Readiness es dinámico y read-only: una instancia viva devuelve 503 mientras la base no sea
// compatible con este binario, y vuelve a 200 cuando una reparación externa deja el esquema
// listo. Nunca publica excepciones, rutas, migraciones concretas ni datos personales.
app.MapGet(
        "/estado",
        async (
            HttpContext contexto,
            IVerificadorEstadoOperativo verificador,
            CancellationToken cancelacion) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            var resultado = await verificador.VerificarAsync(cancelacion).ConfigureAwait(false);

            if (resultado.Listo || resultado.RequiereConfiguracion)
            {
                contexto.RequestServices
                    .GetRequiredService<EstadoPreparacionAplicacion>()
                    .MarcarPreparada();
            }

            if (resultado.Listo)
            {
                return Results.Text($"{Marcas.Sistema};LISTO");
            }

            var estado = resultado.RequiereConfiguracion
                ? "CONFIGURACION_INICIAL_REQUERIDA"
                : "ERROR";
            return Results.Json(
                new RespuestaEstado(Marcas.Sistema, estado, resultado.Codigo),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        })
    .AllowAnonymous();

if (!administrarHostingLocal)
{
    // WebApplicationFactory, herramientas EF y un futuro hosting de servidor conservan el
    // ciclo estándar. La lógica local de puertos no invade esos escenarios.
    await PrepararAplicacionAsync(app).ConfigureAwait(false);
    app.Run();
    return;
}

try
{
    // Kestrel intenta el bind real. No existe una prueba previa que abra y cierre el puerto.
    await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
}
catch (Exception excepcion) when (HostingLocalWindows.EsDireccionEnUso(excepcion))
{
    await app.DisposeAsync().ConfigureAwait(false);
    var siguiente = HostingLocalWindows.SiguientePuerto(puertoActual);
    Console.Error.WriteLine(
        $"El puerto {puertoActual} fue rechazado por Kestrel (AddressInUse). " +
        $"Se reintentará en {HostingLocalWindows.CrearUrl(siguiente)}.");

    // El proceso hijo debe poder tomar el mismo mutex antes de intentar el puerto siguiente.
    instanciaLocal?.LiberarParaRelanzar();
    if (!HostingLocalWindows.RelanzarEnPuerto(args, siguiente))
    {
        Console.Error.WriteLine("No se pudo relanzar ControlMarcacion en el puerto siguiente.");
        Environment.ExitCode = 1;
    }

    return;
}

try
{
    if (puertoActual != puertoPreferido)
    {
        Console.WriteLine(
            $"El puerto preferido {puertoPreferido} estaba ocupado. " +
            $"ControlMarcacion escucha en {urlAplicacion}.");
    }

    try
    {
        // Permite que un segundo doble clic encuentre la instancia aunque use fallback.
        InstanciaLocalWindows.Registrar(rutaEstadoInstancia, puertoActual);
    }
    catch (IOException excepcion)
    {
        Console.Error.WriteLine($"No se pudo registrar el puerto activo: {excepcion.Message}");
    }
    catch (UnauthorizedAccessException excepcion)
    {
        Console.Error.WriteLine($"No se pudo registrar el puerto activo: {excepcion.Message}");
    }

    // La preparación se ejecuta después del bind: /vivo puede distinguir proceso iniciado de
    // sistema operativamente listo. Un fallo deja /estado en 503 y nunca abre el navegador.
    await PrepararAplicacionAsync(app).ConfigureAwait(false);

    using var clienteInicio = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var estadoInicio = await HostingLocalWindows
        .EsperarEstadoAsync(
            clienteInicio,
            urlAplicacion,
            TimeSpan.FromSeconds(15),
            CancellationToken.None)
        .ConfigureAwait(false);

    if (!estadoInicio.Vivo)
    {
        Console.Error.WriteLine(
            $"ControlMarcacion inició en {urlAplicacion}, pero /vivo no pudo verificarse. " +
            estadoInicio.Detalle);
        MostrarFalloInicio();
    }
    else if (!estadoInicio.Listo)
    {
        if (estadoInicio.RequiereConfiguracion)
        {
            Console.WriteLine(
                $"ControlMarcacion requiere configuración inicial en {urlAplicacion}/setup.");
            if (abrirNavegador)
            {
                AbrirEnNavegador($"{urlAplicacion}/setup");
            }
        }
        else
        {
            Console.Error.WriteLine(
                $"ControlMarcacion está vivo en {urlAplicacion}, pero tiene un error operativo. " +
                estadoInicio.Detalle);
            MostrarFalloInicio();
        }
    }
    else
    {
        Console.WriteLine($"ControlMarcacion está operativamente LISTO en {urlAplicacion}.");
        if (abrirNavegador)
        {
            AbrirEnNavegador(urlAplicacion);
        }
    }

    await app.WaitForShutdownAsync(CancellationToken.None).ConfigureAwait(false);
}
finally
{
    InstanciaLocalWindows.RetirarSiPerteneceAlProceso(rutaEstadoInstancia);
    instanciaLocal?.Dispose();
    await app.DisposeAsync().ConfigureAwait(false);
}

static async Task PrepararAplicacionAsync(WebApplication aplicacion)
{
    using var alcance = aplicacion.Services.CreateScope();
    var estadoPreparacion = alcance.ServiceProvider
        .GetRequiredService<EstadoPreparacionAplicacion>();
    var registro = alcance.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("ControlMarcacion.Arranque");

    try
    {
        var preparador = alcance.ServiceProvider.GetRequiredService<IPreparadorAplicacion>();
        await preparador.PrepararAsync(CancellationToken.None).ConfigureAwait(false);
        estadoPreparacion.MarcarPreparada();
    }
    catch (Exception excepcion)
    {
        estadoPreparacion.MarcarFallida();
        RegistroArranque.BaseNoDisponible(registro, excepcion);
    }
}

/// <summary>
/// Abre la dirección en el navegador predeterminado del usuario.
/// </summary>
/// <remarks>
/// Un fallo aquí no puede tumbar el arranque: que no haya navegador asociado, o que se
/// ejecute sin escritorio interactivo, es un inconveniente — el sistema sigue accesible
/// escribiendo la dirección a mano.
/// </remarks>
static void AbrirEnNavegador(string url)
{
    try
    {
        // UseShellExecute es obligatorio para que Windows resuelva el navegador asociado:
        // sin él, Process.Start intentaría ejecutar la URL como si fuera un programa.
        using var proceso = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Win32Exception)
    {
    }
    catch (InvalidOperationException)
    {
    }
    catch (PlatformNotSupportedException)
    {
    }
}

static void MostrarFalloInicio()
{
    DialogoInicioWindows.MostrarFallo(DialogoInicioWindows.RutaLogsPredeterminada);
}

/// <summary>
/// Mensajes de registro del arranque.
/// </summary>
/// <remarks>
/// Va como delegado generado y no como <c>logger.LogError(...)</c> directo porque el
/// proyecto trata las advertencias como errores y CA1848 exige esta forma: evita
/// construir el mensaje —y sus asignaciones— cuando el nivel está desactivado.
/// </remarks>
internal static partial class RegistroArranque
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "No se pudo preparar la base de datos al arrancar (migraciones o usuario " +
                  "inicial). El proceso queda vivo, pero /estado responderá 503 hasta que la " +
                  "base esté disponible y su esquema sea compatible. Revise la carpeta, los " +
                  "permisos y las migraciones antes de habilitar tráfico.")]
    public static partial void BaseNoDisponible(ILogger logger, Exception excepcion);
}

/// <summary>
/// Marca con la que el sistema se identifica ante sí mismo.
/// </summary>
/// <remarks>
/// Constante y no un texto suelto en dos sitios: el endpoint que la publica y la
/// comprobación que la busca tienen que decir exactamente lo mismo, y si se separan el
/// ejecutable dejaría de reconocer a su propia instancia.
/// </remarks>
internal static class Marcas
{
    public const string Sistema = "ControlMarcaciones-OK";
}

internal sealed record RespuestaEstado(string Sistema, string Estado, string Codigo);

// Punto de entrada visible para las pruebas HTTP de extremo a extremo con
// WebApplicationFactory. No altera el arranque normal de la aplicación.
public partial class Program;

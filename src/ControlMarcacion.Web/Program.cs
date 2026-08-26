using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Infrastructure;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Los TXT del marcador ZKTeco vienen en Windows-1252 / Latin-1, no en UTF-8.
// Sin este registro, Encoding.GetEncoding(1252) lanza excepción al leerlos.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// ---------------------------------------------------------------------------------
// Modo "un clic": el ejecutable abre el sistema en el navegador.
//
// El puerto solo lo puede tomar un proceso, así que antes de arrancar hay que averiguar
// quién lo tiene. Son TRES situaciones y confundirlas se nota:
//
//  - Libre: arrancamos y abrimos la pestaña cuando Kestrel ya escucha, no antes, o el
//    navegador llegaría a un puerto todavía cerrado.
//  - Lo tiene NUESTRO sistema (la tarea lo levantó al encender la PC): abrimos la pestaña
//    y salimos. Para quien hizo clic, el efecto es el que espera.
//  - Lo tiene OTRA aplicación: hay que decirlo. El 5000 está muy disputado en Windows
//    (UPnP/SSDP dentro de svchost, Docker Desktop, IIS Express).
//
// ⚠️ No basta con preguntar «¿hay algo escuchando?». Esa comprobación daba por bueno
// cualquier proceso ajeno: el ejecutable abría la pestaña, se cerraba sin arrancar nada y
// el usuario veía una pestaña en blanco sin ninguna explicación. Por eso se pide /estado
// y se comprueba la respuesta: escuchar en el puerto no es ser el sistema.
//
// La tarea programada arranca con --Despliegue:AbrirNavegador=false porque corre como
// SYSTEM, que no tiene escritorio interactivo: allí abrir un navegador no muestra nada y
// deja un proceso colgado en la sesión 0.
// ---------------------------------------------------------------------------------
var urlConfigurada = builder.Configuration["Urls"]?.Split(';')[0] ?? "http://localhost:5000";
var abrirNavegador = builder.Configuration.GetValue("Despliegue:AbrirNavegador", true);

var uriConfigurada = Uri.TryCreate(urlConfigurada, UriKind.Absolute, out var u)
    ? u
    : new Uri("http://localhost:5000");

// El puerto preferido primero y los de respaldo después, sin repetir. Configurables porque
// qué puerto está libre depende de qué más tenga instalado cada PC, y eso no se sabe desde
// aquí.
var candidatos = new List<int> { uriConfigurada.Port };
candidatos.AddRange(
    (builder.Configuration.GetSection("Despliegue:PuertosRespaldo").Get<int[]>()
        ?? [5080, 5090, 8080, 8090])
    .Where(p => !candidatos.Contains(p)));

string UrlDePuerto(int puerto) =>
    $"{uriConfigurada.Scheme}://{uriConfigurada.Host}:{puerto}";

// ---------------------------------------------------------------------------------
// PASO 1: ¿ya hay una instancia nuestra corriendo, en el puerto que sea?
//
// Se barren TODOS los candidatos y no solo el preferido. Si el sistema arrancó en 5080
// porque el 5000 estaba ocupado, y más tarde el 5000 queda libre, mirar solo el preferido
// haría que un segundo clic levantara una SEGUNDA instancia: dos procesos escribiendo la
// misma base y el usuario viendo uno u otro según el puerto que abriera.
// ---------------------------------------------------------------------------------
var puertoEnUso = candidatos.FirstOrDefault(p => EsNuestroSistema(uriConfigurada.Host, p));

if (puertoEnUso != 0)
{
    if (abrirNavegador)
    {
        AbrirEnNavegador(UrlDePuerto(puertoEnUso));
    }

    return;
}

// ---------------------------------------------------------------------------------
// PASO 2: elegir el primer puerto libre.
//
// Se comprueba intentando escuchar de verdad y no consultando una lista del sistema: es la
// única forma de saber que Kestrel va a poder tomarlo. Queda una ventana mínima entre esta
// prueba y el arranque real; en una PC de escritorio es aceptable.
// ---------------------------------------------------------------------------------
var puertoElegido = candidatos.FirstOrDefault(p => SePuedeEscuchar(uriConfigurada.Host, p));

if (puertoElegido == 0)
{
    Console.Error.WriteLine(
        "No se pudo iniciar: todos los puertos previstos están ocupados por otras " +
        $"aplicaciones ({string.Join(", ", candidatos)}).");
    Console.Error.WriteLine(
        "Agregue uno libre en appsettings.Production.json, en Despliegue:PuertosRespaldo.");
    Console.Error.WriteLine(
        "Para ver qué los ocupa: Get-NetTCPConnection -LocalPort " +
        $"{candidatos[0]} -State Listen");
    Environment.ExitCode = 1;
    return;
}

var urlAplicacion = UrlDePuerto(puertoElegido);
builder.WebHost.UseUrls(urlAplicacion);

if (puertoElegido != uriConfigurada.Port)
{
    // Que el cambio se vea: el usuario abrió esperando el puerto de siempre, y sus enlaces
    // guardados apuntan ahí.
    Console.WriteLine(
        $"El puerto {uriConfigurada.Port} está ocupado por otra aplicación. " +
        $"El sistema se inicia en {urlAplicacion}");
}

builder.Services.AddControllersWithViews(opciones =>
{
    // Exigir sesión iniciada es la regla; CuentaController marca con [AllowAnonymous]
    // las dos acciones que de verdad deben quedar abiertas (ingresar y el aviso de
    // acceso denegado). Sin este filtro global, cada controlador nuevo empezaría
    // abierto por defecto, y olvidar un [Authorize] es un error silencioso.
    opciones.Filters.Add(new AuthorizeFilter());
});

// Fuera de desarrollo la base vive en C:\ProgramData\ControlMarcaciones, y no:
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
            "ControlMarcaciones",
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

// Marca de identidad del sistema, para que el propio ejecutable pueda distinguir «el
// puerto lo tengo yo» de «el puerto lo tiene otra aplicación». Va anónimo a propósito: se
// consulta antes de que exista ninguna sesión y no revela nada — ni versión, ni rutas, ni
// si hay usuarios.
app.MapGet("/estado", () => Marcas.Sistema).AllowAnonymous();

// El primer coordinador se crea aquí, no en una migración: una contraseña es un
// credencial y este archivo no se versiona con datos. Ver InicializadorSeguridad.
//
// Va dentro de un try porque es lo único del arranque que toca la base de datos: si SQL
// Server no responde en ese instante, la excepción tumbaba el proceso entero y la
// aplicación no llegaba a escuchar. Eso convierte un corte de red de treinta segundos en
// una caída que necesita que alguien la reinicie a mano, justo cuando nadie está mirando.
//
// Arrancar igual es lo correcto: sin base tampoco se puede iniciar sesión, pero al menos
// el sitio responde, la pantalla de error explica qué pasa y el sistema se recupera solo
// en cuanto la base vuelve. Si la tabla de usuarios estaba vacía, el coordinador se creará
// en el siguiente arranque, que es exactamente lo que ya hacía este inicializador.
using (var alcance = app.Services.CreateScope())
{
    var registro = alcance.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("ControlMarcacion.Arranque");

    try
    {
        // Crea el archivo de base si no existe y aplica las migraciones pendientes. Es lo
        // que convierte la instalación en "copiar el ejecutable y abrirlo": no hay script
        // que correr ni motor que preparar. En el primer arranque entran también los
        // catálogos de DatosSemilla, que viajan como datos dentro de las migraciones.
        //
        // Aplicar migraciones al arrancar es discutible en un sistema que calcula planilla
        // —normalmente se hace en un paso aparte y revisado—, pero aquí el despliegue es
        // una PC sin operador técnico: un paso manual que alguien olvida deja el sistema
        // sin arrancar, y el reemplazo del ejecutable ya es la decisión consciente.
        var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
        await contexto.Database.MigrateAsync().ConfigureAwait(false);

        await InicializadorSeguridad.InicializarAsync(alcance.ServiceProvider, app.Configuration)
            .ConfigureAwait(false);
    }
    catch (Exception excepcion)
    {
        // Se registra el tipo y el mensaje de la excepción, nunca la cadena de conexión:
        // lleva el servidor y, según el despliegue, usuario y contraseña.
        RegistroArranque.BaseNoDisponible(registro, excepcion);
    }
}

// Se abre cuando el servidor ya está escuchando, no antes: ApplicationStarted se dispara
// después de que Kestrel toma el puerto, así que el navegador nunca llega a una puerta
// todavía cerrada.
if (abrirNavegador)
{
    app.Lifetime.ApplicationStarted.Register(() => AbrirEnNavegador(urlAplicacion));
}

app.Run();

/// <summary>
/// ¿Hay una instancia <b>del sistema</b> atendiendo en ese puerto?
/// </summary>
/// <remarks>
/// Se resuelve por el puerto y no con un mutex con nombre: la tarea programada corre como
/// SYSTEM y la persona en su propia sesión, y un mutex creado por SYSTEM no siempre es
/// visible para un usuario normal. El puerto es el recurso que de verdad está en disputa.
///
/// ⚠️ No basta con que algo escuche: hay que comprobar que ese algo <b>es el sistema</b>,
/// pidiéndole /estado y buscando su marca. Preguntar solo si el puerto estaba ocupado hacía
/// que cualquier proceso ajeno en el 5000 se tomara por nuestro, y el ejecutable abría la
/// pestaña y se cerraba sin arrancar nada.
/// </remarks>
static bool EsNuestroSistema(string host, int puerto)
{
    var destino = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        ? "127.0.0.1"
        : host;

    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var respuesta = http
            .GetStringAsync($"http://{destino}:{puerto}/estado")
            .GetAwaiter()
            .GetResult();

        return respuesta.Contains(Marcas.Sistema, StringComparison.Ordinal);
    }
    catch (HttpRequestException)
    {
        return false;
    }
    catch (TaskCanceledException)
    {
        return false;
    }
    catch (AggregateException)
    {
        return false;
    }
}

/// <summary>
/// ¿Se puede tomar ese puerto para escuchar?
/// </summary>
/// <remarks>
/// Se comprueba intentando escuchar de verdad, y no consultando la lista de conexiones del
/// sistema: es la única forma de saber que Kestrel va a poder tomarlo — un puerto puede
/// figurar libre y estar reservado por Windows (<c>netsh interface portproxy</c>, rangos
/// excluidos) y el arranque fallaría igual.
/// </remarks>
static bool SePuedeEscuchar(string host, int puerto)
{
    var direccion = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        ? IPAddress.Loopback
        : IPAddress.TryParse(host, out var analizada) ? analizada : IPAddress.Any;

    TcpListener? oyente = null;

    try
    {
        oyente = new TcpListener(direccion, puerto);
        oyente.Start();
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
    finally
    {
        oyente?.Stop();
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
                  "inicial). La aplicación se inicia igual, pero no se podrá iniciar sesión " +
                  "hasta que la base esté disponible. Revise que la carpeta de datos exista " +
                  "y que el usuario que ejecuta la aplicación pueda escribir en ella.")]
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

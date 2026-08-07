using System.Globalization;
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

var builder = WebApplication.CreateBuilder(args);

// Los TXT del marcador ZKTeco vienen en Windows-1252 / Latin-1, no en UTF-8.
// Sin este registro, Encoding.GetEncoding(1252) lanza excepción al leerlos.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

builder.Services.AddControllersWithViews(opciones =>
{
    // Exigir sesión iniciada es la regla; CuentaController marca con [AllowAnonymous]
    // las dos acciones que de verdad deben quedar abiertas (ingresar y el aviso de
    // acceso denegado). Sin este filtro global, cada controlador nuevo empezaría
    // abierto por defecto, y olvidar un [Authorize] es un error silencioso.
    opciones.Filters.Add(new AuthorizeFilter());
});

var cadenaConexion = builder.Configuration.GetConnectionString("BaseDatos")
    ?? throw new InvalidOperationException(
        "No se configuró la cadena de conexión 'BaseDatos'. " +
        "En desarrollo usa user-secrets; en producción, la variable de entorno " +
        "ConnectionStrings__BaseDatos.");

builder.Services.AgregarInfraestructura(cadenaConexion, builder.Configuration);

// Quién firma cada cambio en la bitácora de auditoría. Va DESPUÉS de la infraestructura
// a propósito: esta registración reemplaza al respaldo "sistema" que ella deja puesto
// para poder arrancar sin una capa web delante.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IProveedorUsuarioActual, ProveedorUsuarioActualHttp>();

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
    app.UseHsts();

    // Solo fuera de desarrollo: en local se trabaja sobre HTTP plano para no depender
    // del certificado de desarrollo. En producción, HTTPS es obligatorio.
    app.UseHttpsRedirection();
}
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

// El primer coordinador se crea aquí, no en una migración: una contraseña es un
// credencial y este archivo no se versiona con datos. Ver InicializadorSeguridad.
using (var alcance = app.Services.CreateScope())
{
    await InicializadorSeguridad.InicializarAsync(alcance.ServiceProvider, app.Configuration)
        .ConfigureAwait(false);
}

app.Run();

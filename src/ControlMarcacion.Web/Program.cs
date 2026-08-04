using System.Globalization;
using System.Text;
using ControlMarcacion.Infrastructure;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Los TXT del marcador ZKTeco vienen en Windows-1252 / Latin-1, no en UTF-8.
// Sin este registro, Encoding.GetEncoding(1252) lanza excepción al leerlos.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

builder.Services.AddControllersWithViews();

var cadenaConexion = builder.Configuration.GetConnectionString("BaseDatos")
    ?? throw new InvalidOperationException(
        "No se configuró la cadena de conexión 'BaseDatos'. " +
        "En desarrollo usa user-secrets; en producción, la variable de entorno " +
        "ConnectionStrings__BaseDatos.");

builder.Services.AgregarInfraestructura(cadenaConexion, builder.Configuration);

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Index}/{id?}");

app.Run();

// TODO fase 1: registrar MarcacionDbContext y los repositorios.
// TODO fase 2: registrar ASP.NET Core Identity con los roles
//              ADMINISTRADOR, RRHH, SUPERVISOR y CONSULTA.

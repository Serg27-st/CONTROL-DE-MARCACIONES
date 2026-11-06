using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Auditoria;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Inicio;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Justificaciones;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.MarcacionesProvisionales;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Application.Programacion;
using ControlMarcacion.Infrastructure.Catalogos;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Reportes;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Infrastructure.Programacion;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlMarcacion.Infrastructure;

/// <summary>
/// Registro de la infraestructura en el contenedor de dependencias.
/// </summary>
/// <remarks>
/// Centralizarlo aquí evita que la capa Web tenga que conocer las clases concretas de
/// persistencia: solo llama a este método y sigue dependiendo únicamente de interfaces.
/// </remarks>
public static class InyeccionDependencias
{
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios,
        string cadenaConexion,
        IConfiguration? configuracion = null)
    {
        // Respaldo: si la capa que hospeda no registra el suyo, la auditoría anota
        // "sistema" en vez de impedir el arranque. Web registra después el que lee la
        // identidad de la petición HTTP, y esa registración gana sobre esta.
        servicios.TryAddScoped<IProveedorUsuarioActual, ProveedorUsuarioSistema>();
        servicios.TryAddScoped<IContextoAutorizacionVinculacionBiometrica, ContextoVinculacionBiometricaNoConfigurado>();
        servicios.TryAddScoped<IProveedorEvidenciaIdentidad, ProveedorEvidenciaIdentidadNoConfigurado>();

        // Aislamiento por planta: mismo patrón que el proveedor de usuario. El respaldo ve
        // TODAS las plantas (sin restricción), para que la siembra y las herramientas de EF
        // no queden sin datos; Web registra después el que lee el rol y las plantas de la
        // sesión, y esa registración gana sobre esta.
        servicios.TryAddScoped<IProveedorAlcancePlantas, AlcancePlantasSistema>();

        // El interceptor de auditoría es Scoped porque acompaña al DbContext durante toda
        // la petición: guarda entre SavingChanges y SavedChanges los cambios que leyó del
        // ChangeTracker. Un singleton compartiría ese estado entre peticiones simultáneas
        // y mezclaría los cambios de un usuario con los de otro.
        servicios.AddScoped<InterceptorAuditoria>();

        // SQLite: la base es un archivo local, no un servidor. Sin EnableRetryOnFailure,
        // que era la respuesta a los fallos transitorios de red de SQL Server: aquí no hay
        // red que falle. Los bloques que envuelven su trabajo en
        // Database.CreateExecutionStrategy().ExecuteAsync(...) siguen siendo correctos —
        // reciben la estrategia por omisión, que ejecuta una sola vez.
        servicios.AddDbContext<MarcacionDbContext>((proveedor, opciones) =>
            opciones
                .UseSqlite(cadenaConexion)
                .AddInterceptors(proveedor.GetRequiredService<InterceptorAuditoria>()));

        // Plantilla de lectura del marcador. Si no se pasa configuración, quedan los
        // valores por defecto de la clase, que son los del archivo real del G3.
        if (configuracion is not null)
        {
            servicios.Configure<OpcionesMarcador>(configuracion.GetSection(OpcionesMarcador.Seccion));
            servicios.Configure<OpcionesLectorBkTransaction>(
                configuracion.GetSection(OpcionesLectorBkTransaction.Seccion));
        }
        else
        {
            servicios.Configure<OpcionesMarcador>(_ => { });
            servicios.Configure<OpcionesLectorBkTransaction>(_ => { });
        }

        servicios.AddScoped<ITrabajadorRepository, TrabajadorRepository>();
servicios.AddScoped<IConsultaTrabajadores, ConsultaTrabajadores>();
        servicios.AddScoped<IConsultaCatalogos, ConsultaCatalogos>();
        servicios.AddScoped<IServicioCatalogos, ServicioCatalogos>();
        servicios.AddScoped<IServicioTurnos, ServicioTurnos>();
        servicios.AddScoped<IServicioCuadrillas, ServicioCuadrillas>();
        servicios.AddScoped<IServicioMarcadores, ServicioMarcadores>();
        servicios.AddScoped<IServicioEquipos, ServicioEquipos>();
        servicios.AddScoped<IConsultaAuditoria, ConsultaAuditoria>();
        servicios.AddScoped<IServicioTiposAusencia, ServicioTiposAusencia>();
        servicios.AddScoped<IServicioCalculoTareo, ServicioCalculoTareo>();
        servicios.AddScoped<IValidadorPreparacionTareo, ValidadorPreparacionTareo>();
        servicios.AddScoped<IConciliadorProgramacion, ConciliadorProgramacion>();
        servicios.AddScoped<IImportadorProgramacionLaboral, ImportadorProgramacionLaboral>();
        servicios.AddScoped<IServicioProgramacionMasiva, ServicioProgramacionMasiva>();
        servicios.AddScoped<IServicioDescansosSustitutorios, ServicioDescansosSustitutorios>();
        servicios.AddScoped<IServicioPeriodos, ServicioPeriodos>();
        servicios.AddScoped<IServicioSolicitudes, ServicioSolicitudes>();
        servicios.AddScoped<IServicioJustificaciones, ServicioJustificaciones>();
        servicios.AddScoped<IServicioReportes, ServicioReportes>();
        servicios.AddScoped<IExportadorReportesExcel, ExportadorReportesExcel>();
        servicios.AddScoped<IServicioReintegrosTareo, ServicioReintegrosTareo>();
        servicios.AddScoped<IServicioUsuarios, ServicioUsuarios>();

        // Ingesta: los dos lectores concretos y, delante de ellos, el que decide cuál toca
        // según el contenido. Solo el automático se expone como IFuenteDeMarcaciones, para que
        // una futura conexión directa al equipo no obligue a tocar el servicio de importación.
        servicios.AddScoped<LectorTxtZkteco>();
        servicios.AddScoped<LectorRespaldoZkteco>();
        servicios.AddScoped<LectorRespaldoZktecoSdk>();
        servicios.AddScoped<ComparadorLecturasBkTransaction>();
        servicios.AddSingleton<ILectorUsuariosZkteco, LectorUsuariosZkteco>();
        servicios.AddScoped<IServicioConciliacionBiometrica, ServicioConciliacionBiometrica>();
        servicios.AddScoped<IServicioConfirmacionVinculacionBiometrica, ServicioConfirmacionVinculacionBiometrica>();
        servicios.AddScoped<IFuenteDeMarcaciones, LectorMarcadorAutomatico>();
        servicios.AddScoped<IServicioImportacion, ServicioImportacion>();

        // Módulo provisional — eliminar estas líneas cuando el motor principal cubra este flujo.
        servicios.AddScoped<IServicioImportacionProvisional, ServicioImportacionProvisional>();
        servicios.AddScoped<IServicioConsultaMarcacionProvisional, ServicioConsultaMarcacionProvisional>();
        servicios.AddScoped<IExportadorMarcacionesProvisionales, ExportadorMarcacionesProvisionales>();

        // Orquestador de la portada: vive en Application porque solo compone servicios de
        // caso de uso, sin tocar el DbContext.
        servicios.AddScoped<IServicioPanelInicio, ServicioPanelInicio>();

        servicios.AddScoped<ServicioTrabajadores>();
        servicios.AddScoped<ICargaMasivaTrabajadores, CargaMasivaTrabajadores>();

        // Feriados: sincronización manual contra Nager.Date. El cálculo del tareo nunca pasa
        // por este HttpClient — siempre lee la tabla Feriado local.
        servicios.AddHttpClient<IProveedorFeriadosExternos, ProveedorFeriadosNagerDate>(cliente =>
        {
            cliente.BaseAddress = new Uri("https://date.nager.at/");
            cliente.Timeout = TimeSpan.FromSeconds(10);
        });
        servicios.AddScoped<IServicioFeriados, ServicioFeriados>();

        return servicios;
    }
}

using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Justificaciones;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Reportes;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        servicios.AddDbContext<MarcacionDbContext>(opciones =>
            opciones.UseSqlServer(cadenaConexion, sql => sql.EnableRetryOnFailure()));

        // Plantilla de lectura del marcador. Si no se pasa configuración, quedan los
        // valores por defecto de la clase, que son los del archivo real del G3.
        if (configuracion is not null)
        {
            servicios.Configure<OpcionesMarcador>(configuracion.GetSection(OpcionesMarcador.Seccion));
        }
        else
        {
            servicios.Configure<OpcionesMarcador>(_ => { });
        }

        servicios.AddScoped<ITrabajadorRepository, TrabajadorRepository>();
servicios.AddScoped<IConsultaTrabajadores, ConsultaTrabajadores>();
        servicios.AddScoped<IConsultaCatalogos, ConsultaCatalogos>();
        servicios.AddScoped<IServicioCatalogos, ServicioCatalogos>();
        servicios.AddScoped<IServicioTurnos, ServicioTurnos>();
        servicios.AddScoped<IServicioTiposAusencia, ServicioTiposAusencia>();
        servicios.AddScoped<IServicioCalculoTareo, ServicioCalculoTareo>();
        servicios.AddScoped<IServicioPeriodos, ServicioPeriodos>();
        servicios.AddScoped<IServicioSolicitudes, ServicioSolicitudes>();
        servicios.AddScoped<IServicioJustificaciones, ServicioJustificaciones>();
        servicios.AddScoped<IServicioReportes, ServicioReportes>();
        servicios.AddScoped<IExportadorReportesExcel, ExportadorReportesExcel>();
        servicios.AddScoped<IServicioUsuarios, ServicioUsuarios>();

        // Ingesta: el lector del TXT es un adaptador detrás de IFuenteDeMarcaciones, para
        // que una futura conexión directa al equipo no obligue a tocar el servicio.
        servicios.AddScoped<IFuenteDeMarcaciones, LectorTxtZkteco>();
        servicios.AddScoped<IServicioImportacion, ServicioImportacion>();

        servicios.AddScoped<ServicioTrabajadores>();

        return servicios;
    }
}

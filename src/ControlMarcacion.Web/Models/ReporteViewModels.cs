using System.Globalization;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Reportes;

namespace ControlMarcacion.Web.Models;

/// <summary>Filtros comunes a las pantallas de reportes.</summary>
/// <remarks>
/// Los cuatro alcances viajan en la misma query string y solo se leen los campos del
/// alcance elegido: así un enlace de exportación es el mismo que el de la consulta, y
/// cambiar de alcance no pierde lo que ya se había escrito en los otros.
/// </remarks>
public sealed class FiltroReporteVm
{
    public AlcanceReporte Alcance { get; init; } = AlcanceReporte.Periodo;

    public long? PeriodoId { get; init; }

    public int? Anio { get; init; }

    public int? Mes { get; init; }

    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }

    public long? EmpresaId { get; init; }

    public long? PlantaId { get; init; }

    public long? CentroId { get; init; }

    public long? CentroCostoId { get; init; }

    /// <summary>Aparato marcador del que salieron las marcaciones.</summary>
    public long? EquipoId { get; init; }

    /// <summary>
    /// Si el alcance elegido está completo como para consultar.
    /// </summary>
    /// <remarks>
    /// Es solo para decidir si la pantalla arranca vacía o consulta: la validación de
    /// verdad —con su mensaje— vive en <see cref="ResolutorRangoReporte"/>, en Domain.
    /// Repetirla aquí con otro criterio sería la clase de duplicación que termina
    /// divergiendo.
    /// </remarks>
    public bool EstaCompleto => Alcance switch
    {
        AlcanceReporte.Periodo => PeriodoId is not null,
        AlcanceReporte.Mes => Anio is not null && Mes is not null,
        AlcanceReporte.Dia => Desde is not null,
        AlcanceReporte.Rango => Desde is not null && Hasta is not null,
        _ => false
    };

    /// <summary>El tareo final solo se emite por periodo: ver <c>IServicioReportes</c>.</summary>
    public bool PuedeDescargarTareoFinal => Alcance == AlcanceReporte.Periodo && PeriodoId is not null;

    /// <summary>La query string que arrastra el filtro completo a otra acción.</summary>
    /// <remarks>
    /// Todo se formatea con <see cref="CultureInfo.InvariantCulture"/>: la interfaz muestra
    /// las fechas en <c>dd/MM/yyyy</c> y los números con separador local, pero una URL no es
    /// interfaz — la lee el binding del servidor, que espera ISO y punto decimal.
    /// </remarks>
    public Dictionary<string, string?> ARuta() => new()
    {
        ["Alcance"] = Alcance.ToString(),
        ["PeriodoId"] = PeriodoId?.ToString(CultureInfo.InvariantCulture),
        ["Anio"] = Anio?.ToString(CultureInfo.InvariantCulture),
        ["Mes"] = Mes?.ToString(CultureInfo.InvariantCulture),
        ["Desde"] = Desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["Hasta"] = Hasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["EmpresaId"] = EmpresaId?.ToString(CultureInfo.InvariantCulture),
        ["PlantaId"] = PlantaId?.ToString(CultureInfo.InvariantCulture),
        ["CentroId"] = CentroId?.ToString(CultureInfo.InvariantCulture),
        ["CentroCostoId"] = CentroCostoId?.ToString(CultureInfo.InvariantCulture),
        ["EquipoId"] = EquipoId?.ToString(CultureInfo.InvariantCulture)
    };

}

/// <summary>Lo que comparten las dos pantallas de reportes.</summary>
public abstract class ReporteVm
{
    public FiltroReporteVm Filtro { get; init; } = new();

    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    public IReadOnlyList<OpcionDto> Empresas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Centros { get; init; } = [];

    public IReadOnlyList<OpcionDto> CentrosCosto { get; init; } = [];

    /// <summary>
    /// Aparatos ofrecidos en el filtro, ya acotados a la planta elegida si la hay.
    /// </summary>
    public IReadOnlyList<EquipoOpcionDto> Equipos { get; init; } = [];

    /// <summary>
    /// Encabezado de trazabilidad del alcance consultado, o <c>null</c> si todavía no se
    /// consultó nada.
    /// </summary>
    /// <remarks>
    /// Es lo que permite mostrar en pantalla el mismo aviso de tareo desactualizado que
    /// lleva el Excel: sin él, la pantalla enseñaría cifras viejas sin distinguirse de las
    /// buenas.
    /// </remarks>
    public MetadatosReporte? Metadatos { get; init; }

    public bool HayAlcanceSeleccionado => Filtro.EstaCompleto;
}

/// <summary>Modelo del resumen mensual de tareo.</summary>
public sealed class ResumenTareoVm : ReporteVm
{
    public ResultadoPaginado<ResumenTareoDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<ResumenTareoDto>(FilasPorPagina);

    /// <summary>
    /// Totales del <b>alcance completo</b>, calculados en el servicio.
    /// </summary>
    /// <remarks>
    /// No se derivan de <see cref="Resultado"/>: esa es una página. Ver
    /// <c>TotalesResumenTareo</c>.
    /// </remarks>
    public TotalesResumenTareo Totales { get; init; } = TotalesResumenTareo.Vacio;

    /// <summary>Filas por página. Vive aquí porque la vista también la necesita.</summary>
    public const int FilasPorPagina = 50;
}

/// <summary>Modelo del reporte de excepciones.</summary>
public sealed class ExcepcionesVm : ReporteVm
{
    public ResultadoPaginado<ExcepcionDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<ExcepcionDto>(FilasPorPagina);

    public const int FilasPorPagina = 50;
}

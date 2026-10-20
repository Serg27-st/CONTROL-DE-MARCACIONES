using ControlMarcacion.Application.Marcaciones;
using NSubstitute;

namespace ControlMarcacion.Infrastructure.Tests;

/// <summary>
/// Doble de <see cref="IServicioImportacion"/> para los tests del motor que no van sobre la
/// cobertura de carga.
/// </summary>
/// <remarks>
/// El motor pregunta qué aparatos no entregaron archivo cada día, porque un día sin cargar
/// no es una falta. En los tests que prueban otra cosa la respuesta es «no falta ninguno»:
/// así el día sin marcaciones sigue siendo falta y el escenario se comporta como antes de
/// existir la cobertura.
///
/// Los tests que sí van sobre la cobertura usan el servicio real contra SQLite, que es
/// donde la regla se puede equivocar.
/// </remarks>
internal static class CoberturaDePrueba
{
    /// <summary>Todos los aparatos entregaron su archivo todos los días.</summary>
    public static IServicioImportacion TodoCargado()
    {
        var doble = Substitute.For<IServicioImportacion>();

        doble
            .AparatosSinCargaAsync(default, default, default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyDictionary<DateOnly, IReadOnlyList<long>>>(
                new Dictionary<DateOnly, IReadOnlyList<long>>()));

        return doble;
    }
}

using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Comun;

/// <summary>
/// La línea de tiempo de una asignación con vigencia no admite huecos imposibles ni
/// solapamientos.
/// </summary>
/// <remarks>
/// Escrito sobre <see cref="AsignacionTurno"/> porque la rotación de turnos es el caso que
/// motivó el validador, pero la regla aplica igual al ID de marcador y al sueldo básico:
/// las tres comparten <see cref="IVigenciaPorFecha"/>.
/// </remarks>
public sealed class ValidadorVigenciasTests
{
    private const string Turno = "turno";

    [Fact]
    public void Una_sola_asignacion_abierta_es_valida()
    {
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 1, 1), null)
        ];

        Should.NotThrow(() => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));
    }

    [Fact]
    public void Rotacion_quincenal_encadenada_es_valida()
    {
        // Cuatro rotaciones seguidas, cada una empezando el día siguiente al cierre de la
        // anterior. Es exactamente la forma que debe tener una rotación bien registrada.
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)),
            Asignar(2, new DateOnly(2026, 7, 16), new DateOnly(2026, 7, 31)),
            Asignar(1, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)),
            Asignar(2, new DateOnly(2026, 8, 16), null)
        ];

        Should.NotThrow(() => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));
    }

    [Fact]
    public void Un_hueco_entre_dos_vigencias_no_es_error()
    {
        // Un día sin turno asignado no es un dato corrupto: el motor lo resuelve como
        // "sin horario" y lo explica. Lo que no puede haber es dos turnos a la vez.
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10)),
            Asignar(2, new DateOnly(2026, 7, 20), null)
        ];

        Should.NotThrow(() => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));
    }

    [Fact]
    public void Dos_asignaciones_abiertas_a_la_vez_se_rechazan()
    {
        // Es el caso que la base aceptaba en silencio: dos filas con FechaFin nula.
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 1), null),
            Asignar(2, new DateOnly(2026, 7, 6), null)
        ];

        var error = Should.Throw<ReglaNegocioException>(
            () => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));

        error.Message.ShouldContain("dos asignaciones de turno a la vez");
    }

    [Fact]
    public void Dos_vigencias_solapadas_se_rechazan()
    {
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20)),
            Asignar(2, new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 31))
        ];

        var error = Should.Throw<ReglaNegocioException>(
            () => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));

        error.Message.ShouldContain("se solapan");
    }

    [Fact]
    public void El_dia_de_cierre_compartido_se_rechaza()
    {
        // Cerrar el 15 y abrir el 15 deja al trabajador con dos turnos ese día.
        // La rotación correcta cierra el 15 y abre el 16.
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)),
            Asignar(2, new DateOnly(2026, 7, 15), null)
        ];

        Should.Throw<ReglaNegocioException>(
            () => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));
    }

    [Fact]
    public void Una_vigencia_que_termina_antes_de_empezar_se_rechaza()
    {
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 16), new DateOnly(2026, 7, 15))
        ];

        var error = Should.Throw<ReglaNegocioException>(
            () => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, Turno));

        error.Message.ShouldContain("vigencia imposible");
    }

    [Fact]
    public void El_mensaje_nombra_el_dato_que_se_estaba_asignando()
    {
        // Quien lee el error es personal de planta, no un desarrollador: tiene que decir
        // qué se estaba asignando, no "IVigenciaPorFecha".
        AsignacionTurno[] vigencias =
        [
            Asignar(1, new DateOnly(2026, 7, 1), null),
            Asignar(2, new DateOnly(2026, 7, 6), null)
        ];

        var error = Should.Throw<ReglaNegocioException>(
            () => ValidadorVigencias.GarantizarLineaDeTiempoSana(vigencias, "ID de marcador"));

        error.Message.ShouldContain("ID de marcador");
    }

    [Fact]
    public void Una_lista_vacia_es_valida()
    {
        Should.NotThrow(
            () => ValidadorVigencias.GarantizarLineaDeTiempoSana(Array.Empty<AsignacionTurno>(), Turno));
    }

    private static AsignacionTurno Asignar(long turnoId, DateOnly desde, DateOnly? hasta) =>
        new() { TurnoId = turnoId, FechaInicio = desde, FechaFin = hasta };
}

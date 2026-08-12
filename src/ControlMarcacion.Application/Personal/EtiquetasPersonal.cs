using ControlMarcacion.Domain.Personal;

namespace ControlMarcacion.Application.Personal;

/// <summary>
/// Textos en español de los enums de Personal.
/// </summary>
/// <remarks>
/// Los nombres de los miembros del enum no sirven para mostrar: <c>NoSuperoPeriodoPrueba</c>
/// no es lo que un usuario espera leer, y ponerle tildes al identificador para arreglarlo
/// sería peor. La traducción vive aquí, en un solo sitio, y no repartida por las vistas.
/// </remarks>
public static class EtiquetasPersonal
{
    public static string Describir(MotivoCese motivo) => motivo switch
    {
        MotivoCese.RenunciaVoluntaria => "Renuncia voluntaria",
        MotivoCese.TerminoContrato => "Término de contrato",
        MotivoCese.NoSuperoPeriodoPrueba => "No superó el periodo de prueba",
        MotivoCese.AbandonoLaboral => "Abandono laboral",
        MotivoCese.Despido => "Despido",
        MotivoCese.Destitucion => "Destitución",
        _ => motivo.ToString()
    };

    /// <summary>Los seis motivos con su etiqueta, en el orden en que los listó GCA.</summary>
    public static IReadOnlyList<(MotivoCese Valor, string Etiqueta)> MotivosCese { get; } =
        Enum.GetValues<MotivoCese>()
            .Select(m => (m, Describir(m)))
            .ToList();

    public static string Describir(TipoPlanilla tipo) => tipo switch
    {
        TipoPlanilla.Obrero => "Obrero (OBM)",
        TipoPlanilla.Empleado => "Empleado (EMP)",
        _ => tipo.ToString()
    };

    public static string Describir(Sexo sexo) => sexo switch
    {
        Sexo.Masculino => "Masculino",
        Sexo.Femenino => "Femenino",
        _ => sexo.ToString()
    };
}

using ControlMarcacion.Application.Marcaciones;

namespace ControlMarcacion.Infrastructure.Marcaciones;

internal sealed class ContextoVinculacionBiometricaNoConfigurado : IContextoAutorizacionVinculacionBiometrica
{
    public bool EstaAutenticado => false;
    public bool EsCoordinador => false;
    public string Usuario => "sistema";
}

internal sealed class ProveedorEvidenciaIdentidadNoConfigurado : IProveedorEvidenciaIdentidad
{
    public Task<EvidenciaIdentidadVerificada?> VerificarAsync(
        FuenteEvidenciaDocumento fuente,
        string referencia,
        CancellationToken cancelacion = default) =>
        Task.FromResult<EvidenciaIdentidadVerificada?>(null);
}

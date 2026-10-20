namespace ControlMarcacion.Web.Operaciones;

internal enum FasePreparacionAplicacion
{
    Starting,
    Ready,
    Failed
}

/// <summary>
/// Estado thread-safe de la preparación crítica. No contiene excepciones ni secretos.
/// </summary>
internal sealed class EstadoPreparacionAplicacion
{
    private int fase = (int)FasePreparacionAplicacion.Starting;

    public FasePreparacionAplicacion Fase =>
        (FasePreparacionAplicacion)Volatile.Read(ref fase);

    public bool PermiteTraficoFuncional => Fase == FasePreparacionAplicacion.Ready;

    public void MarcarPreparada() =>
        Interlocked.Exchange(ref fase, (int)FasePreparacionAplicacion.Ready);

    public void MarcarFallida() =>
        Interlocked.Exchange(ref fase, (int)FasePreparacionAplicacion.Failed);
}

using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Ficha del trabajador: datos personales y datos laborales.
/// </summary>
public class Trabajador : EntidadBase
{
    // ---------- Datos personales ----------

    public long TipoDocumentoId { get; set; }

    /// <summary>
    /// Número de documento de identidad.
    /// <b>Siempre texto, jamás numérico:</b> un DNI que empieza en cero pierde el dígito
    /// al convertirse a número, y el cruce con el marcador falla.
    /// </summary>
    public string NumeroDocumento { get; set; } = null!;

    public string ApellidoPaterno { get; set; } = null!;

    public string ApellidoMaterno { get; set; } = null!;

    public string Nombres { get; set; } = null!;

    public Sexo Sexo { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public string? CorreoElectronico { get; set; }

    public bool TieneHijos { get; set; }

    // ---------- Datos laborales ----------

    public long EmpresaId { get; set; }

    /// <summary>Unidad organizativa. ⚠️ Definición pendiente de confirmar con GCA.</summary>
    public string? Unidad { get; set; }

    /// <summary>Obrero (OBM) o empleado (EMP). Determina el régimen de turnos.</summary>
    public TipoPlanilla TipoPlanilla { get; set; }

    public long PlantaId { get; set; }

    /// <summary>
    /// Aproximación anterior a <see cref="CentroCostoId"/>, la fuente real de SAP.
    /// <b>Ya no se captura en el formulario de Personal</b> (Centro/Centro de costo
    /// cumplen ese rol), pero se conserva como respaldo: <c>ServicioReportes</c> del
    /// módulo de Reportes todavía la usa mientras un trabajador no tenga Centro de
    /// costo asignado. Nulo en trabajadores nuevos, no en los que ya la tenían.
    /// </summary>
    public long? AreaId { get; set; }

    /// <summary>Ver <see cref="AreaId"/>: mismo respaldo, mismo motivo.</summary>
    public long? SeccionId { get; set; }

    public DateOnly FechaIngreso { get; set; }

    /// <summary>Nulo mientras el trabajador siga activo.</summary>
    public DateOnly? FechaCese { get; set; }

    /// <summary>
    /// Causal del cese. Va siempre acompañada de <see cref="FechaCese"/>: las dos se
    /// escriben juntas desde <see cref="DarDeBaja"/> y nunca por separado.
    /// </summary>
    public MotivoCese? MotivoCese { get; set; }

    /// <summary>Detalle libre del cese, opcional.</summary>
    public string? ObservacionCese { get; set; }

    public long PuestoId { get; set; }

    /// <summary>
    /// Centro de costo real de SAP (catálogo Centro → Centro de costo, una
    /// dimensión propia ajena a Planta/Área/Sección). Campo plano y nullable,
    /// <b>sin vigencia por fecha</b>: a diferencia del ID de marcador, el turno y
    /// el sueldo básico, nadie pidió historizarlo, y los trabajadores existentes
    /// no tendrán valor hasta que alguien los edite.
    /// </summary>
    public long? CentroCostoId { get; set; }

    // ---------- Asignaciones con vigencia ----------

    /// <summary>
    /// El ID de marcador NO es un campo plano de esta entidad: se reasigna a otra
    /// persona con el tiempo, y guardarlo aquí haría que las marcaciones históricas
    /// quedaran atribuidas a quien no era.
    /// </summary>
    public ICollection<AsignacionMarcador> AsignacionesMarcador { get; set; } = [];

    /// <summary>
    /// Los obreros rotan de turno por calendario, así que el turno tampoco es un campo
    /// plano: se resuelve por la asignación vigente a la fecha que se está calculando.
    /// </summary>
    public ICollection<AsignacionTurno> AsignacionesTurno { get; set; } = [];

    /// <summary>
    /// El sueldo básico tampoco es un campo plano: cambia con el tiempo y algunos
    /// cálculos legales necesitan el sueldo vigente en la fecha exacta que se calcula.
    /// </summary>
    public ICollection<AsignacionSueldo> AsignacionesSueldo { get; set; } = [];

    /// <summary>
    /// Historia de <see cref="FechaIngreso"/> y <see cref="FechaCese"/>: una fila por
    /// cada vez que la persona entró y salió de la empresa. Ver <see cref="PeriodoLaboral"/>.
    /// </summary>
    public ICollection<PeriodoLaboral> PeriodosLaborales { get; set; } = [];

    // ---------- Comportamiento ----------

    /// <summary>Formato usado en el tareo final.</summary>
    public string ApellidosYNombres => $"{ApellidoPaterno} {ApellidoMaterno}, {Nombres}";

    /// <summary>
    /// Indica si el trabajador tenía vínculo laboral en la fecha dada.
    /// El motor de cálculo lo usa para no generar faltas antes del ingreso
    /// ni después del cese.
    /// </summary>
    public bool TieneVinculoEn(DateOnly fecha) =>
        fecha >= FechaIngreso && (FechaCese is null || fecha <= FechaCese.Value);

    /// <summary>Sigue en planilla: no tiene cese registrado.</summary>
    public bool EstaVigente => FechaCese is null;

    /// <summary>
    /// Registra el cese: fecha y causal, las dos obligatorias.
    /// </summary>
    /// <remarks>
    /// Cerrar el vínculo cierra también las asignaciones abiertas de marcador, turno y
    /// sueldo en la misma fecha. Dejarlas abiertas haría que el ID del marcador siguiera
    /// figurando como ocupado por alguien que ya no viene, y nadie podría reasignárselo
    /// al reemplazo sin editar a mano una asignación que este sistema no permite editar.
    /// </remarks>
    public void DarDeBaja(DateOnly fechaCese, MotivoCese motivo, string? observacion, string usuario)
    {
        if (!EstaVigente)
        {
            throw new ReglaNegocioException(
                $"El trabajador ya tiene un cese registrado el {FechaCese:dd/MM/yyyy}. " +
                "Si la fecha o el motivo son incorrectos, corrija el cese en vez de registrar otro.");
        }

        if (fechaCese < FechaIngreso)
        {
            throw new ReglaNegocioException(
                $"La fecha de cese ({fechaCese:dd/MM/yyyy}) no puede ser anterior a la de " +
                $"ingreso ({FechaIngreso:dd/MM/yyyy}).");
        }

        FechaCese = fechaCese;
        MotivoCese = motivo;
        ObservacionCese = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();
        ModificadoPor = usuario;

        var vinculo = PeriodosLaborales.FirstOrDefault(p => p.EstaAbierto);

        if (vinculo is not null)
        {
            vinculo.FechaCese = fechaCese;
            vinculo.MotivoCese = motivo;
            vinculo.ObservacionCese = ObservacionCese;
            vinculo.ModificadoPor = usuario;
        }

        CerrarAsignacionesAbiertas(fechaCese, usuario);
    }

    /// <summary>
    /// Cierra las asignaciones de un trabajador que se dio de alta <b>ya cesado</b>.
    /// </summary>
    /// <remarks>
    /// Es el caso de la carga masiva de personal histórico: la ficha nace con
    /// <see cref="FechaCese"/> puesta, sin pasar nunca por <see cref="DarDeBaja"/>. Sin
    /// esto sus asignaciones de marcador, turno y sueldo quedarían abiertas para siempre,
    /// y el ID del marcador figuraría ocupado por alguien que ya no está — impidiendo
    /// asignárselo a su reemplazo, que es justo lo que el cese viene a habilitar.
    ///
    /// No hace nada si el trabajador está vigente: dar de baja es otra operación, y esta
    /// no debe poder usarse para saltársela.
    /// </remarks>
    public void CerrarAsignacionesSiNaceCesado(string usuario)
    {
        if (FechaCese is { } cese)
        {
            CerrarAsignacionesAbiertas(cese, usuario);
        }
    }

    /// <summary>
    /// Cambia la razón social del trabajador.
    /// </summary>
    /// <remarks>
    /// <b>Exige que esté cesado.</b> Consulting Advisers y CA Outsourcing son dos
    /// personas jurídicas distintas: mover a alguien de una a otra sin cerrar el
    /// vínculo anterior reescribiría a qué empresa pertenecen sus tareos ya calculados
    /// — incluidos los de periodos cerrados que ya se pagaron desde la otra planilla.
    /// El cese es el hecho que separa un vínculo del siguiente.
    /// </remarks>
    public void CambiarEmpresa(long nuevaEmpresaId, string usuario)
    {
        if (nuevaEmpresaId == EmpresaId)
        {
            return;
        }

        if (EstaVigente)
        {
            throw new ReglaNegocioException(
                "No se puede cambiar la empresa de un trabajador activo. Registre primero " +
                "su baja indicando fecha de cese y motivo; recién entonces podrá asignarlo " +
                "a la otra razón social.");
        }

        EmpresaId = nuevaEmpresaId;
        ModificadoPor = usuario;
    }

    /// <summary>
    /// Abre el vínculo laboral inicial. Se llama una sola vez, al dar de alta la ficha.
    /// </summary>
    public void AbrirVinculo(string usuario) =>
        PeriodosLaborales.Add(new PeriodoLaboral
        {
            EmpresaId = EmpresaId,
            FechaIngreso = FechaIngreso,
            FechaCese = FechaCese,
            MotivoCese = MotivoCese,
            ObservacionCese = ObservacionCese,
            CreadoPor = usuario
        });

    private void CerrarAsignacionesAbiertas(DateOnly fechaCese, string usuario)
    {
        foreach (var asignacion in AsignacionesMarcador.Where(a => a.FechaFin is null))
        {
            asignacion.FechaFin = fechaCese;
            asignacion.ModificadoPor = usuario;
        }

        foreach (var asignacion in AsignacionesTurno.Where(a => a.FechaFin is null))
        {
            asignacion.FechaFin = fechaCese;
            asignacion.ModificadoPor = usuario;
        }

        foreach (var asignacion in AsignacionesSueldo.Where(a => a.FechaFin is null))
        {
            asignacion.FechaFin = fechaCese;
            asignacion.ModificadoPor = usuario;
        }
    }
}

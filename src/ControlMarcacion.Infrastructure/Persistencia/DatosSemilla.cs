using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Organizacion;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Datos base del sistema: estructura organizativa de GCA/Intradevco, catálogos
/// y tasas legales de sobretasa.
/// </summary>
/// <remarks>
/// Son <b>datos</b>, no código. Los cinco turnos y las tasas de horas extras viven aquí
/// justamente para que cambiarlos sea un UPDATE y no un despliegue.
///
/// La fecha de auditoría es fija a propósito: <c>DateTime.Now</c> en un <c>HasData</c>
/// haría que EF detecte un cambio en el modelo en cada compilación y genere migraciones
/// espurias sin parar.
/// </remarks>
internal static class DatosSemilla
{
    private static readonly DateTime Origen = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public static void Aplicar(ModelBuilder modelBuilder)
    {
        SembrarOrganizacion(modelBuilder);
        SembrarTurnos(modelBuilder);
        SembrarCatalogos(modelBuilder);
        SembrarTasasLegales(modelBuilder);
    }

    private static void SembrarOrganizacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>().HasData(
            new Cliente { Id = 1, Codigo = "INTRADEVCO", RazonSocial = "Intradevco", Activo = true, CreadoEn = Origen });

        // ⚠️ PENDIENTE: los RUC son placeholders evidentes (empiezan en 0, cosa que ningún
        // RUC peruano real hace). Deben reemplazarse por los reales antes de producción.
        modelBuilder.Entity<Empresa>().HasData(
            new Empresa
            {
                Id = 1, ClienteId = 1, Codigo = "CA",
                RazonSocial = "Consulting Advisers", Ruc = "00000000001",
                Activo = true, CreadoEn = Origen
            },
            new Empresa
            {
                Id = 2, ClienteId = 1, Codigo = "CAO",
                RazonSocial = "CA Outsourcing", Ruc = "00000000002",
                Activo = true, CreadoEn = Origen
            });

        modelBuilder.Entity<Planta>().HasData(
            new Planta { Id = 1, EmpresaId = 1, Codigo = "DET", Nombre = "Planta Detergentes", Activo = true, CreadoEn = Origen },
            new Planta { Id = 2, EmpresaId = 1, Codigo = "P20M", Nombre = "Planta 20 mil", Activo = true, CreadoEn = Origen },
            new Planta { Id = 3, EmpresaId = 2, Codigo = "P01", Nombre = "Planta 01", Activo = true, CreadoEn = Origen },
            new Planta { Id = 4, EmpresaId = 2, Codigo = "SULF", Nombre = "Planta Sulfonación", Activo = true, CreadoEn = Origen });

        // ⚠️ PROVISIONAL: GCA aún no entregó la estructura real de áreas, secciones y
        // puestos de cada planta. Se siembra un mínimo genérico para que la ficha de
        // personal sea utilizable desde ya — sin esto, área, sección y puesto son
        // obligatorios y no se podría registrar a nadie.
        // Reemplazar por la estructura real; el mantenimiento de catálogos permitirá
        // hacerlo desde la aplicación.
        modelBuilder.Entity<Area>().HasData(
            new Area { Id = 1, PlantaId = 1, Codigo = "PROD", Nombre = "Producción", Activo = true, CreadoEn = Origen },
            new Area { Id = 2, PlantaId = 2, Codigo = "PROD", Nombre = "Producción", Activo = true, CreadoEn = Origen },
            new Area { Id = 3, PlantaId = 3, Codigo = "PROD", Nombre = "Producción", Activo = true, CreadoEn = Origen },
            new Area { Id = 4, PlantaId = 4, Codigo = "PROD", Nombre = "Producción", Activo = true, CreadoEn = Origen });

        modelBuilder.Entity<Seccion>().HasData(
            new Seccion { Id = 1, AreaId = 1, Codigo = "GEN", Nombre = "General", Activo = true, CreadoEn = Origen },
            new Seccion { Id = 2, AreaId = 2, Codigo = "GEN", Nombre = "General", Activo = true, CreadoEn = Origen },
            new Seccion { Id = 3, AreaId = 3, Codigo = "GEN", Nombre = "General", Activo = true, CreadoEn = Origen },
            new Seccion { Id = 4, AreaId = 4, Codigo = "GEN", Nombre = "General", Activo = true, CreadoEn = Origen });

        modelBuilder.Entity<Puesto>().HasData(
            new Puesto { Id = 1, Codigo = "OPER", Nombre = "Operario", Activo = true, CreadoEn = Origen },
            new Puesto { Id = 2, Codigo = "SUPV", Nombre = "Supervisor", Activo = true, CreadoEn = Origen },
            new Puesto { Id = 3, Codigo = "AUX", Nombre = "Auxiliar", Activo = true, CreadoEn = Origen },
            new Puesto { Id = 4, Codigo = "ANAL", Nombre = "Analista", Activo = true, CreadoEn = Origen });
    }

    private static void SembrarTurnos(ModelBuilder modelBuilder)
    {
        // ⚠️ Los minutos de refrigerio y de tolerancia son un supuesto inicial:
        // 45 min es el mínimo legal de refrigerio en Perú y se asumió 60 en las jornadas
        // de 12 h. Confirmar ambos valores con GCA — afectan directamente el cálculo
        // de horas extras.
        //
        // Las ventanas de anticipación arrancan en 15 min para la entrada y 5 para la
        // salida, según lo indicado por GCA. Se siembran explícitas y no por omisión para
        // que el valor esté a la vista de quien lea la semilla, y son editables por turno
        // desde el catálogo.
        const int AnticipacionEntrada = 15;
        const int AnticipacionSalida = 5;

        modelBuilder.Entity<Turno>().HasData(
            new Turno
            {
                Id = 1, Codigo = "DIU", Nombre = "Diurno",
                HoraInicio = new TimeOnly(7, 0), HoraFin = new TimeOnly(19, 0),
                MinutosRefrigerio = 60, MinutosTolerancia = 5, MinutosJornadaOrdinaria = 480,
                MinutosAnticipacionEntrada = AnticipacionEntrada,
                MinutosAnticipacionSalida = AnticipacionSalida,
                DiasAplicables = DiasSemana.LunesASabado, Activo = true, CreadoEn = Origen
            },
            new Turno
            {
                // Cruza medianoche: la jornada se imputa al día de INICIO del turno.
                Id = 2, Codigo = "NOC", Nombre = "Nocturno",
                HoraInicio = new TimeOnly(19, 0), HoraFin = new TimeOnly(7, 0),
                MinutosRefrigerio = 60, MinutosTolerancia = 5, MinutosJornadaOrdinaria = 480,
                MinutosAnticipacionEntrada = AnticipacionEntrada,
                MinutosAnticipacionSalida = AnticipacionSalida,
                DiasAplicables = DiasSemana.LunesASabado, Activo = true, CreadoEn = Origen
            },
            new Turno
            {
                Id = 3, Codigo = "SAB", Nombre = "Especial sábado",
                HoraInicio = new TimeOnly(7, 0), HoraFin = new TimeOnly(15, 0),
                MinutosRefrigerio = 45, MinutosTolerancia = 5, MinutosJornadaOrdinaria = 480,
                MinutosAnticipacionEntrada = AnticipacionEntrada,
                MinutosAnticipacionSalida = AnticipacionSalida,
                DiasAplicables = DiasSemana.Sabado, Activo = true, CreadoEn = Origen
            },
            new Turno
            {
                Id = 4, Codigo = "DOM1", Nombre = "Dominical 1",
                HoraInicio = new TimeOnly(7, 0), HoraFin = new TimeOnly(15, 0),
                MinutosRefrigerio = 45, MinutosTolerancia = 5, MinutosJornadaOrdinaria = 480,
                MinutosAnticipacionEntrada = AnticipacionEntrada,
                MinutosAnticipacionSalida = AnticipacionSalida,
                DiasAplicables = DiasSemana.Domingo, Activo = true, CreadoEn = Origen
            },
            new Turno
            {
                Id = 5, Codigo = "DOM2", Nombre = "Dominical 2",
                HoraInicio = new TimeOnly(7, 0), HoraFin = new TimeOnly(19, 0),
                MinutosRefrigerio = 60, MinutosTolerancia = 5, MinutosJornadaOrdinaria = 480,
                MinutosAnticipacionEntrada = AnticipacionEntrada,
                MinutosAnticipacionSalida = AnticipacionSalida,
                DiasAplicables = DiasSemana.Domingo, Activo = true, CreadoEn = Origen
            });

        // El turno especial de sábado solo existe en Planta 01. Se modela como dato,
        // no como condición en el código: extenderlo a otra planta será un INSERT.
        modelBuilder.Entity<TurnoPlanta>().HasData(
            new TurnoPlanta { Id = 1, TurnoId = 3, PlantaId = 3, Activo = true, CreadoEn = Origen });
    }

    private static void SembrarCatalogos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipoDocumento>().HasData(
            new TipoDocumento { Id = 1, Codigo = "DNI", Nombre = "Documento Nacional de Identidad", LongitudExacta = 8, SoloDigitos = true, Activo = true, CreadoEn = Origen },
            new TipoDocumento { Id = 2, Codigo = "CE", Nombre = "Carné de Extranjería", SoloDigitos = false, Activo = true, CreadoEn = Origen },
            new TipoDocumento { Id = 3, Codigo = "PAS", Nombre = "Pasaporte", SoloDigitos = false, Activo = true, CreadoEn = Origen },
            new TipoDocumento { Id = 4, Codigo = "PTP", Nombre = "Permiso Temporal de Permanencia", SoloDigitos = false, Activo = true, CreadoEn = Origen });

        // La lista puede crecer: agregar un tipo nuevo es un INSERT, no un despliegue.
        modelBuilder.Entity<TipoAusencia>().HasData(
            new TipoAusencia { Id = 1, Codigo = "VAC", Nombre = "Vacaciones", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 2, Codigo = "DM", Nombre = "Descanso médico", Comportamiento = ComportamientoAusencia.Subsidiada, ComputaComoAsistencia = true, RequiereSustento = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 3, Codigo = "LCG", Nombre = "Licencia con goce", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 4, Codigo = "LSG", Nombre = "Licencia sin goce", Comportamiento = ComportamientoAusencia.JustificadaNoRemunerada, ComputaComoAsistencia = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 5, Codigo = "PER", Nombre = "Permiso", Comportamiento = ComportamientoAusencia.PermisoParcial, ComputaComoAsistencia = true, EsPorHoras = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 6, Codigo = "LPAT", Nombre = "Licencia paternal", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, RequiereSustento = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 7, Codigo = "LMAT", Nombre = "Licencia por maternidad", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, RequiereSustento = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 8, Codigo = "SUBE", Nombre = "Subsidio por enfermedad", Comportamiento = ComportamientoAusencia.Subsidiada, ComputaComoAsistencia = true, RequiereSustento = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 9, Codigo = "SUBA", Nombre = "Subsidio por accidente", Comportamiento = ComportamientoAusencia.Subsidiada, ComputaComoAsistencia = true, RequiereSustento = true, Activo = true, CreadoEn = Origen });
    }

    private static void SembrarTasasLegales(ModelBuilder modelBuilder)
    {
        // Mínimos legales peruanos. Si el convenio colectivo de GCA mejora alguna tasa,
        // se cierra la vigencia de estas filas y se insertan las nuevas: así un recálculo
        // de un periodo antiguo sigue aplicando las tasas que estaban vigentes entonces.
        var desde = new DateOnly(2002, 8, 1);

        modelBuilder.Entity<TasaSobretasa>().HasData(
            new TasaSobretasa { Id = 1, Concepto = ConceptoSobretasa.HoraExtraPrimerasDos, Porcentaje = 25m, VigenteDesde = desde, BaseLegal = "D.S. 007-2002-TR, art. 10", Activo = true, CreadoEn = Origen },
            new TasaSobretasa { Id = 2, Concepto = ConceptoSobretasa.HoraExtraAdicionales, Porcentaje = 35m, VigenteDesde = desde, BaseLegal = "D.S. 007-2002-TR, art. 10", Activo = true, CreadoEn = Origen },
            new TasaSobretasa { Id = 3, Concepto = ConceptoSobretasa.TrabajoNocturno, Porcentaje = 35m, VigenteDesde = desde, BaseLegal = "D.S. 007-2002-TR, art. 8 (sobre la RMV)", Activo = true, CreadoEn = Origen },
            new TasaSobretasa { Id = 4, Concepto = ConceptoSobretasa.DescansoSemanalTrabajado, Porcentaje = 100m, VigenteDesde = desde, BaseLegal = "D. Leg. 713, art. 3", Activo = true, CreadoEn = Origen },
            new TasaSobretasa { Id = 5, Concepto = ConceptoSobretasa.FeriadoTrabajado, Porcentaje = 100m, VigenteDesde = desde, BaseLegal = "D. Leg. 713, art. 9", Activo = true, CreadoEn = Origen });
    }
}

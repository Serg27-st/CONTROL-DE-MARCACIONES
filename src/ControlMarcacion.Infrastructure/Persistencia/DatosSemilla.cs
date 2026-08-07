using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Organizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Datos base del sistema: estructura organizativa de GCA/Intradevco, catálogos,
/// tasas legales de sobretasa y parámetros legales con vigencia por fecha.
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
        SembrarCentros(modelBuilder);
        SembrarTurnos(modelBuilder);
        SembrarCatalogos(modelBuilder);
        SembrarTasasLegales(modelBuilder);
        SembrarParametrosLegales(modelBuilder);
        SembrarPeriodos(modelBuilder);
        SembrarRoles(modelBuilder);
    }

    /// <summary>
    /// Los cuatro roles del sistema. Los usuarios NO se siembran aquí: una cuenta con
    /// contraseña es un credencial, y este archivo queda versionado en el repositorio.
    /// El primer coordinador se crea en el arranque de la aplicación a partir de
    /// configuración externa (ver <c>InicializadorSeguridad</c>).
    /// </summary>
    /// <remarks>
    /// Los Ids 2, 3 y 4 correspondían a RRHH, SUPERVISOR y CONSULTA, retirados del sistema.
    /// No se reciclan: los nuevos roles entran con Ids 5, 6 y 7 para que cualquier fila
    /// histórica que todavía apuntara a los viejos falle de forma visible en vez de quedar
    /// silenciosamente reasignada a otro rol con más permisos.
    /// </remarks>
    private static void SembrarRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole>().HasData(
            // Id 1 se mantiene: es el mismo rol de siempre, pero su semántica cambió —
            // ya no es el rol superior del sistema, ahora "gestiona el tareo".
            new IdentityRole
            {
                Id = "00000000-0000-0000-0000-000000000001",
                Name = RolesSistema.Administrador,
                NormalizedName = RolesSistema.Administrador,
                ConcurrencyStamp = "00000000-0000-0000-0000-000000000001"
            },
            new IdentityRole
            {
                Id = "00000000-0000-0000-0000-000000000005",
                Name = RolesSistema.Coordinador,
                NormalizedName = RolesSistema.Coordinador,
                ConcurrencyStamp = "00000000-0000-0000-0000-000000000005"
            },
            new IdentityRole
            {
                Id = "00000000-0000-0000-0000-000000000006",
                Name = RolesSistema.Gerencia,
                NormalizedName = RolesSistema.Gerencia,
                ConcurrencyStamp = "00000000-0000-0000-0000-000000000006"
            },
            new IdentityRole
            {
                Id = "00000000-0000-0000-0000-000000000007",
                Name = RolesSistema.TrabajadorSocial,
                NormalizedName = RolesSistema.TrabajadorSocial,
                ConcurrencyStamp = "00000000-0000-0000-0000-000000000007"
            });
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

    /// <summary>
    /// Catálogo real de Centros de Costo de SAP, entregado por GCA. Reemplaza la
    /// aproximación con Área/Sección que usaba el tareo final hasta ahora:
    /// <see cref="Centro"/> es una dimensión propia de SAP (columna "Centro" del
    /// archivo modelo), ajena a la jerarquía Planta → Área → Sección, y
    /// <see cref="CentroCosto"/> llena "Ceco"/"Descrip. Ceco". El código de centro
    /// de costo NO es único a nivel global: 9 códigos se repiten bajo distintos
    /// centros (verificado sobre las 106 filas reales).
    /// </summary>
    /// <remarks>
    /// El cruce de empresa se hizo por <c>RazonSocial</c> (case-insensitive, con
    /// trim) contra los dos únicos valores del catálogo SAP — "CA Outsourcing" y
    /// "Consulting Advisers" — no por un código SAP nuevo en <see cref="Empresa"/>.
    /// Los cinco centros (066D/E/F bajo CA Outsourcing; 066G/H bajo Consulting
    /// Advisers) y los 106 centros de costo se generaron a partir del CSV limpio
    /// que entregó GCA, para no transcribirlos a mano fila por fila.
    /// </remarks>
    private static void SembrarCentros(ModelBuilder modelBuilder)
    {
        // Las descripciones no vienen en el archivo de SAP (solo trae el código de centro):
        // las entregó GCA aparte, y son las que llenan "Texto Centro" en el tareo final.
        modelBuilder.Entity<Centro>().HasData(
            new Centro { Id = 1, EmpresaId = 2, Codigo = "066D", Descripcion = "PLANTA INTRACLORO", Activo = true, CreadoEn = Origen },
            new Centro { Id = 2, EmpresaId = 2, Codigo = "066E", Descripcion = "CENTRO LURÍN", Activo = true, CreadoEn = Origen },
            new Centro { Id = 3, EmpresaId = 2, Codigo = "066F", Descripcion = "PLANTA SULFONACIÓN", Activo = true, CreadoEn = Origen },
            new Centro { Id = 4, EmpresaId = 1, Codigo = "066G", Descripcion = "PLANTA DETERGENTES LURÍN", Activo = true, CreadoEn = Origen },
            new Centro { Id = 5, EmpresaId = 1, Codigo = "066H", Descripcion = "PLANTA INTRACAN/FEM", Activo = true, CreadoEn = Origen });

        modelBuilder.Entity<CentroCosto>().HasData(
            new CentroCosto { Id = 1, CentroId = 1, Codigo = "PE21400700", Descripcion = "PRODUCCIÓN PET RECICLADO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 2, CentroId = 1, Codigo = "PE21400701", Descripcion = "PASTILLAS WC", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 3, CentroId = 1, Codigo = "PE21400702", Descripcion = "MANUFACTURA WC LÍQUIDO GRANEL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 4, CentroId = 1, Codigo = "PE21400703", Descripcion = "MANUFACTURA GRANEL HIPOCLORITO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 5, CentroId = 1, Codigo = "PE21400704", Descripcion = "IMPRESIÓN BALDES", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 6, CentroId = 1, Codigo = "PE21400705", Descripcion = "ENVASADO WC LÍQUIDO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 7, CentroId = 1, Codigo = "PE21400706", Descripcion = "ENVASADO HIPOCLORITO CISTERNA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 8, CentroId = 1, Codigo = "PE21420700", Descripcion = "GASTOS INDIRECTOS INTRACLORO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 9, CentroId = 1, Codigo = "PE21420701", Descripcion = "MANTENIMIENTO INTRACLORO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 10, CentroId = 1, Codigo = "PE21420702", Descripcion = "SEGURIDAD INTRACLORO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 11, CentroId = 1, Codigo = "PE21420703", Descripcion = "CALIDAD INTRACLORO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 12, CentroId = 1, Codigo = "PE21420704", Descripcion = "ALMACÉN EEIIREP INTRACLORO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 13, CentroId = 1, Codigo = "PE21430001", Descripcion = "GERENCIA DE LA PRODUCCIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 14, CentroId = 1, Codigo = "PE21430006", Descripcion = "GASTOS COVID", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 15, CentroId = 1, Codigo = "PE21430011", Descripcion = "SEGURIDAD INDUSTRIAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 16, CentroId = 2, Codigo = "PE21300004", Descripcion = "ALMACÉN PT PLANTA 1", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 17, CentroId = 2, Codigo = "PE21300005", Descripcion = "CENTRO DE DISTRIBUCIÓN PRINCIPAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 18, CentroId = 2, Codigo = "PE21400107", Descripcion = "MANUFACTURA SANITARIOS ACUOSOS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 19, CentroId = 2, Codigo = "PE21400114", Descripcion = "ENVASADO WC", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 20, CentroId = 2, Codigo = "PE21400601", Descripcion = "PRODUCCIÓN SOPLADO PET", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 21, CentroId = 2, Codigo = "PE21400602", Descripcion = "PRODUCCIÓN SOPLADO PE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 22, CentroId = 2, Codigo = "PE21400603", Descripcion = "PRODUCCIÓN OFERTADO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 23, CentroId = 2, Codigo = "PE21400604", Descripcion = "PRODUCCIÓN LÍNEA FLEXOGRAFIA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 24, CentroId = 2, Codigo = "PE21400605", Descripcion = "PRODUCCIÓN LÍN BARNIZADO Y LITOGRHOJALAT", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 25, CentroId = 2, Codigo = "PE21400606", Descripcion = "PRODUCCIÓN INYECCIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 26, CentroId = 2, Codigo = "PE21400607", Descripcion = "PRODUCCIÓN ETIQUETADO/CODIFICADO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 27, CentroId = 2, Codigo = "PE21400608", Descripcion = "PRODUCCIÓN ENVASES DE ALUMINIO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 28, CentroId = 2, Codigo = "PE21400609", Descripcion = "PRODUCCIÓN ENSAMBLAJES", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 29, CentroId = 2, Codigo = "PE21400610", Descripcion = "PRODUCCIÓN CEPILLOS DENTALES", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 30, CentroId = 2, Codigo = "PE21400611", Descripcion = "PRODUCCIÓN BOLSAS PLÁSTICAS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 31, CentroId = 2, Codigo = "PE21400612", Descripcion = "MEZCLADO DE RESINAS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 32, CentroId = 2, Codigo = "PE21400613", Descripcion = "MANUFACTURA GRANELES LEJÍA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 33, CentroId = 2, Codigo = "PE21400614", Descripcion = "MANUFACTURA GRANEL LIMPIATOD-LAVALÍQ", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 34, CentroId = 2, Codigo = "PE21400615", Descripcion = "MANUFACTURA GRANAEROSOLES/INSECTICIDA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 35, CentroId = 2, Codigo = "PE21400616", Descripcion = "ENVASADO DE LIMPIATODO Y LAVAVAJILLALÍQ", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 36, CentroId = 2, Codigo = "PE21400617", Descripcion = "ENVASADO DE LEJÍA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 37, CentroId = 2, Codigo = "PE21400618", Descripcion = "ENVASADO DE AEROSOLES/INSECTICIDAS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 38, CentroId = 2, Codigo = "PE21400619", Descripcion = "ENSAMBLE DE ENVASES DE HOJALATA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 39, CentroId = 2, Codigo = "PE21400707", Descripcion = "PROD TUB PASTA DENTAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 40, CentroId = 2, Codigo = "PE21400712", Descripcion = "ENVASADO SANITARIOS ACUOSOS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 41, CentroId = 2, Codigo = "PE21400715", Descripcion = "ENVASADO CERAS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 42, CentroId = 2, Codigo = "PE21420600", Descripcion = "GASTOS INDIRECTOS LURIN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 43, CentroId = 2, Codigo = "PE21420601", Descripcion = "TRATAMIENTO DE AGUA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 44, CentroId = 2, Codigo = "PE21420602", Descripcion = "MANTENIMIENTO LURIN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 45, CentroId = 2, Codigo = "PE21420603", Descripcion = "SEGURIDAD LURIN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 46, CentroId = 2, Codigo = "PE21420604", Descripcion = "CALIDAD LURIN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 47, CentroId = 2, Codigo = "PE21420605", Descripcion = "ALMACÉN EEIIREP LURÍN PRINCIPAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 48, CentroId = 2, Codigo = "PE21430000", Descripcion = "GERENCIA DEL MEDIO AMBIENTE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 49, CentroId = 2, Codigo = "PE21430001", Descripcion = "GERENCIA DE LA PRODUCCIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 50, CentroId = 2, Codigo = "PE21430006", Descripcion = "GASTOS COVID", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 51, CentroId = 2, Codigo = "PE21430011", Descripcion = "SEGURIDAD INDUSTRIAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 52, CentroId = 2, Codigo = "PE21430012", Descripcion = "GERENCIA DE MANTENIMIENTO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 53, CentroId = 2, Codigo = "PE21430015", Descripcion = "MANTENIMIENTO ELÉCTRICO/INSTRUMENTACIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 54, CentroId = 3, Codigo = "PE21400105", Descripcion = "OFERTADO INTRALAB", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 55, CentroId = 3, Codigo = "PE21400200", Descripcion = "PRODUCCIÓN TUBOS PLÁSTICOS COSMÉTICOS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 56, CentroId = 3, Codigo = "PE21400201", Descripcion = "PRODUCCIÓN SULFONACIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 57, CentroId = 3, Codigo = "PE21400202", Descripcion = "ENVASADO ÁCIDO SULFÓNICO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 58, CentroId = 3, Codigo = "PE21400701", Descripcion = "PASTILLA WC", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 59, CentroId = 3, Codigo = "PE21400705", Descripcion = "ENVASADO WC LIQUIDO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 60, CentroId = 3, Codigo = "PE21400708", Descripcion = "OFERTADO PERSONAL CARE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 61, CentroId = 3, Codigo = "PE21400710", Descripcion = "JABON LIQUIDO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 62, CentroId = 3, Codigo = "PE21400713", Descripcion = "PASTA DENTAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 63, CentroId = 3, Codigo = "PE21420200", Descripcion = "GASTOS INDIRECTOS SULFONACIÓN / TUBEPACK", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 64, CentroId = 3, Codigo = "PE21420201", Descripcion = "MANTENIMIENTO  SULFONACIÓN/TUBEPACK", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 65, CentroId = 3, Codigo = "PE21420202", Descripcion = "SEGURIDAD SULFONACIÓN /TUBEPACK", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 66, CentroId = 3, Codigo = "PE21420203", Descripcion = "CALIDAD SULFONACIÓN  /TUBEPACK", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 67, CentroId = 3, Codigo = "PE21420204", Descripcion = "ALMACÉN EEIIREP SULFONACIÓN / TUBEPACK", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 68, CentroId = 3, Codigo = "PE21430001", Descripcion = "GERENCIA DE LA PRODUCCIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 69, CentroId = 3, Codigo = "PE21430006", Descripcion = "GASTOS COVID", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 70, CentroId = 4, Codigo = "PE21300005", Descripcion = "CENTRO DE DISTRIBUCIÓN PRINCIPAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 71, CentroId = 4, Codigo = "PE21400300", Descripcion = "PRODUCCIÓN ENVASES PLÁSTICOS TERMOFORMA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 72, CentroId = 4, Codigo = "PE21400301", Descripcion = "MANUFACTURA POLVO/PULIDOR", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 73, CentroId = 4, Codigo = "PE21400302", Descripcion = "MANUFACTURA LAVAVAJILLA POTE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 74, CentroId = 4, Codigo = "PE21400303", Descripcion = "MANUFACTURA DETERGENTES LURÍN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 75, CentroId = 4, Codigo = "PE21400304", Descripcion = "IMPRESIÓN POTES PLÁSTICOS LURÍN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 76, CentroId = 4, Codigo = "PE21400305", Descripcion = "ENVASADO POLVO/PULIDOR", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 77, CentroId = 4, Codigo = "PE21400306", Descripcion = "ENVASADO LAVAVAJILLAS EN POTE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 78, CentroId = 4, Codigo = "PE21400307", Descripcion = "ENVASADO DETERGENTES LURÍN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 79, CentroId = 4, Codigo = "PE21400506", Descripcion = "ACABADOS FIBRAS, PAÑOS Y OTROS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 80, CentroId = 4, Codigo = "PE21420300", Descripcion = "GASTOS INDIR DETERGENTES / LAVAVAJILLA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 81, CentroId = 4, Codigo = "PE21420301", Descripcion = "MANTENIMIENTO DETERGENTES", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 82, CentroId = 4, Codigo = "PE21420302", Descripcion = "SEGURIDAD DETERGENTES", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 83, CentroId = 4, Codigo = "PE21420303", Descripcion = "CALIDAD DETERGENTES", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 84, CentroId = 4, Codigo = "PE21420304", Descripcion = "ALMACÉN EEIIREP DETERGENTE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 85, CentroId = 4, Codigo = "PE21430000", Descripcion = "GERENCIA DEL MEDIO AMBIENTE", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 86, CentroId = 4, Codigo = "PE21430001", Descripcion = "GERENCIA DE LA PRODUCCIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 87, CentroId = 4, Codigo = "PE21430006", Descripcion = "GASTOS COVID", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 88, CentroId = 4, Codigo = "PE21430011", Descripcion = "SEGURIDAD INDUSTRIAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 89, CentroId = 5, Codigo = "PE21400105", Descripcion = "OFERTADO INTRALAB", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 90, CentroId = 5, Codigo = "PE21400500", Descripcion = "PRODUCCIÓN ENVASES HOJALATA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 91, CentroId = 5, Codigo = "PE21400501", Descripcion = "PRODUCCIÓN DE DOMOS Y FONDOS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 92, CentroId = 5, Codigo = "PE21400502", Descripcion = "PRODUCCIÓN DE CORTE DE BOBINAS DE HOJAL", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 93, CentroId = 5, Codigo = "PE21400503", Descripcion = "PRODUCCIÓN DE BARNIZADO Y LITOGRAFÍA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 94, CentroId = 5, Codigo = "PE21400504", Descripcion = "MANUFACTURA PAÑO AMARILLO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 95, CentroId = 5, Codigo = "PE21400505", Descripcion = "MANUFACTURA FIBRA", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 96, CentroId = 5, Codigo = "PE21400506", Descripcion = "ACABADOS FIBRAS, PAÑOS Y OTROS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 97, CentroId = 5, Codigo = "PE21400705", Descripcion = "ENVASADO WC LIQUIDO", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 98, CentroId = 5, Codigo = "PE21400714", Descripcion = "ENV. COSMETIC. ACUOSOS", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 99, CentroId = 5, Codigo = "PE21420500", Descripcion = "GASTOS INDIRECTOS INTRACAN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 100, CentroId = 5, Codigo = "PE21420502", Descripcion = "MANTENIMIENTO INTRACAN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 101, CentroId = 5, Codigo = "PE21420503", Descripcion = "SEGURIDAD INTRACAN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 102, CentroId = 5, Codigo = "PE21420504", Descripcion = "CALIDAD INTRACAN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 103, CentroId = 5, Codigo = "PE21420505", Descripcion = "ALMACÉN EEIIREP INTRACAN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 104, CentroId = 5, Codigo = "PE21430001", Descripcion = "GERENCIA DE LA PRODUCCIÓN", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 105, CentroId = 5, Codigo = "PE21430006", Descripcion = "GASTOS COVID", Activo = true, CreadoEn = Origen },
            new CentroCosto { Id = 106, CentroId = 5, Codigo = "PE21430011", Descripcion = "SEGURIDAD INDUSTRIAL", Activo = true, CreadoEn = Origen });
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
        //
        // Los topes de días también son dato del catálogo, no reglas cableadas: descanso
        // médico se agota por año calendario (20 días, sumando todos los certificados);
        // las licencias paternal y por maternidad se agotan por evento (10 días por hijo,
        // 98 —catorce semanas— por parto), así que dos eventos en el mismo año dan derecho
        // a dos licencias completas. El resto de tipos no tiene tope.
        modelBuilder.Entity<TipoAusencia>().HasData(
            new TipoAusencia { Id = 1, Codigo = "VAC", Nombre = "Vacaciones", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 2, Codigo = "DM", Nombre = "Descanso médico", Comportamiento = ComportamientoAusencia.Subsidiada, ComputaComoAsistencia = true, RequiereSustento = true, MaximoDiasPorAnioCalendario = 20, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 3, Codigo = "LCG", Nombre = "Licencia con goce", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 4, Codigo = "LSG", Nombre = "Licencia sin goce", Comportamiento = ComportamientoAusencia.JustificadaNoRemunerada, ComputaComoAsistencia = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 5, Codigo = "PER", Nombre = "Permiso", Comportamiento = ComportamientoAusencia.PermisoParcial, ComputaComoAsistencia = true, EsPorHoras = true, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 6, Codigo = "LPAT", Nombre = "Licencia paternal", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, RequiereSustento = true, MaximoDiasPorEvento = 10, Activo = true, CreadoEn = Origen },
            new TipoAusencia { Id = 7, Codigo = "LMAT", Nombre = "Licencia por maternidad", Comportamiento = ComportamientoAusencia.JustificadaRemunerada, ComputaComoAsistencia = true, RequiereSustento = true, MaximoDiasPorEvento = 98, Activo = true, CreadoEn = Origen },
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

    /// <summary>
    /// Valores legales absolutos: RMV, porcentaje de asignación familiar y movilidad diaria.
    /// </summary>
    /// <remarks>
    /// Mismo criterio que las tasas: son <b>datos con vigencia por fecha</b>. Cuando el
    /// Estado suba la RMV no se edita esta fila —eso reescribiría los periodos ya
    /// liquidados—, se le pone <c>VigenteHasta</c> y se inserta la siguiente.
    ///
    /// ⚠️ La RMV arranca el 01/01/2025 porque esa es la fecha real del decreto. Los otros
    /// dos arrancan en <see cref="Origen"/>, que es desde cuándo existen datos en este
    /// sistema; para una fecha anterior no habrá parámetro vigente, y quien los consulte
    /// debe tratar esa ausencia como un dato faltante y no como un cero silencioso.
    /// </remarks>
    private static void SembrarParametrosLegales(ModelBuilder modelBuilder)
    {
        // Las tres filas comparten fecha de vigencia (2025-01-01), aunque solo la RMV tiene
        // ahí su cambio normativo real: si asignación familiar y movilidad arrancaran en la
        // fecha genérica de origen del seed (2026-01-01), liquidar cualquier periodo anterior
        // se quedaría sin parámetro vigente y sin avisar — el mismo tipo de falla silenciosa
        // que este sistema existe para evitar.
        var desde = new DateOnly(2025, 1, 1);

        modelBuilder.Entity<ParametroLegal>().HasData(
            new ParametroLegal
            {
                Id = 1,
                Tipo = TipoParametroLegal.RemuneracionMinimaVital,
                Valor = 1130.00m,
                VigenteDesde = desde,
                BaseLegal = "D.S. 006-2024-TR",
                Activo = true,
                CreadoEn = Origen
            },
            new ParametroLegal
            {
                Id = 2,
                Tipo = TipoParametroLegal.PorcentajeAsignacionFamiliar,
                Valor = 10.00m,
                VigenteDesde = desde,
                BaseLegal = "Ley 25129",
                Activo = true,
                CreadoEn = Origen
            },
            new ParametroLegal
            {
                Id = 3,
                Tipo = TipoParametroLegal.ValorMovilidadDiaria,
                Valor = 3.00m,
                VigenteDesde = desde,
                // No es normativa: es política interna de GCA. Usa el mismo mecanismo de
                // vigencia porque cambia con el tiempo igual que una tasa legal, y el
                // pasado tampoco debe moverse cuando cambie.
                BaseLegal = "Política interna GCA",
                Activo = true,
                CreadoEn = Origen
            });
    }

    private static void SembrarPeriodos(ModelBuilder modelBuilder)
    {
        // El periodo de julio 2026 es el del juego de datos de prueba del ZKTeco
        // (`docs/04-juego-de-datos-julio-2026.md`). Nace abierto: el ciclo de cierre
        // lo gobierna el mantenimiento de periodos.
        modelBuilder.Entity<Periodo>().HasData(
            new Periodo
            {
                Id = 1,
                Codigo = "2026-07",
                Nombre = "Julio 2026",
                FechaInicio = new DateOnly(2026, 7, 1),
                FechaFin = new DateOnly(2026, 7, 31),
                Estado = EstadoPeriodo.Abierto,
                Activo = true,
                CreadoEn = Origen
            });
    }
}

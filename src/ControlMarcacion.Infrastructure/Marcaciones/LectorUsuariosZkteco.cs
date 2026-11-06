using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Marcaciones;

namespace ControlMarcacion.Infrastructure.Marcaciones;

/// <summary>Lector acotado y sin persistencia del formato conocido de <c>user.u</c>.</summary>
internal sealed class LectorUsuariosZkteco : ILectorUsuariosZkteco
{
    private const byte TablaUsuarios = 1;
    private const byte CantidadCampos = 11;
    private static readonly byte[] CamposConocidos = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    public ResultadoLecturaUsuariosZkteco Leer(
        Stream contenido,
        EquipoOrigenUsuariosZkteco equipo,
        OpcionesLecturaUsuariosZkteco? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        ArgumentNullException.ThrowIfNull(equipo);
        opciones ??= new OpcionesLecturaUsuariosZkteco();

        if (equipo.EquipoId <= 0 || string.IsNullOrWhiteSpace(equipo.Codigo))
        {
            throw new ArgumentException("Debe indicarse el equipo seleccionado.", nameof(equipo));
        }

        if (opciones.MaximoBytes < 13 || opciones.MaximoRegistros <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opciones), "Los límites de lectura no son válidos.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(checked(opciones.MaximoBytes + 1));
        var usados = 0;
        try
        {
            while (usados <= opciones.MaximoBytes)
            {
                var leidos = contenido.Read(
                    buffer,
                    usados,
                    Math.Min(buffer.Length - usados, opciones.MaximoBytes + 1 - usados));
                if (leidos == 0)
                {
                    break;
                }

                usados += leidos;
            }

            if (usados > opciones.MaximoBytes)
            {
                return Rechazo(
                    equipo,
                    $"El archivo supera el límite configurado de {opciones.MaximoBytes} bytes.",
                    string.Empty,
                    usados,
                    DateTime.UtcNow);
            }

            var datos = buffer.AsSpan(0, usados);
            var huella = Convert.ToHexString(SHA256.HashData(datos));
            var leidoEnUtc = DateTime.UtcNow;
            return Interpretar(datos, equipo, opciones.MaximoRegistros, huella, usados, leidoEnUtc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer.AsSpan(0, usados));
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ResultadoLecturaUsuariosZkteco Interpretar(
        ReadOnlySpan<byte> datos,
        EquipoOrigenUsuariosZkteco equipo,
        int maximoRegistros,
        string huella,
        long tamanoBytes,
        DateTime leidoEnUtc)
    {
        if (datos.IsEmpty)
        {
            return Rechazo(equipo, "El archivo está vacío.", huella, tamanoBytes, leidoEnUtc);
        }

        if (datos.Length < 13)
        {
            return Rechazo(equipo, "El encabezado está truncado.", huella, tamanoBytes, leidoEnUtc);
        }

        if (datos[0] != TablaUsuarios || datos[1] != CantidadCampos ||
            !datos.Slice(2, CantidadCampos).SequenceEqual(CamposConocidos))
        {
            return Rechazo(equipo, "El archivo no corresponde al formato user.u conocido (tabla 1, campos 1..11).", huella, tamanoBytes, leidoEnUtc);
        }

        var registros = new List<RegistroTemporal>();
        var posicion = 13;
        while (posicion < datos.Length)
        {
            if (registros.Count >= maximoRegistros)
            {
                return Rechazo(equipo, $"El archivo supera el límite configurado de {maximoRegistros} registros.", huella, tamanoBytes, leidoEnUtc);
            }

            var campos = new ReadOnlyMemory<byte>[CantidadCampos];
            for (var campo = 0; campo < CantidadCampos; campo++)
            {
                if (posicion >= datos.Length)
                {
                    return Rechazo(equipo, $"El registro {registros.Count + 1} está truncado antes del campo {campo + 1}.", huella, tamanoBytes, leidoEnUtc);
                }

                var longitud = datos[posicion++];
                if (posicion + longitud > datos.Length)
                {
                    return Rechazo(equipo, $"El registro {registros.Count + 1}, campo {campo + 1}, está truncado.", huella, tamanoBytes, leidoEnUtc);
                }

                // Solo se copian campos permitidos. Password (4) y demás valores sensibles
                // nunca se convierten en texto ni forman parte del resultado.
                if (campo is 0 or 1 or 2 or 7 or 8)
                {
                    campos[campo] = datos.Slice(posicion, longitud).ToArray();
                }

                posicion += longitud;
            }

            registros.Add(CrearTemporal(registros.Count + 1, campos));
        }

        MarcarDuplicados(registros);
        var usuarios = registros.Select(Finalizar).ToArray();
        var estado = usuarios.Any(x => x.Estado != EstadoRegistroUsuarioZkteco.Leido)
            ? EstadoArchivoUsuariosZkteco.ValidoConAdvertencias
            : EstadoArchivoUsuariosZkteco.Valido;

        return new(equipo, estado, usuarios, [], "user.u/table=1/fields=1..11", huella, tamanoBytes, leidoEnUtc);
    }

    private static RegistroTemporal CrearTemporal(int indice, ReadOnlyMemory<byte>[] campos)
    {
        var incidencias = new HashSet<IncidenciaUsuarioZkteco>();
        var uid = LeerEntero(campos[0].Span);
        var tieneTarjeta = campos[1].Span.IndexOfAnyExcept((byte)0) >= 0;
        var pin = LeerTexto(campos[2].Span, soloAscii: true, out var pinValido);
        var nombre = LeerTexto(campos[7].Span, soloAscii: false, out var nombreValido);
        var privilegio = LeerEntero(campos[8].Span) ?? 0;

        if (!pinValido || !nombreValido)
        {
            incidencias.Add(IncidenciaUsuarioZkteco.BytesDeTextoInvalidos);
        }

        if (string.IsNullOrEmpty(pin))
        {
            incidencias.Add(IncidenciaUsuarioZkteco.PinAusente);
        }
        else if (!pin.All(char.IsDigit))
        {
            incidencias.Add(IncidenciaUsuarioZkteco.PinNoNumerico);
        }

        if (string.IsNullOrEmpty(nombre))
        {
            incidencias.Add(IncidenciaUsuarioZkteco.NombreAusente);
        }
        else
        {
            incidencias.Add(IncidenciaUsuarioZkteco.NombreCandidatoNoVerificado);
            if (nombre.Length != 8)
            {
                incidencias.Add(IncidenciaUsuarioZkteco.NombreNoTieneOchoCaracteres);
            }
        }

        if (privilegio != 0)
        {
            incidencias.Add(IncidenciaUsuarioZkteco.UsuarioAdministrador);
        }

        return new(indice, uid, pin ?? string.Empty, nombre, tieneTarjeta, privilegio != 0, incidencias, pinValido, nombreValido);
    }

    private static void MarcarDuplicados(List<RegistroTemporal> registros)
    {
        foreach (var grupo in registros.Where(x => x.Pin.Length > 0).GroupBy(x => x.Pin).Where(x => x.Count() > 1))
        {
            foreach (var registro in grupo)
            {
                registro.Incidencias.Add(IncidenciaUsuarioZkteco.PinDuplicado);
            }
        }

        foreach (var grupo in registros.Where(x => !string.IsNullOrEmpty(x.Nombre)).GroupBy(x => x.Nombre!).Where(x => x.Count() > 1))
        {
            foreach (var registro in grupo)
            {
                registro.Incidencias.Add(IncidenciaUsuarioZkteco.NombreDuplicado);
            }
        }
    }

    private static UsuarioZktecoLeido Finalizar(RegistroTemporal registro)
    {
        var invalido = registro.Pin.Length == 0 || !registro.PinValido || !registro.NombreValido;
        var estado = invalido
            ? EstadoRegistroUsuarioZkteco.Invalido
            : registro.Incidencias.Count == 0
                ? EstadoRegistroUsuarioZkteco.Leido
                : EstadoRegistroUsuarioZkteco.LeidoConAdvertencias;
        var candidato = !registro.NombreValido
            ? EstadoCandidatoIdentidadZkteco.NoInterpretable
            : string.IsNullOrEmpty(registro.Nombre)
                ? EstadoCandidatoIdentidadZkteco.Ausente
                : registro.Incidencias.Contains(IncidenciaUsuarioZkteco.NombreDuplicado)
                    ? EstadoCandidatoIdentidadZkteco.Ambiguo
                    : EstadoCandidatoIdentidadZkteco.NoVerificado;

        return new(
            registro.Indice,
            registro.Uid,
            registro.Pin,
            registro.Nombre,
            registro.TieneTarjeta,
            registro.EsAdministrador,
            estado,
            candidato,
            registro.Incidencias.OrderBy(x => x).ToArray());
    }

    private static ulong? LeerEntero(ReadOnlySpan<byte> valor)
    {
        if (valor.IsEmpty)
        {
            return null;
        }

        if (valor.Length > sizeof(ulong))
        {
            return null;
        }

        Span<byte> normalizado = stackalloc byte[sizeof(ulong)];
        valor.CopyTo(normalizado);
        return BinaryPrimitives.ReadUInt64LittleEndian(normalizado);
    }

    private static string? LeerTexto(ReadOnlySpan<byte> valor, bool soloAscii, out bool valido)
    {
        if (valor.IsEmpty)
        {
            valido = true;
            return null;
        }

        valido = true;
        foreach (var caracter in valor)
        {
            if (caracter < 32 || caracter == 127 || (soloAscii && caracter > 126))
            {
                valido = false;
                break;
            }
        }

        return valido ? Encoding.Latin1.GetString(valor) : null;
    }

    private static ResultadoLecturaUsuariosZkteco Rechazo(
        EquipoOrigenUsuariosZkteco equipo,
        string error,
        string huella,
        long tamanoBytes,
        DateTime leidoEnUtc) =>
        new(equipo, EstadoArchivoUsuariosZkteco.Rechazado, [], [error], "no reconocido", huella, tamanoBytes, leidoEnUtc);

    private sealed record RegistroTemporal(
        int Indice,
        ulong? Uid,
        string Pin,
        string? Nombre,
        bool TieneTarjeta,
        bool EsAdministrador,
        HashSet<IncidenciaUsuarioZkteco> Incidencias,
        bool PinValido,
        bool NombreValido);
}

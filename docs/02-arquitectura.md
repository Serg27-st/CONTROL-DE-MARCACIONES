# Arquitectura

## Estilo

**Monolito modular en 4 capas**, con dependencias hacia adentro. No es microservicios y no
debe serlo: un sistema de tareo para cuatro plantas no tiene el problema de escala que
justifique el costo operativo de distribuirlo.

```
ControlMarcacion.Web            → Application, Infrastructure, Domain
ControlMarcacion.Infrastructure → Application, Domain
ControlMarcacion.Application    → Domain
ControlMarcacion.Domain         → (nada)
```

| Capa | Responsabilidad |
|---|---|
| `Domain` | Entidades POCO, enums, value objects, cálculo puro, interfaces de repositorio |
| `Application` | Servicios de caso de uso, DTOs, validación, orquestación transaccional |
| `Infrastructure` | `DbContext`, Fluent API, repositorios, lector del TXT, exportadores |
| `Web` | Controladores, vistas Razor, Identity, composición de dependencias |

### Por qué `Domain` no referencia a nadie

Es la decisión estructural más importante del proyecto. El motor de cálculo del tareo es la
pieza con más casos borde y la que más va a cambiar mientras se afinan las reglas. Si depende
de EF Core, cada prueba necesita una base de datos, y probar catorce escenarios de turno
nocturno se vuelve tan lento que se deja de hacer.

Manteniendo `Domain` puro, el cálculo se prueba en memoria, en milisegundos, con fechas
literales. Esa es la diferencia entre un motor con cobertura real y uno que se valida a mano.

## Módulos

Carpeta por módulo dentro de cada capa:

`Seguridad` · `Organizacion` · `Personal` · `Catalogos` · `Marcaciones` · `Solicitudes` ·
`Justificaciones` · `Asistencia` · `Reportes`

### Reglas de frontera

1. Un módulo llama a otro **solo por sus servicios públicos de `Application`**. Nunca por su
   repositorio ni su `DbSet`.
2. `Asistencia` y `Reportes` dependen de los módulos base. Nunca al revés.
3. Si dos módulos se necesitan mutuamente, el diseño está mal: extrae lo común a `Domain` o
   invierte la dependencia con una interfaz.

## Flujo de datos principal

```
Archivo TXT (USB, ZKTeco G3)
        │
        ▼
  LectorTxtZkteco  ──── adaptador en Infrastructure
        │
        ▼
  IFuenteDeMarcaciones ─── validación, deduplicación, resolución de trabajador
        │
        ▼
   marcacion_cruda  ◄──── INMUTABLE. Nunca se edita ni se borra.
        │
        ├──────────────┐
        ▼              ▼
   Turno vigente   Solicitudes de ausencia
        │              │
        └──────┬───────┘
               ▼
        CalculadoraTareo  ──── función pura en Domain
               │
               ▼
         tareo_diario
               │
               ▼
        Justificaciones ◄──── editable mientras el periodo esté abierto
               │
               ▼
          Tareo final
```

La ingesta está detrás de una interfaz precisamente para que el día que el marcador se conecte
por red, sea un adaptador nuevo y no una reescritura del módulo.

## Modelo temporal

Cuatro entidades del sistema son **temporales**, no referencias simples. Todas llevan
`FechaInicio` / `FechaFin`:

| Entidad | Por qué tiene vigencia |
|---|---|
| `AsignacionTurno` | Los obreros rotan de turno por calendario |
| `AsignacionMarcador` | El ID del equipo se reasigna a otra persona cuando alguien cesa |
| `TasaSobretasa` | Recalcular un periodo antiguo debe usar las tasas de entonces |
| `Periodo` | Define qué rango de fechas está abierto a modificación |

Ignorar esto es la vía más rápida a un tareo histórico corrompido: si el ID de marcador se
resolviera por la asignación actual, reasignarlo reatribuiría todas las marcaciones pasadas
del ocupante anterior.

## Persistencia

- **SQL Server** con EF Core, migraciones versionadas.
- Entidades sin atributos de EF; todo el mapeo en `IEntityTypeConfiguration<T>`.
- Nombres físicos en snake_case y español. Enums persistidos como texto.
- Tiempos en **minutos enteros**, importes en `DECIMAL`.
- Consultas de lectura con `AsNoTracking()`; reportes con proyección a DTO.

## Presentación

ASP.NET Core MVC con vistas Razor renderizadas en servidor. Sin SPA y sin build de JavaScript:
es un panel administrativo interno y el costo de una capa cliente separada no se justifica.

Los assets se sirven **localmente** desde `wwwroot/`: la red de planta puede no tener salida a
internet, y un panel que depende de un CDN deja de funcionar justo donde tiene que operar.

## Decisiones registradas

Las decisiones costosas de revertir se documentan como ADR en `docs/adr/`. Ver
[requerimientos de negocio](01-requerimientos-negocio.md) para el estado de las decisiones
abiertas y cerradas.

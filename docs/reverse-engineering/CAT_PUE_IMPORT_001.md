# CAT-PUE-IMPORT-001 — Creación controlada de puestos desde personal

## Problema original

La carga masiva resolvía `puesto` únicamente contra filas activas ya existentes. Un valor
nuevo rechazaba al trabajador y obligaba a mantener el catálogo manualmente antes de cada
importación. La pantalla permitía además cargar directamente, sin una confirmación posterior
a la validación.

## Inventario comprobado

- Entidad: `ControlMarcacion.Domain.Catalogos.Puesto`.
- Tabla física SQLite: `puesto`.
- Alcance: `Puesto.EmpresaId` nullable. Primero se resuelve dentro de la empresa y, si no
  existe coincidencia allí, se permite reutilizar un puesto universal (`EmpresaId = null`).
- Restricción física: índice único global `ix_puesto_codigo` sobre `puesto.codigo`.
- Catálogo de consulta: `ConsultaCatalogos.PuestosAsync`.
- Resolución para carga: `CatalogosResueltos`.
- Importador: `CargaMasivaTrabajadores` mediante `ICargaMasivaTrabajadores`.
- Entrada web: `TrabajadoresController` y `Views/Trabajadores/CargaMasiva.cshtml`.
- Permisos: autorización del controlador para `COORDINADOR` y `ADMINISTRADOR`.
- Auditoría: `InterceptorAuditoria`; `puesto` no está excluida y registra inserción, actor,
  clave creada y campos no protegidos.

## Flujo anterior

```text
Excel/CSV
→ lectura y mapa de columnas
→ carga de catálogos activos
→ resolución obligatoria del puesto
→ validación/alta o actualización del trabajador
→ SaveChanges por fila dentro de una transacción global
→ commit, o rollback en “validar sin guardar”
```

Las filas válidas se guardaban aunque otras fueran rechazadas. La creación opcional de IDs
de marcador ya tenía esa misma semántica parcial y se conserva.

## Flujo nuevo

```text
Excel/CSV
→ análisis sin persistencia final
→ conciliación de puestos por empresa/universal
→ resumen de existentes, nuevos y ambiguos
→ archivo exacto retenido temporalmente en memoria del servidor
→ token opaco de un solo uso ligado al usuario
→ Confirmar
→ relectura del archivo + nueva conciliación del catálogo
→ comparación de huella
→ creación del puesto y primer trabajador en la misma transacción
→ commit
```

Una huella distinta rechaza la confirmación y exige volver a analizar. Cancelar o consumir el
token lo elimina. El token vence después de 20 minutos y no contiene datos de la plantilla.

## Reglas implementadas

1. Se reutiliza una coincidencia activa de la empresa; si no existe, se busca una universal.
2. Dos o más coincidencias dentro del alcance elegido se consideran ambiguas y bloquean la
   confirmación completa.
3. Un valor nuevo se propone en la vista previa y solo se confirma mediante la segunda acción.
4. La clave de comparación recorta extremos, colapsa whitespace y no distingue mayúsculas.
   Conserva tildes y caracteres significativos.
5. El nombre visible nuevo conserva tildes y mayúsculas del primer valor, con espacios
   normalizados.
6. `SIN PUESTO`, `VACÍO`, `VACIO`, `N/A` y `DESCONOCIDO` se rechazan; `puesto` continúa
   siendo obligatorio por el contrato previo.
7. Las repeticiones de un mismo puesto dentro del lote se concilian a una sola fila.
8. El código nuevo es determinista: `PUE-` más 16 dígitos hexadecimales derivados de
   `(EmpresaId, clave normalizada)`. Mide exactamente 20 caracteres.
9. El índice global existente sobre `codigo` sigue siendo el árbitro físico. Una exclusión
   asíncrona evita carreras entre importaciones del mismo proceso local.
10. No se reactivan filas inactivas ni se modifican puestos, periodos o asignaciones previas.

## Atomicidad y errores

La política previa de éxitos parciales permanece. Para un puesto nuevo se crea un savepoint
de fila, se obtiene primero su ID definitivo y luego se guarda el trabajador. Si la regla del
trabajador rechaza la fila, se revierte el savepoint y se limpia el seguimiento EF: el puesto
y su auditoría no quedan huérfanos. Un fallo no controlado revierte la transacción global.

La previsualización ejecuta las validaciones reales dentro de una transacción revertida. Al
terminar no existen trabajadores, marcadores, puestos ni auditorías nuevos.

## Archivos modificados

- `src/ControlMarcacion.Application/Personal/Dto/CargaMasivaDtos.cs`
- `src/ControlMarcacion.Application/Personal/ICargaMasivaTrabajadores.cs`
- `src/ControlMarcacion.Infrastructure/Personal/CatalogosResueltos.cs`
- `src/ControlMarcacion.Infrastructure/Personal/CargaMasivaTrabajadores.cs`
- `src/ControlMarcacion.Web/Controllers/TrabajadoresController.cs`
- `src/ControlMarcacion.Web/Models/TrabajadorViewModels.cs`
- `src/ControlMarcacion.Web/Program.cs`
- `src/ControlMarcacion.Web/Views/Trabajadores/CargaMasiva.cshtml`
- `tests/ControlMarcacion.Infrastructure.Tests/Personal/CargaMasivaCreacionPuestosTests.cs`
- `tests/ControlMarcacion.Web.Tests/Trabajadores/CargaMasivaPuestosMvcTests.cs`
- `docs/reverse-engineering/CAT_PUE_IMPORT_001.md`

## Pruebas

Los contratos `PUEIMP-01` a `PUEIMP-12` cubren reutilización, creación y enlace real,
deduplicación, normalización, valores inválidos, rollback, concurrencia, idempotencia,
preservación histórica, vista previa sin escritura, autorización/antiforgery y auditoría.
Se agregó además el caso explícito de ambigüedad. Persistencia, transacciones, unicidad y
concurrencia usan SQLite real, no EF InMemory.

Resultados de cierre:

- Contratos SQLite de creación de puestos: 16/16 verdes.
- Contrato MVC de autorización y antiforgery: 1/1 verde.
- Regresión dirigida del importador: 35/35 verdes.
- Suite Release completa: 1011/1011 verdes, 0 fallos, 0 omitidos.
- Build Release: 0 errores, 0 advertencias.

## Migración

No hay migración. El catálogo, `EmpresaId`, la longitud de `codigo` y el índice único global
existentes son suficientes.

## Limitaciones y riesgos residuales

- La previsualización pendiente vive en memoria del proceso. Reiniciar la aplicación la
  invalida de forma segura y obliga a volver a subir el archivo.
- La exclusión de concurrencia es por proceso. El despliegue local impide instancias
  simultáneas y el índice físico sigue siendo la última defensa frente a otro escritor.
- Una colisión criptográfica del código determinista se muestra como ambigüedad; nunca se
  reutiliza arbitrariamente la fila que ya ocupa ese código.
- No se incorporó administración automática de puestos inactivos: deben corregirse desde el
  catálogo con una decisión humana.

## Estado operativo

Producción no fue tocada. No se ejecutó OP-03, no se aplicó ninguna migración y no se creó
commit.

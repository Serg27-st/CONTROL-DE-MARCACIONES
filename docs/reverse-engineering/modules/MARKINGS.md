# Módulo Marcaciones

## Propósito

Ingerir archivos ZKTeco, conservar origen y errores, almacenar marcas crudas y atribuirlas dinámicamente al trabajador correcto.

## Responsabilidades

- Detectar TXT o respaldo binario por contenido.
- Extraer serie/equipo y validar origen.
- Evitar cargas incompatibles o anuladas.
- Persistir cabecera, errores y marcas crudas.
- Resolver trabajador por equipo + identificador + fecha.
- Evaluar cobertura de carga por aparato.

## Entidades

`ImportacionMarcador`, `MarcacionCruda`, `AtribucionMarcaciones` y `CoberturaDeCarga`.

## Servicios

`ServicioImportacion`, `LectorMarcadorAutomatico`, `LectorTxtZkteco`, `LectorRespaldoZkteco`, `DetectorFormatoMarcador` y `SerieDelArchivo`.

## Reglas

- La extensión no es fuente suficiente del formato.
- La marca cruda es evidencia; la atribución se resuelve por fecha.
- Una carga anulada no cuenta para cobertura ni tareo.
- Identificadores iguales en equipos diferentes no deben colisionar.
- La identidad técnica del equipo y el origen verificado forman parte del control.
- La unidad persistente principal es atómica: serie aprendida, cabecera, errores, auditoría y marcas se confirman o revierten juntas ante un fallo técnico.
- Una línea inválida es parcialidad funcional intencional: se registra como error y no impide confirmar las marcas válidas del archivo.

## Flujo

Archivo → detección → lectura → validación de equipo/origen → cabecera/errores → marcas crudas → atribución dinámica → cálculo de jornada.

Véase [D07](../diagrams/D07_ZKTECO_IMPORT_FLOW.md) y [D08](../diagrams/D08_MARKING_TO_WORKDAY.md).

## Fuentes de verdad

- Archivo importado para datos de origen.
- `MarcacionCruda` para evidencia persistida.
- Asignación de marcador/equipo y fecha para atribución.
- Importación no anulada para cobertura.

## Archivos críticos

- `src/ControlMarcacion.Infrastructure/Marcaciones/ServicioImportacion.cs`
- `src/ControlMarcacion.Infrastructure/Marcaciones/LectorMarcadorAutomatico.cs`
- `src/ControlMarcacion.Domain/Marcaciones/AtribucionMarcaciones.cs`
- `src/ControlMarcacion.Domain/Marcaciones/CoberturaDeCarga.cs`
- `src/ControlMarcacion.Web/Controllers/MarcacionesController.cs`

## Pruebas

Lectores TXT/DAT, detección de formato, serie, colisión entre equipos, atribución al calcular, origen, cobertura y anulación.

VIG-01 confirma que una `MarcacionCruda` relevante cargada después del cálculo invalida
el tareo. La detección la vincula al alcance mediante `(EquipoId, IdMarcador)` y una
`AsignacionMarcador` aplicable de sus trabajadores; la
anulación/reactivación sigue observándose en la cabecera de importación. Cambiar datos
descriptivos del equipo no invalida porque el motor no los consume.

## Problemas conocidos

- RESUELTO/R-P1-07: los `SaveChanges` necesarios están dentro de una única transacción SQLite iniciada después de lectura, parsing, validación y dedupe. IMP-AT-01..07 cubren rollback y camino positivo.
- RIESGO: la base canónica no aporta muestra representativa para medir el flujo principal.
- DOCUMENTACIÓN: distinguir siempre importación principal de PMV.
- COMPORTAMIENTO VIGENTE: repetir el mismo archivo en el mismo equipo crea una nueva cabecera de trazabilidad con cero marcas nuevas; el hash lo detecta y la clave única evita duplicar `MarcacionCruda`. Cambiar esa política no forma parte de atomicidad.

## Relación con PMV

Comparte lectores, pero no tablas ni efectos. PMV no debe crear `ImportacionMarcador`, `MarcacionCruda` o `TareoDiario`.

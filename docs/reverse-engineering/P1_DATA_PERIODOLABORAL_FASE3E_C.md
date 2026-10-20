# P1-DATA-PERIODOLABORAL — Fase 3E-C

## Flujo Application/UI de reingreso

Estado: **COMPLETADA**  
Fecha: 2026-10-15  
Alcance: caso de uso explícito de reingreso en Application y Web, sin cambios de
esquema, migraciones ni reglas históricas.

## 1. Baseline autoritativo

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 419/419 |
| Infrastructure | 483/483 |
| Web | 23/23 |
| Total | 925/925 verdes; 0 fallos; 0 omitidos |

## 2. Inventario mínimo

| Componente | Archivo | Responsabilidad relevante |
|---|---|---|
| Dominio | `Domain/Personal/Trabajador.cs` | `Reingresar(DateOnly, long, long, string)` |
| Repositorio | `Domain/Personal/ITrabajadorRepository.cs` y su implementación | carga del agregado completo y persistencia selectiva |
| Application | `Application/Personal/ServicioTrabajadores.cs` | fachada del caso de uso y alcance |
| Contrato | `Application/Personal/Dto/HistorialTrabajadorDtos.cs` | datos mínimos del reingreso |
| Web | `Web/Controllers/TrabajadoresController.cs` | GET/POST, antiforgery y PRG |
| ViewModel | `Web/Models/TrabajadorViewModels.cs` | cabecera, catálogos y disponibilidad canónica |
| Vista | `Web/Views/Trabajadores/Reingresar.cshtml` | formulario específico |
| Entrada UI | `Web/Views/Trabajadores/Historial.cshtml` | botón visible solo sin periodo abierto |

## 3. Dominio reutilizado

Firma existente:

```csharp
PeriodoLaboral Trabajador.Reingresar(
    DateOnly fechaIngreso,
    long empresaId,
    long plantaId,
    string usuario)
```

La operación ya:

- rechaza cualquier `PeriodoLaboral` abierto;
- valida que el nuevo periodo no solape la historia;
- crea un `PeriodoLaboral` abierto con la empresa indicada;
- crea su `AsignacionPlanta` inicial desde la misma fecha;
- sincroniza `FechaIngreso`, `FechaCese`, `EmpresaId` y `PlantaId`;
- limpia motivo y observación del cese actual;
- conserva todos los vínculos anteriores.

Application no replica esas reglas.

## 4. Caso de uso Application

Método agregado:

```csharp
Task ServicioTrabajadores.ReingresarAsync(
    ReingresarTrabajadorRequest solicitud,
    string usuario,
    CancellationToken cancelacion = default)
```

Secuencia:

1. carga el agregado con todos sus periodos y asignaciones de planta;
2. aplica el mismo alcance por planta usado por las demás escrituras de Personal;
3. comprueba que la planta destino esté dentro del alcance del usuario;
4. valida que empresa y planta existan y correspondan entre sí;
5. invoca `Trabajador.Reingresar`;
6. valida la línea temporal completa;
7. persiste raíz y nuevas entidades con un solo `SaveChanges`.

`TrabajadorRepository.Actualizar` marca solo la raíz. EF detecta los dos hijos nuevos, pero
los periodos y asignaciones cerrados cargados para validar solapamientos permanecen
`Unchanged`.

El índice `UQ_periodo_laboral_abierto` sigue siendo la defensa ante dos POST concurrentes.
`GuardarReingresoAsync` traduce únicamente una colisión SQLite de restricción a un mensaje
funcional que pide actualizar el historial; otros fallos técnicos no se ocultan.

## 5. Contrato y datos

`ReingresarTrabajadorRequest` contiene exclusivamente:

- `TrabajadorId`;
- `FechaIngreso`;
- `EmpresaId`;
- `PlantaId`.

No se incorporaron turno, marcador, sueldo, área, puesto ni centro de costo. Continúan en
sus operaciones existentes.

## 6. Flujo Web

Acciones:

- `GET Trabajadores/Reingresar/{id}`;
- `POST Trabajadores/RegistrarReingreso`.

La pantalla usa el patrón vigente de Personal:

- roles `COORDINADOR` y `ADMINISTRADOR` heredados del controlador;
- antiforgery en el POST;
- errores de dominio en `ModelState`, sin stack traces;
- POST válido → redirect → historial;
- formulario específico con solo los tres datos editables.

El botón `Reingresar` está en el historial y su condición es:

```text
no existe PeriodoLaboral abierto
```

No consulta `Trabajador.FechaCese` para decidir su visibilidad. El backend vuelve a validar
la misma invariante mediante el dominio, por lo que ocultar o falsificar el botón no evita
el control.

## 7. Atomicidad, historia y proyección

Caso validado:

```text
Periodo A: 2025-01-01 → 2025-12-31
Gap:       2026-01-01 → 2026-01-31
Periodo B: 2026-02-01 → abierto
```

El periodo A y su asignación de planta mantienen todos sus valores y metadatos. El gap no
se rellena. El periodo B registra la empresa nueva y una asignación de planta abierta desde
2026-02-01.

Proyección tras el reingreso:

| Campo | Resultado |
|---|---|
| `FechaIngreso` | nueva fecha |
| `FechaCese` | `null` |
| `EmpresaId` | empresa del nuevo vínculo |
| `PlantaId` | planta inicial del nuevo vínculo |

Un interceptor de prueba falla durante el INSERT de `AsignacionPlanta`, después de iniciado
el guardado. SQLite revierte el periodo, la planta y la modificación de la raíz. Ninguna
parte del reingreso queda persistida.

## 8. Autorización y auditoría

- No se creó ningún rol ni permiso.
- Un usuario `GERENCIA` autenticado recibe la redirección de acceso denegado real.
- El origen y el destino respetan `IProveedorAlcancePlantas`.
- El `InterceptorAuditoria` existente registra el usuario autenticado y cambios en
  `trabajador`, `periodo_laboral` y `asignacion_planta`, incluidos fecha, empresa y planta.
- No se agregó un segundo mecanismo de auditoría.

## 9. Contratos ejecutables añadidos

| Familia | Cantidad | Cobertura |
|---|---:|---|
| `PL3EC_APP_*` | 7 | válido, activo, solape, historia, proyección, rollback y concurrencia |
| `PL3EC_WEB_*` | 5 | GET, POST/PRG, rechazo activo, autorización y ModelState |
| **Total** | **12** | flujo completo Application/Web |

Ejecución dirigida:

- Application: 7/7 verdes.
- Web: 5/5 verdes.

## 10. Límites respetados

- Reportes, tareo, `CalculoTareo`, marcaciones, PMV, rotaciones y feriados: no tocados.
- Migración nueva: no.
- Esquema SQLite: no modificado.
- Campos legacy: no eliminados.
- Producción: no tocada.
- OP-03: no ejecutado.
- Commit: no creado.

## 11. Validación integral final

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 419/419 |
| Infrastructure | 490/490 |
| Web | 28/28 |
| **Total** | **937/937 verdes; 0 fallos; 0 omitidos** |

## 12. Conclusión

El reingreso ya es una operación explícita y usable desde Application/Web, basada en la
historia canónica y en la capacidad de dominio existente.

Estado siguiente permitido: **LISTO PARA FASE 3E-D**. Esto no autoriza ejecutar 3E-D ni
implica preparación para producción.

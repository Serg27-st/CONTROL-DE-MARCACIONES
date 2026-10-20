# Módulo Seguridad

## Propósito

Autenticar usuarios, asignar roles y restringir el acceso operativo a las plantas autorizadas.

## Responsabilidades

- Identity, cookies, login y acceso denegado.
- Roles de sistema y administración de usuarios.
- Asociación usuario–planta.
- Proveer alcance de plantas al nivel de aplicación.
- Aplicar filtros fail-closed en consultas y operaciones.
- Registrar usuario actual para auditoría.

## Entidades

`ApplicationUser`, `UsuarioPlanta` y los roles definidos por `RolesSistema`.

## Servicios

`ServicioUsuarios`, `IProveedorAlcancePlantas`, `ProveedorAlcancePlantasHttp`, `AlcancePlantasConsultas` e inicialización de seguridad.

## Reglas

- Un usuario restringido solo puede operar plantas asignadas.
- El coordinador/global conserva alcance total según rol.
- Ocultar una opción en UI no reemplaza validación del servicio.
- El equipo/planta solicitado debe validarse en listar, importar, consultar y exportar.
- Ante ausencia de contexto, el alcance sensible debe fallar cerrado.

## Flujo

Cookie/Identity → usuario/roles → proveedor de alcance → filtro/validación de servicio → consulta o mutación.

## Fuentes de verdad

- Identity para identidad y roles.
- `UsuarioPlanta` para asignaciones.
- `IProveedorAlcancePlantas` como contrato transversal.

## Archivos críticos

- `src/ControlMarcacion.Web/Seguridad/ProveedorAlcancePlantasHttp.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/AlcancePlantasConsultas.cs`
- `src/ControlMarcacion.Infrastructure/Seguridad/ServicioUsuarios.cs`
- `src/ControlMarcacion.Infrastructure/Seguridad/UsuarioPlanta.cs`
- `src/ControlMarcacion.Web/Program.cs`

## Pruebas

`AislamientoPorPlantaTests` cubre servicios principales. `AislamientoPmvAcceptanceTests` cubre SEC-01..04 con SQLite: filtro de listado/historial, rechazo directo de importación y consulta/exportación fuera de alcance, y operación autorizada. No existe proyecto de pruebas Web extremo a extremo.

## Problemas conocidos

- SEGURIDAD/P0: **RESUELTO**; PMV reutiliza `IProveedorAlcancePlantas` en backend.
- DEUDA/P2: falta cobertura extremo a extremo de controladores/políticas Web.
- RIESGO: configuración sensible de despliegue depende de disciplina externa.

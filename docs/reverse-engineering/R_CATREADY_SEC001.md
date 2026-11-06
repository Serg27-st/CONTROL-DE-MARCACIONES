# R-CATREADY-SEC-001 — Autorización por unidad en preparación para tareo

Fecha de reconciliación: 2026-11-02  
Estado técnico automatizado: **CORREGIDO**  
Estado de cierre formal: **CERRADO**

## Defecto original

`D-SMOKE-SEC-001` permitió que un usuario con rol `COORDINADOR` y una asignación
restrictiva en `UsuarioPlanta` visualizara trabajadores y diagnósticos de otra unidad.
La consulta con `plantaId` manipulado también devolvía información ajena.

Los datos expuestos en el escenario ficticio eran:

- nombre del trabajador;
- documento ficticio;
- unidad;
- estado LISTO/INCOMPLETO/CONFLICTO;
- cantidades de días y motivos del diagnóstico.

No se usaron datos personales reales.

## Causa raíz demostrada

`ProveedorAlcancePlantasHttp.CalcularAsync` devolvía `null` para todo coordinador.
En el contrato `IProveedorAlcancePlantas`, `null` representa acceso global. Por tanto, las
filas restrictivas de `UsuarioPlanta` nunca llegaban al validador.

El controlador y `ValidadorPreparacionTareo` ya aplicaban el alcance en servidor. El
validador filtra mediante `PeriodoLaboral` y `AsignacionPlanta` antes de materializar los
trabajadores y vuelve a comprobar cada fecha histórica. La fuente incorrecta era el
proveedor HTTP de alcance.

## Corrección

La semántica efectiva queda así:

| Identidad | Resultado |
|---|---|
| Coordinador con filas `UsuarioPlanta` | limitado exactamente a esas plantas |
| Coordinador sin filas | acceso global por la política vigente del rol superior |
| Administrador con filas | limitado exactamente a esas plantas |
| Administrador sin filas | colección vacía; ningún acceso |
| Rol operativo sin `NameIdentifier` | colección vacía; ningún acceso |
| Proceso sin sesión HTTP | `null`, para arranque y herramientas internas |

El parámetro `plantaId` continúa siendo un filtro solicitado, no una autorización. Un
filtro fuera del alcance produce una respuesta HTTP `200` sin resultados ni datos ajenos,
que es el contrato HTTP vigente de esta pantalla.

## Seguridad histórica

La autorización histórica se resuelve con `PeriodoLaboral.AsignacionesPlanta` vigente en
la fecha consultada. Un trabajador que cambia de unidad solo entrega los días pertenecientes
al alcance autorizado; los días de la unidad ajena no forman parte de la respuesta.

## Acceso directo por ID

`EvaluarFechaAsync` y `EvaluarIntervaloAsync` reutilizan `EvaluarAutorizadosAsync`. Si el
trabajador no pertenece al alcance en la fecha solicitada, el servicio responde con
`RecursoNoEncontradoException`, sin confirmar que el registro existe.

## Política del coordinador global

La excepción de coordinador sin asignaciones está respaldada por el contrato heredado:

- `RolesSistema` define `COORDINADOR` como rol superior con acceso a todo el sistema;
- el alta vigente solo interpreta `PlantaIds` para `ADMINISTRADOR`;
- antes de esta remediación el contrato documentaba a `COORDINADOR` sin restricción.

La remediación conserva ese acceso global únicamente cuando no existen asignaciones. Si
existe al menos una fila `UsuarioPlanta`, esa configuración explícita prevalece y restringe
el alcance.

Riesgo residual: la interfaz normal no administra asignaciones de planta para coordinadores;
por ello un coordinador restringido requiere que su configuración explícita provenga de un
flujo administrativo controlado. Esto no reabre el bypass corregido, pero debe considerarse
si se incorpora administración de alcance para ese rol.

## Contratos automatizados

Se agregaron contratos `SEC-CATREADY-01..14` y una defensa adicional:

- proveedor HTTP con coordinador restringido, global, múltiple y alcance vacío;
- consulta sin filtro y con `plantaId` autorizado/no autorizado;
- acceso individual por ID;
- alcance vacío;
- consulta histórica y cambio de unidad;
- preservación de LISTO, INCOMPLETO y CONFLICTO;
- ausencia de escrituras;
- respuestas HTTP autenticadas sin nombre, documento ni diagnóstico ajeno.

Resultados dirigidos finales:

- Web/HTTP: 7/7 verdes;
- Infraestructura y CATREADY: 28/28 verdes;
- total dirigido: 35/35 verdes.

## Validación general

Build Release:

- errores: 0;
- advertencias: 0.

Suite completa:

- Domain: 442/442;
- Web: 48/48;
- Infrastructure: 612/612;
- total: **1,102/1,102 verdes**;
- omitidos: 0.

La baseline anterior era 1,087; se incorporaron 15 pruebas de seguridad.

## Smoke de navegador R-CATREADY-SEC-001-B

Se intentaron, en orden:

1. navegador integrado: en esta sesión solo permitió abrir una pestaña, sin API para leer o
   interactuar con su contenido;
2. Chrome y Edge externos headless: los procesos no conservaron un canal automatizable;
3. Playwright del runtime integrado: cargó correctamente y lanzó Chrome real, pero la
   navegación a `http://127.0.0.1:5298` terminó con `ERR_NETWORK_ACCESS_DENIED` impuesto por
   el runtime del navegador.

No hubo respuesta HTML ni captura que pudiera atribuirse a la aplicación. No se fabricaron
capturas. Los casos visuales permanecen bloqueados; las pruebas HTTP autenticadas son
evidencia complementaria, no sustitución de la prueba visual.

## Integridad

La copia aislada usada como fixture está en:

`artifacts/R-CATREADY-SEC-001-B/runtime/R-CATREADY-SEC-001-B.db`

- SHA-256 final: `2784C56317A6BF72E92F58C32525589A816005B40E40579221F661F2A886400B`;
- tamaño: 704,512 bytes;
- `PRAGMA integrity_check`: `ok`;
- `PRAGMA foreign_key_check`: 0 filas.

Contiene exclusivamente trabajadores ficticios: al menos dos en la unidad autorizada y dos
en la unidad no autorizada. La cuenta ficticia de coordinador posee una sola fila
`UsuarioPlanta` para la unidad autorizada. La contraseña temporal no se documentó.

## Evidencia

- `artifacts/R-CATREADY-SEC-001-B/00_Informe_Smoke.md`;
- `artifacts/R-CATREADY-SEC-001-B/evidencia/seguridad-web.trx`;
- `artifacts/R-CATREADY-SEC-001-B/evidencia/seguridad-infraestructura.trx`;
- tres archivos TRX de la suite completa, uno por proyecto de pruebas.

## Cierre visual R-CATREADY-SEC-001-C

El 2026-11-02 se ejecutó Playwright directamente en el host Windows con Google Chrome
instalado, contra `http://127.0.0.1:5298` y una copia SQLite aislada.

Resultados:

- 12/12 casos visuales PASS;
- 0 fallos;
- 0 bloqueados;
- 12 capturas PNG reales;
- respuestas HTTP 200/404 comprobadas;
- matriz JSON e informe HTML Playwright generados;
- ninguna exposición de nombres, documentos o diagnósticos de la unidad ajena;
- ninguna regresión nueva.

La evidencia está en `artifacts/R-CATREADY-SEC-001-C/`.

## Estado

La vulnerabilidad está corregida y validada por SQLite real, HTTP autenticado y navegador
real. Se satisfacen los criterios de cierre, por lo que `R-CATREADY-SEC-001` queda **CERRADO**.

Producción no fue tocada, OP-03 no se ejecutó y no se creó ningún commit ni migración.

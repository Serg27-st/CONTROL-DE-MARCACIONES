# R-WEB-ROT-DESC — Corrección Web/MVC del formulario de rotaciones

Fecha: 2026-10-07  
Resultado: **VALIDADO**  
Alcance: Web/MVC, binding, validación, JavaScript, CSP y pruebas HTTP  
Dominio, cálculo, migración y reglas económicas modificados: **NO**

## 1. Reproducción y causa

La reproducción se hizo con ASP.NET Core real, sesión `COORDINADOR`, antiforgery y una
copia migrada de OP-02B.

### UI-ROT-02 — CSP

- vista: `src/ControlMarcacion.Web/Views/Cuadrillas/Programar.cshtml`;
- política: `script-src 'self'` en `Program.cs`;
- defecto: la transición Trabajo/Descanso estaba implementada en un `<script>` inline;
- efecto: el navegador no ejecutaba la lógica, por lo que Turno quedaba habilitado y Días
  vacío/editable al elegir Descanso.

La solución movió la lógica a `wwwroot/js/programacion-cuadrilla.js` y la vista carga el
recurso mediante `src` con `asp-append-version`. No se agregó `unsafe-inline`, `eval`,
`new Function` ni eventos inline.

### UI-ROT-05 — binding MVC

- action: `CuadrillasController.Programar(ProgramarCuadrillaVm, CancellationToken)`;
- modelo anterior: `ProgramarCuadrillaVm.Datos` exponía directamente
  `ProgramarCuadrillaRequest` de Application;
- colección anterior: `IReadOnlyList<TramoProgramadoRequest>`;
- payload reproducido: índices continuos `Datos.Tramos[0..5]`, enum como
  `Trabajo`/`Descanso`, descanso sin `TurnoId` y con `Dias=1`;
- estado reproducido: las entradas hijas quedaban `Unvalidated`; `ModelState.IsValid` era
  falso sin errores útiles y el POST válido regresaba HTTP 200 al formulario sin persistir.

No había un defecto de índices, enum ni nulabilidad de `TurnoId`. La causa era usar como
modelo de formulario una colección de solo lectura del contrato de Application.

La corrección incorpora `ProgramarCuadrillaDatosVm` y `TramoProgramadoVm`, con una
`List<TramoProgramadoVm>` mutable, validación condicional mediante `IValidatableObject` y
mapeo explícito al DTO de Application solo después de que `ModelState` es válido.

## 2. Contrato del formulario

El script externo conserva una sola fuente por dato:

- Trabajo: Turno habilitado y Días editable;
- Descanso: limpia el turno, deshabilita el `select`, fija Días en 1 y deja el input
  `readonly` para que el valor sí viaje en el POST.

El servidor no confía en JavaScript:

- Descanso + TurnoId produce `Un descanso no puede tener turno.`;
- Descanso + Días distinto de 1 produce
  `Un descanso debe durar exactamente un día.`;
- Trabajo parcialmente llenado exige TurnoId y Días;
- las filas Trabajo completamente vacías siguen siendo plazas libres del formulario.

El payload real del smoke válido fue:

```text
Datos.Desde=2026-10-12
Datos.Tramos[0]=Trabajo, TurnoId=1, Dias=2
Datos.Tramos[1]=Descanso, TurnoId ausente, Dias=1
Datos.Tramos[2]=Trabajo, TurnoId=2, Dias=2
Datos.Tramos[3..5]=Trabajo, TurnoId vacío, Dias vacío
```

Token antiforgery y credenciales no se registraron.

## 3. Pruebas MVC autenticadas

Se añadió `tests/ControlMarcacion.Web.Tests`, incluido en la solución, con cuatro casos
ejecutados contra una SQLite temporal única y comprobada explícitamente:

1. Descanso + TurnoId: HTTP 200 con error específico y cero persistencia.
2. Descanso + Días=2: HTTP 200 con error específico y cero persistencia.
3. DIU ×2, Descanso ×1, NOC ×2: HTTP 302 a Detalle; tres tramos exactos en SQLite; GET
   HTTP 200 con `DIU 2 d → DESCANSO PROGRAMADO → NOC 2 d`.
4. CSP/vista: `script-src 'self'` exacto, recurso externo presente y función inline ausente.

La prueba inicia sesión por `/Cuenta/Ingresar` con el rol real `COORDINADOR`, obtiene y
envía tokens antiforgery reales y atraviesa la action MVC. No desactiva autorización ni
invoca Application directamente para simular el POST.

## 4. Smoke IAB sobre copia OP-02B

Base:
`artifacts/r-web-rot-desc-20261007/ControlMarcacion_RWEB_SMOKE_FINAL.db`.

Resultado:

| Caso | Resultado | Evidencia |
|---|---|---|
| UI-ROT-01 | PASA | Trabajo deja Turno habilitado y Días editable. |
| UI-ROT-02 | PASA | Descanso limpia/deshabilita Turno, fija Días=1 y lo deja readonly; también se deshabilita el control visual enriquecido. |
| UI-ROT-03 | PASA | Prueba HTTP autenticada rechaza Descanso + TurnoId y no persiste. |
| UI-ROT-04 | PASA | Prueba HTTP autenticada rechaza Descanso + Días=2 y no persiste. |
| UI-ROT-05 | PASA | POST real válido redirige a `/Cuadrillas/Detalle/4` y persiste. |
| UI-ROT-06 | PASA | Detalle y consulta SQLite muestran exactamente DIU 2, Descanso 1, NOC 2. |
| UI-ROT-07 | PASA | `/Reportes/Excepciones` muestra `DESCANSO PROGRAMADO`. |

Consola del navegador: 0 errores CSP y 0 errores JavaScript. La copia terminó con
`integrity_check=ok`, cero filas en `foreign_key_check`, 15 migraciones y SHA-256 final
`7B219003A84B9BDCDC92A1E979BA53FE98357774F113A0302E02AC78EA035028`.

## 5. Suite final

```text
Domain          368/368
Infrastructure 435/435
Web               4/4
Total           807/807
Fallos                0
Omitidos              0
```

Compilación de Web y Web.Tests: 0 errores y 0 advertencias.

## 6. Incidentes de aislamiento y recuperación

Durante la reproducción hubo dos desvíos de configuración. Se documentan porque forman
parte de la evidencia operativa y cambian afirmaciones previas de identidad física.

### Copia canónica local del proyecto

Un primer arranque usó una clave de conexión incorrecta y el perfil de lanzamiento, por lo
que aplicó la migración a `src/ControlMarcacion.Web/ControlMarcacion.db`. Se detuvo la
instancia, se conservó una copia del estado migrado y se ejecutó el `Down` sobre una copia
de recuperación. Estado final verificado:

- `integrity_check=ok` y cero violaciones FK;
- 14 migraciones; última `20261001212252_MarcacionesProvisionales`;
- migración ROT-DESC ausente;
- 0 programaciones y 0 tramos;
- SHA-256 final
  `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928`.

El estado lógico quedó recuperado, pero el archivo no es byte a byte idéntico al original:
el rebuild de SQLite cambió su SHA. Por ello quedan corregidas las afirmaciones históricas
que decían que la base canónica conservaba su hash original.

### Base de `C:\ProgramData`

Un segundo arranque omitió `ASPNETCORE_ENVIRONMENT=Development`; `Program.cs` sustituyó la
conexión indicada por la ruta productiva. La aplicación se detuvo inmediatamente. Los
cambios estaban únicamente en WAL. Con autorización expresa del usuario se respaldaron
los sidecars y se retiraron los WAL/SHM exactos. Verificación final:

- archivo principal SHA-256 sin cambio:
  `0E91899A86D8534A1D3770056AC5D7B8BED5C8A6A242CCDB80E3BC552F3A0734`;
- `integrity_check=ok`, cero violaciones FK;
- 8 migraciones; última `20260923140604_RotacionPorCuadrilla`;
- 0 programaciones y 0 tramos;
- 0 sidecars finales.

Los respaldos de los sidecars accidentales están en
`artifacts/r-web-rot-desc-20261007/`. No quedó una migración productiva persistente y no se
ejecutó OP-03.

El arnés MVC también comprueba que el `DbContext` apunta al archivo temporal esperado; esta
aserción impide que una futura prueba caiga silenciosamente sobre la base por defecto.

## 7. Decisión

**OP-02B = VALIDADO.** Los bloqueantes de migración, restore, regresión histórica y Web
están cerrados. **OP-03 está listo para preparación, pero no fue ejecutado.** El despliegue
deberá mantener parada de escrituras, backup restaurable y validación inmediata debido a la
operación SQLite no transaccional ya registrada.

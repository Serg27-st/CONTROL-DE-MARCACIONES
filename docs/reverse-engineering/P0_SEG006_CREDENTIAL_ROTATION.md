# P0-SEG-006 — cierre verificable de credencial comprometida

Fecha de corte: 2026-10-19  
Estado: `CERRADO`  
Producción: no tocada. OP-03: no ejecutado. Historia Git: no reescrita.

## Cierre operacional

La rotación manual segura se ejecutó el 2026-10-19 exclusivamente sobre la instalación local
conocida en `C:\ProgramData\ControlMarcacion`. No se mostró, registró ni automatizó ninguna
contraseña.

| Control | Resultado |
|---|---|
| Respaldo previo | `C:\ProgramData\ControlMarcacion\backups\P0-SEG-006-before-rotation-20261019-090952.db` |
| Tamaño del respaldo | 6,873,088 bytes |
| SHA-256 del respaldo | `AFD9DC13BDBE65894F805492763326F69FA4F5065BD728909913AA203F011E96` |
| Cuenta sustituta | Creada por `/Usuarios`, activa, rol `COORDINADOR` |
| Autonomía | Login inicial, acceso a `Usuarios` y `Marcaciones`, logout/login y login final correctos |
| Cuenta comprometida | Inactiva; bloqueo hasta `9999-12-31`; `SecurityStamp` rotado respecto del respaldo |
| Prueba antigua | Un único intento controlado; rechazado con `Correo o contraseña incorrectos.` |
| Estado Identity final | 2 cuentas: 1 activa y 1 inactiva |
| Integridad SQLite | `ok`; 0 violaciones de FK |
| Escaneo posterior | 18 configuraciones, 4 ZIP y 7 logs; 0 coincidencias del secreto |
| User Secrets / variables bootstrap | 0 entradas / 0 variables presentes |

La aplicación estaba abierta al finalizar. SQLite conservaba el archivo principal con el hash
previo y materializaba la rotación en `ControlMarcacion.db-wal`; por tanto, el hash aislado del
archivo principal no se usa como prueba del estado lógico. La consulta transaccional sí confirmó
el bloqueo, la rotación del sello, la cuenta sustituta y la integridad. El respaldo previo quedó
sin cambios y se conserva solo para rollback/evidencia.

## Tipo, proveedor y uso

| Campo | Resultado |
|---|---|
| Tipo | Usuario/correo y contraseña de bootstrap administrativo |
| Proveedor | ASP.NET Core Identity respaldado por la base SQLite de cada instancia; no existe proveedor externo centralizado identificado |
| Uso histórico | Crear el primer usuario administrativo cuando una base no contenía usuarios |
| Consumidor histórico | `InicializadorSeguridad.InicializarAsync` mediante `Seguridad:AdminCorreo` y `Seguridad:AdminClave` |
| Dependencia del código actual | No para el primer arranque normal: `/setup` crea el primer `COORDINADOR`; el bootstrap externo permanece como compatibilidad opcional |

No se registra el valor, su fragmento, el hash Identity ni ningún dato que permita recuperarlo.

## Origen y propagación histórica

La fuente fue `src/ControlMarcacion.Web/appsettings.json` versionado. El par se propagó a
copias de build/publish y configuraciones de despliegue de su época. Un ZIP histórico conserva
la contraseña en dos entradas:

- `appsettings.json`;
- `appsettings.Production.json`.

Ese ZIP permanece únicamente en la cuarentena privada como
`HISTORICO_COMPROMETIDO_NO_UTILIZAR_PARA_DESPLIEGUE__ControlMarcaciones.zip`.
Git conserva el valor en historia; la historia no se reescribió por decisión explícita.

## Inventario actual

La comparación se realizó contra el valor histórico leído en memoria, sin imprimirlo.

| Superficie | Resultado |
|---|---|
| Fuente actual `appsettings.json` | Sin correo ni contraseña histórica; campos de bootstrap vacíos |
| Builds Debug/Release y RID revisados | Cero coincidencias exactas |
| Publish local y portable | Cero coincidencias; campos de bootstrap vacíos |
| Despliegue local actual | Cero coincidencias; campos vacíos |
| ZIP OP-03A actual | Cero coincidencias en todas sus entradas |
| Instalador actual `ControlMarcacion-Setup.exe` | Cero coincidencias exactas en el binario; su publish fuente también está limpio |
| Logs actuales del proyecto | Cero coincidencias del secreto y cero patrones `Authorization`, `Bearer`, `Password=`, `ApiKey=` o `Token=` |
| User Secrets del proyecto | Cero entradas |
| Variables del proceso auditado | `Seguridad__AdminCorreo` y `Seguridad__AdminClave` ausentes |
| ZIP histórico en cuarentena | Dos entradas afectadas; preservado como evidencia y fuera de distribución |

El instalador revisado tiene SHA-256
`FB354DA1D37D1716323C72D65657D94966B1612B6E2EE325BE15AE147C5A65BB`. El ZIP histórico
comprometido tiene SHA-256
`10120A42A30F5716E5F5EFFA8B580D14A9451A1D61D2B50E1DB697883A1AE994`.

## Verificación previa — corte histórico superado

Se inspeccionaron en modo inmutable la base de la instalación local conocida y las copias SQLite
de cuarentena. No se realizaron escrituras.

- 28 archivos SQLite conocidos fueron considerados.
- 28 hashes de contraseña Identity fueron verificados contra la contraseña histórica.
- 13 snapshots/copia-instancia contienen una cuenta cuyo hash todavía acepta esa contraseña.
- Las copias de cuarentena son evidencia offline, no servicios activos.
- La instalación local conocida en `C:\ProgramData\ControlMarcacion\data\ControlMarcacion.db`
  contiene un único usuario, activo, con rol `COORDINADOR`, cuya contraseña todavía coincide.
- El correo de esa cuenta no coincide con el correo histórico del bootstrap, pero reutiliza la
  misma contraseña comprometida.
- La lectura final de esa base fue inmutable: SHA-256 antes y después
  `AFD9DC13BDBE65894F805492763326F69FA4F5065BD728909913AA203F011E96`.

Conclusión previa, **SUPERADA por el cierre operacional**: la credencial antigua seguía siendo
válida en al menos una instancia local conocida. No existía evidencia suficiente para clasificar esa instancia como prueba descartable
ni autorización para modificarla como si no fuera operativa. Por ello no se intentó login ni se
alteró la cuenta.

## Mitigación ya vigente

- Se retiró el secreto de fuente, build, publish, despliegue e instalador actuales.
- `/setup` sustituyó la necesidad normal de bootstrap empaquetado.
- El instalador no captura ni persiste una contraseña.
- Identity solo persiste hashes.
- El ZIP comprometido está aislado, marcado como histórico/no utilizable y excluido de
  distribución.
- Las bases/snapshots afectados permanecen en cuarentena privada.

## Procedimiento manual ejecutado

**Proveedor:** ASP.NET Core Identity de la instancia local respaldada por
`C:\ProgramData\ControlMarcacion\data\ControlMarcacion.db`.

**Pantalla/sección:** aplicación local → `Usuarios` (`/Usuarios`). Solo un `COORDINADOR` puede
administrar cuentas.

**Credencial a revocar:** contraseña comprometida reutilizada por el único `COORDINADOR`
activo. No copiarla desde Git o desde el ZIP a tickets, documentos o comandos compartidos.

**Procedimiento recomendado:**

1. El responsable confirma primero si esa base es operativa/productiva y aprueba una ventana.
2. Autenticado como el coordinador existente, crear en `/Usuarios/Crear` un `COORDINADOR`
   sustituto con identidad real aprobada y contraseña nueva, única, entregada fuera de banda.
3. Cerrar sesión e iniciar sesión una vez con la cuenta sustituta.
4. Con la cuenta sustituta, abrir `/Usuarios` y desactivar la cuenta anterior. La aplicación
   fija bloqueo indefinido y rota su security stamp, invalidando sesiones activas.
5. Realizar una sola prueba controlada: la cuenta anterior ya no debe autenticar.
6. Confirmar que la cuenta sustituta inicia sesión y conserva el rol esperado.
7. No guardar la nueva contraseña en `appsettings`, repositorio, instalador, documentación,
   scripts o variables permanentes. El flujo `/setup`/Identity ya es el mecanismo seguro.
8. Registrar fecha, responsable, instancia y resultado, nunca el secreto.

Si negocio exige conservar la misma identidad en vez de sustituirla, el sistema no ofrece hoy
un flujo de cambio/restablecimiento de contraseña. Debe aprobarse un procedimiento Identity
administrativo específico; no se debe editar `password_hash` directamente con SQL.

**Resultado confirmado:** cuenta anterior inactiva/bloqueada, una prueba de autenticación antigua
fallida, cuenta sustituta operativa y escaneo actual en cero.

## Contratos existentes

No se añadieron tests porque la exposición local ya está protegida por contratos existentes:

- `InicializadorSeguridadTests.Base_sin_usuarios_y_sin_bootstrap_externo_falla_explicito`;
- `FirstRunTests` comprueba configuración distribuida vacía y flujo `/setup`;
- `tools/installer/validar-publish.ps1` limpia variables, usa credenciales sintéticas efímeras y
  comprueba que no queden en texto plano.

La brecha era operacional, no una reintroducción de código: revocar una contraseña ya
materializada en una cuenta Identity existente. Quedó cerrada por el procedimiento anterior.

## Estado y riesgo residual

`SEG-006 = CERRADO`.

Se cumplen los criterios de cierre: la credencial histórica ya no autentica la instancia local
activa conocida, el sustituto mantiene autonomía administrativa y el escaneo actual permanece en
cero. La exposición histórica de Git y las copias offline no desaparece: continúa documentada y
contenida por cuarentena. Esas copias no deben reutilizarse para despliegue y la historia Git no
se reescribió.

`REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md` y `PROJECT_STATE.md` se actualizaron al estado
resuelto. Esta operación no autoriza producción, OP-03 ni migraciones productivas.

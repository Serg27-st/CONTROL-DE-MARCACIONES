# P1-STARTUP-HEALTH — Arranque seguro ante fallos de migración

Fecha: 2026-10-09  
Estado: **RESUELTO Y VERIFICADO**  
Producción: **intacta**  
OP-03: **no ejecutado**

> El encargo recibido termina literalmente en `# 4. END`. La implementación se limitó al
> objetivo explícito de separar proceso vivo de aplicación operativamente lista.

## 1. Reproducción del defecto

### Flujo anterior confirmado

```text
Program.cs
  ↓
MapGet("/estado", () => Marcas.Sistema)
  ↓
CreateScope
  ↓
MarcacionDbContext.Database.MigrateAsync()
  ↓
InicializadorSeguridad.InicializarAsync()
  ↓
catch Exception
  ↓
solo log; no propaga
  ↓
app.Run()
```

Consecuencia confirmada en código:

- una excepción de migración entraba al `catch` general;
- `RegistroArranque.BaseNoDisponible` registraba el fallo;
- la excepción no se propagaba;
- Kestrel arrancaba;
- `/estado` siempre devolvía 200 y la marca `ControlMarcaciones-OK`, sin consultar la base;
- el proceso podía anunciarse sano con migraciones pendientes o esquema parcial.

La excepción específica de bootstrap ausente ya se propagaba por la contención 7A; este cambio
no altera ese fail-safe.

## 2. Reconstrucción actual

### Archivo: `src/ControlMarcacion.Web/Program.cs`

Método: top-level startup y funciones locales `EsNuestroSistema`/`SePuedeEscuchar`.

Orden:

1. carga configuración y elige puerto;
2. reconoce una instancia viva mediante `/vivo`, con fallback `/estado` para una versión anterior;
3. registra MVC, infraestructura, Identity, `IPreparadorAplicacion` e
   `IVerificadorEstadoOperativo`;
4. construye middleware y rutas;
5. publica `/vivo` y `/estado` como endpoints anónimos separados;
6. crea un scope y ejecuta `IPreparadorAplicacion.PrepararAsync`;
7. el preparador aplica migraciones y ejecuta el bootstrap de seguridad;
8. credenciales de bootstrap ausentes en base vacía abortan el arranque;
9. otros fallos de preparación se registran y el proceso queda vivo, pero readiness da 503;
10. un estado thread-safe bloquea con 503 todas las rutas salvo `/vivo` y `/estado`;
11. `app.Run()` inicia el servidor.

### Archivo: `src/ControlMarcacion.Web/Operaciones/IPreparadorAplicacion.cs`

Clase: `PreparadorAplicacion`.

```text
MigrateAsync(cancelación)
→ InicializadorSeguridad.InicializarAsync(...)
```

Es la única pieza nueva de startup con efectos de escritura.

### Archivo: `src/ControlMarcacion.Web/Operaciones/IVerificadorEstadoOperativo.cs`

Clase: `VerificadorEstadoOperativo`.

Es read-only y valida:

1. conexión disponible;
2. conjunto de migraciones aplicadas exactamente compatible con el binario;
3. ninguna migración esperada pendiente;
4. `PRAGMA quick_check = ok`;
5. `PRAGMA foreign_key_check` sin filas;
6. existencia de cada tabla mapeada por el modelo EF;
7. existencia de todas las columnas mapeadas en esas tablas.

No aplica migraciones, no repara, no crea usuarios y no expone excepciones.

## 3. Contrato HTTP

### `/vivo`

```http
200 OK
ControlMarcaciones-OK
```

Significa únicamente que el proceso correcto escucha. Impide levantar una segunda instancia
cuando la primera está viva pero no lista. Incluye `Cache-Control: no-store`.

### `/estado` listo

```http
200 OK
ControlMarcaciones-OK;LISTO
```

### `/estado` no listo

```http
503 Service Unavailable
{
  "sistema": "ControlMarcaciones-OK",
  "estado": "NO_LISTO",
  "codigo": "<código genérico>"
}
```

Códigos posibles:

```text
BASE_NO_DISPONIBLE
MIGRACIONES_PENDIENTES
ESQUEMA_MAS_NUEVO_QUE_BINARIO
ESQUEMA_INCOMPATIBLE
VERIFICACION_FALLIDA
```

No se devuelven rutas, nombres concretos de migración, SQL, stack traces, cadenas de conexión ni
datos personales.

Las dos respuestas incluyen `Cache-Control: no-store`, por lo que un proxy o navegador no puede
reutilizar un 200 anterior después de que la base deje de estar preparada.

## 4. Recuperación

Readiness se calcula en cada consulta. Por tanto:

- si la conexión vuelve y el esquema ya era compatible, `/estado` cambia a 200 sin reiniciar;
- si una intervención autorizada aplica la migración pendiente, cambia de 503 a 200 sin reiniciar;
- si la migración quedó parcial, permanece en 503 hasta que el esquema sea reparado;
- consultar `/estado` nunca ejecuta esa reparación.

No se agregó un bucle automático de reintento de migraciones: repetir una reconstrucción SQLite
parcial de forma desatendida podría aumentar el daño. La recuperación separa detección de
remediación.

Mientras la preparación está en `Starting` o `Failed`, el middleware devuelve 503 con el código
`INICIALIZACION_NO_COMPLETA` para cualquier ruta funcional. Si una reparación externa deja el
esquema compatible, una comprobación exitosa de `/estado` cambia el estado thread-safe a Ready y
rehabilita el tráfico sin reiniciar.

## 5. Contratos ejecutables

| ID | Caso | Esperado | Resultado |
|---|---|---|---|
| SH-01 | Preparación lanza excepción | `/vivo` 200 y `/estado` 503 | Verde |
| SH-02 | Fallo contiene detalle interno | Respuesta no filtra el mensaje | Verde |
| SH-03 | Base en penúltima migración | `MIGRACIONES_PENDIENTES` | Verde |
| SH-04 | Se aplica la migración faltante | readiness vuelve a 200 sin reinicio | Verde |
| SH-05 | Historial completo, tabla requerida ausente | `ESQUEMA_INCOMPATIBLE` | Verde |
| SH-06 | Configuración normal | tests MVC existentes arrancan y hacen login | Verde |
| SH-07 | Health consultado | no aplica migraciones ni escribe | Garantizado por separación de servicio |

Archivo de pruebas:
`tests/ControlMarcacion.Web.Tests/Operaciones/StartupHealthTests.cs`.

## 6. Consumidores operativos alineados

- `tools/despliegue/publicar.ps1` mantiene `/estado` como prueba de readiness y exige HTTP
  200, la marca del sistema y `LISTO`. Para ensayar una base nueva usa un bootstrap aleatorio
  y efímero heredado por el proceso hijo; no lo escribe en archivos ni lo incorpora al ZIP.
- `tools/despliegue/instalar.ps1` usa `/vivo` para distinguir proceso iniciado y `/estado`
  para confirmar disponibilidad operativa. Rechaza la contraseña de relleno antes de registrar
  la tarea programada.
- `OP_03_ROT_DESC_PRODUCTION_RUNBOOK.md` quedó actualizado para registrar ambas señales. El
  runbook no fue ejecutado y producción permanece intacta.

## 7. Verificación final

```text
Build Release
0 errores
0 warnings

Domain.Tests
368 / 368

Infrastructure.Tests
436 / 436

Web.Tests
9 / 9

Total
813 / 813
0 fallos
0 omitidos
```

La ejecución no regeneró bases SQLite dentro del repositorio. Las cuatro eliminaciones
preparadas de Fase 7A permanecen. La auditoría final descubrió dos sidecars regenerados al
inspeccionar un backup y una base SQLite sin extensión de la evidencia de path drift de OP-03A
con sus dos sidecars. Los cinco archivos se copiaron a la cuarentena, se verificaron 5/5 hashes
y se retiraron sus originales. Quedaron registrados en dos manifiestos addendum; producción no
fue tocada.

Los dos scripts PowerShell fueron analizados por el parser de Windows PowerShell sin errores.
No se ejecutó publicación, instalación ni despliegue.

## 8. Criterio de cierre

```text
fallo de migración
→ proceso identificable por /vivo
→ /estado = 503
→ no recibe tráfico como listo

esquema reparado y compatible
→ /estado = 200
→ sin reinicio obligatorio
```

`P1-STARTUP-HEALTH = RESUELTO`.

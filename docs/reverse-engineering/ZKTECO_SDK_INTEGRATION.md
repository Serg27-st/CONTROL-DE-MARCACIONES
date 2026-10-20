# P1-ZKTECO-SDK — integración controlada

Fecha de auditoría: 2026-10-13.

## Resultado ejecutivo

El paquete oficial inspeccionado contiene Standalone SDK 6.3.1.37 y PULL SDK
2.2.0.220. `plcommpro.dll` sí expone una API de parsing offline,
`ProcessBackupDataExt`, que acepta el contenido de un respaldo USB. No es necesario
conectar un equipo para invocarla.

La API abre `BKtransaction(5).dat` y reconoce su cabecera y sus 981 registros
estructurales, pero devuelve columnas de marcación inválidas para este modelo/firmware:
identificadores y tiempos quedan en cero o en campos incompatibles. Por tanto, la
capacidad de abrir el archivo no demuestra fidelidad. El lector interno continúa siendo
la única fuente autoritativa.

## Inventario del paquete

| Componente | Tipo | Arquitecturas | Versión observada | Uso relevante |
|---|---|---|---|---|
| `zkemkeeper.dll` | COM | x86 y x64 | 6.3.1.37 | Conexión y lectura desde un dispositivo Standalone |
| `plcommpro.dll` | DLL nativa PULL | x86 y x64 | 2.2.0.220 | `Connect`, `GetDeviceData`, `ProcessBackupData`, `ProcessBackupDataExt` |
| `Standalone SDK Development Manual V2.1` | PDF | — | V2.1 | API Standalone y lectura del buffer del equipo |
| demos C# y VB | código fuente/EXE | x86/x64 según proyecto | paquete 6.3.1.37 | conexión y descarga desde equipos |

La edición PULL 2.2.1.4 publicada por otro portal oficial de ZKTeco se usó como
contraprueba. Produjo la misma salida inválida para el corpus; no corrige esta
incompatibilidad.

Fuentes del paquete: [SDK global de ZKTeco](https://www.zkteco.com/en/SDK),
[descargas SDK de ZKTeco South Africa](https://zkteco.co.za/downloads/sdk/) y
[SDK de ZKTeco Thailand](https://www.zkteco.co.th/SDK).

## API offline confirmada

```text
plcommpro.dll!ProcessBackupDataExt
```

El adaptador utiliza carga dinámica de la DLL configurada. No agrega referencia de
compilación, paquete NuGet, registro COM ni copia de DLL al repositorio. Antes de la
invocación valida que el PE sea x64, de acuerdo con la publicación actual `win-x64`.

`zkemkeeper.dll` no se usa para este flujo. Sus métodos de asistencia leen el buffer de
un equipo conectado y requieren COM; son una vía distinta a interpretar un respaldo
offline.

## Arquitectura implementada

```text
Web (configuración)
  ↓
Application.IFuenteDeMarcaciones
  ↑
Infrastructure
  ├─ LectorRespaldoZkteco        (interno, autoritativo)
  ├─ LectorRespaldoZktecoSdk     (adaptador diagnóstico)
  ├─ ZktecoSdkOfflineParserNativo
  └─ ComparadorLecturasBkTransaction
```

Domain y Application no referencian DLL ni tipos ZKTeco. `MarcacionCruda` no fue
modificada. Los dos lectores normalizan a `ResultadoLectura`.

## Configuración

```json
"Marcaciones": {
  "LectorBkTransaction": "Interno",
  "DirectorioSdkZkteco": "",
  "DirectorioDiagnostico": "artifacts/bktransaction-validation"
}
```

- `Interno`: comportamiento previo; no intenta cargar el SDK.
- `Sdk`: usa exclusivamente el SDK. Un fallo devuelve `SDK_NO_DISPONIBLE`,
  `SDK_INCOMPATIBLE` o `SDK_ERROR`; no hay fallback silencioso.
- `Comparar`: ejecuta ambos sobre los mismos bytes, genera evidencia y devuelve solo el
  resultado interno. No combina ni persiste dos veces.

El valor por defecto es `Interno`.

## Licencia y redistribución

El ZIP inspeccionado no contiene EULA, archivo de licencia ni una concesión explícita de
redistribución. La página pública permite descargar el SDK, pero eso no acredita el
derecho a incorporar sus binarios en un instalador. Resultado: **NO DETERMINADO**.

No se copiaron DLL al repositorio, al portable ni a `ControlMarcacion-Setup.exe`. Cualquier
redistribución requiere primero una licencia escrita aplicable y una validación de x64.

## Contratos automatizados

- ZKSDK-01: dependencia confinada a Infrastructure.
- ZKSDK-02: modo Interno conserva el comportamiento.
- ZKSDK-03: Comparar devuelve una sola fuente autoritativa.
- ZKSDK-04: detecta diferencias de ID, fecha y código.
- ZKSDK-05: SDK ausente produce error explícito.
- ZKSDK-06: normalización SDK y preservación textual de ceros cuando existen.
- ZKSDK-07: Interno no carga ni invoca el SDK.
- ZKSDK-08: se rechaza una DLL que no sea PE x64.

Validación final: build Release 0 errores/0 warnings; 874/874 pruebas verdes, sin omitidas.

## Decisión

Mantener `Interno`. El adaptador oficial queda como herramienta diagnóstica de desarrollo,
no como lector principal ni componente redistribuible. Si se requiere una referencia
oficial adicional, la alternativa segura es descargar desde el dispositivo mediante el
SDK y comparar el CSV oficial con el lector interno.


# Formato de los archivos del marcador ZKTeco

> Análisis del 25/08/2026, motivado por el cambio de equipo en planta y por un archivo
> `BKtransaction.dat` que "el sistema no podía leer".

## Conclusión corta

**Nada está cifrado.** El equipo exporta dos archivos distintos, los dos con extensión
`.dat`, y se cargó el que no era. El sistema ahora **lee los dos** y decide por el
contenido, nunca por la extensión.

| | Descarga de marcaciones | Respaldo de datos |
|---|---|---|
| Nombre | `SERIE_attlog.dat` (o `.txt`) | `BKtransaction.dat` |
| Contenido | Texto tabulado, una línea por marcación | Volcado binario de las tablas del equipo |
| Lo lee | `LectorTxtZkteco` | `LectorRespaldoZkteco` |
| **Recomendado** | **Sí** | Solo si no hay otra cosa — ver limitaciones |

En `\\192.168.13.182\Asistencia` hay archivos `AEWD193260084_attlog.dat` desde 2024: el
flujo de la empresa **ya venía usando `.dat` de texto**, porque abrirlos en el Bloc de notas
se ve igual que un `.txt`.

## Cómo se reparte

`LectorMarcadorAutomatico` es el único `IFuenteDeMarcaciones` registrado. Mira la cabecera,
decide cuál de los dos formatos es y delega. **La extensión y el nombre no intervienen**:
los dos llegan como `.dat`, y un archivo renombrado a mano no debe cambiar cómo se lee.

```
archivo → DetectorFormatoMarcador
             ├── cabecera de respaldo → LectorRespaldoZkteco → marcaciones
             └── cualquier otra cosa  → LectorTxtZkteco      → marcaciones
```

## El archivo de texto: `SERIE_attlog.dat`

Seis columnas separadas por tabulador, `CRLF`, sin encabezado:

```
       73→2025-12-01 06:06:38→1→0→1→0
```

| # | Columna | Notas |
|---|---|---|
| 0 | ID de marcador | Espacios a la izquierda. **Hay ceros significativos** (`0014` ≠ `14`): siempre texto |
| 1 | Fecha y hora | `yyyy-MM-dd HH:mm:ss` |
| 2 | Modo de verificación | `1` en toda la muestra (huella) |
| 3 | Tipo de marcación | **Siempre `0`**: el equipo no distingue entrada de salida |
| 4 | Workcode | Casi siempre `1`; aparece `15` de forma aislada |
| 5 | Reservado | `0` |

## El archivo binario: `BKtransaction.dat`

Binario propietario, **sin cifrar**: entropía **2,85 bits/byte** sobre 8 (cifrado o
comprimido daría ~7,9), 45 valores de byte distintos de 256, y cadenas ASCII en claro. La
muestra pesa 315 bytes y cierra exactamente:

```
0x00  ┌─ CABECERA (63 bytes) ──────────────────────────────┐
0x00  │ 0D  "VGU6251600671"      serie (prefijo longitud)  │
0x0E  │ 16  "ZAM70-NF28VA-Ver3.0.15"  plataforma/firmware  │
0x25  │ 1C 00 00 00  uint32 = 28 → tamaño de registro      │
0x29  │ 07           número de campos                      │
0x2A  │ 7 × [69 'i' | ancho | 00]  esquema: 4,4,1,1,1,1,4  │
0x3F  ├─ REGISTROS: 9 × 28 = 252 ──────────────────────────┤
0x13B └─ fin: 63 + 252 = 315 ──────────────────────────────┘
```

Los siete campos suman 16 bytes y el registro mide 28: quedan **8 bytes de relleno por
delante y 4 por detrás**, y eso no está declarado en ninguna parte.

| rel | Ancho | Contenido en la muestra | Lectura |
|---|---|---|---|
| `0x00–0x07` | 8 | todo cero | relleno |
| `0x08–0x0B` | 4 | `0,0,2,3,2,3,2,3,1` | **ID de marcador** — confirmado contra el padrón del equipo |
| `0x0C–0x0F` | 4 | todo cero | sin determinar |
| `0x10` | 1 | `C8,C8,1,1,1,1,1,1,3` | modo de verificación |
| `0x11` | 1 | `1` siempre | tipo de marcación |
| `0x12–0x13` | 1+1 | `CE 02`, `66 02`, luego `03 00` | sin determinar |
| `0x14–0x17` | 4 | — | **fecha y hora** |
| `0x18–0x1B` | 4 | todo cero | relleno |

### La codificación de tiempo

**No es epoch Unix.** ZKTeco trata todos los meses como de 31 días:

```
s = t % 60;  t /= 60;    mi = t % 60;  t /= 60;    h = t % 24;  t /= 24;
d = t % 31 + 1;  t /= 31; mo = t % 12 + 1;         año = t / 12 + 2000
```

Los 9 registros dan **24/08/2026 entre 15:11:43 y 16:04:46**, y el archivo se escribió a las
**16:05:06** — veinte segundos después del último. Eso confirma la fórmula y descarta que el
reloj esté desajustado. Leerlo como epoch Unix daría fechas plausibles y equivocadas, que es
la peor clase de error.

### El layout se deduce, no se supone

El lector **no lleva grabado el 8** del relleno. Prueba todos los desplazamientos posibles y
elige el que hace que el último campo decodifique a una fecha plausible **en todos los
registros**. Si ninguno lo consigue, rechaza el archivo entero.

⚠️ **La fecha por sí sola no basta para desempatar.** Con pocos registros hay
desplazamientos equivocados que también dan una fecha creíble: leer cuatro bytes **tres
posiciones antes** da `0x34000301` → 2027-02-23, tan plausible como la buena, y gana por ser
menor. El identificador entonces pasa de `2` a `33 554 432`: una marcación con la fecha
correcta atribuida a nadie. Por eso se exige además que la fecha quede **alineada a cuatro
bytes** —en un volcado de estructuras un `uint32` siempre lo está—, con una segunda pasada
sin esa condición por si algún firmware empaqueta distinto. Hay un test que fija justo este
caso.

### Salvaguardas

- **ID en cero → se rechaza.** En la muestra real los dos primeros registros lo tienen: son
  los que el equipo escribe para sí mismo. Atribuirlos a un trabajador sería inventar una
  marcación que después se paga.
- **Fecha imposible → se rechaza.** El empaquetado permite el 31 de febrero; que aparezca es
  la señal de que se leyó el campo equivocado.
- **Esquema desconocido → se rechaza el archivo entero**, con su motivo, en vez de forzar
  una interpretación.
- Se aplica la misma ventana de antigüedad (`DiasAntiguedadMaxima`) que en el texto.

### Limitaciones frente al texto

Dos cosas que este formato **no puede dar**, y que justifican seguir prefiriendo el `attlog`:

1. **Los ceros a la izquierda se pierden.** El ID viaja como entero, así que `0014` y `14`
   colapsan en `14`. El attlog de julio de 2026 trae los dos, y son **personas distintas**.
2. **Los cuatro bytes intermedios siguen sin significado confirmado.** Se leen pero **no
   deciden nada**, igual que en el TXT, donde el tipo viene siempre en cero y el
   emparejamiento lo resuelve el motor contra la ventana del turno. Para fijarlos haría
   falta un archivo de control —marcar con un ID conocido a una hora anotada y exportar el
   respaldo— y compararlo con esta muestra.

## El identificador está confirmado

GCA confirmó el padrón del equipo de prueba: **`2` y `3` son usuarios, `1` es el master**
(la cuenta de administrador del propio marcador). El parser extrae exactamente ese conjunto
`{1, 2, 3}`, que es la confirmación que faltaba — un campo mal elegido no produce el padrón
real por casualidad, sino números arbitrarios como los `51200`, `456` o `257` que devolvía
un intento anterior basado en probar offsets.

Hay además una corroboración interna, independiente de lo que dijo nadie: el registro del
master es **el único con un modo de verificación distinto**.

| ID | Quién | Registros | Modo de verificación |
|---|---|---|---|
| `0` | el propio equipo | 2 | `200` |
| `1` | **master** | 1 | `3` |
| `2` | usuario | 3 | `1` |
| `3` | usuario | 3 | `1` |

Que la fila señalada como master sea también la única con otro modo de verificación es una
segunda señal apuntando al mismo sitio.

⚠️ **El master no debe entrar al tareo, y no hace falta código para conseguirlo**: basta con
**no asignarle ese ID a ningún trabajador**. El padrón decide —el mecanismo que el sistema ya
tiene— y sus registros se reportan como ID sin asignar sin insertarse. Hay un test que lo
fija. Añadir una regla que descartara "el ID 1" sería peor: en otro equipo ese número puede
ser una persona.

## Alternativas evaluadas

| Opción | Veredicto |
|---|---|
| **Leer los dos formatos** | **Implementado.** El texto sin cambios; el binario con el parser y sus salvaguardas |
| SDK oficial (`zkemkeeper`) | Descartado: COM ActiveX de 32 bits, exige registrar DLL como administrador y compilar en x86. Choca con un `.exe` autocontenido de .NET 10 |
| Protocolo de red (UDP/TCP 4370) | **Aplazado, no descartado.** Quita el paso del USB y el error humano de origen; `IFuenteDeMarcaciones` ya existe para enchufarlo |
| Descifrado | **No aplica: el archivo no está cifrado** |

## Si algún día se conecta por red

La integración por protocolo nativo es una tercera implementación de
`IFuenteDeMarcaciones`; nada del motor de cálculo, la deduplicación ni la anulación de
cargas cambia. Datos del equipo, ya conocidos:

- Serie `VGU6251600671`, plataforma `ZAM70`, firmware `Ver3.0.15`.
- Protocolo documentado por ingeniería inversa en
  [adrobinoga/zk-protocol](https://github.com/adrobinoga/zk-protocol).
- La IP y la clave de comunicación van en configuración externa, **nunca versionadas**.

## Verificación de punta a punta

`Recursos/BKtransaction.dat` es el archivo real del equipo, versionado como muestra: no
contiene datos personales —nueve registros de una sesión de pruebas con los identificadores
1, 2 y 3, sin nombres, documentos ni plantillas biométricas—. Sobre él corre
`ImportacionRespaldoRealTests`, que va del binario a filas consultables:

| Comprobación | Resultado |
|---|---|
| Registros del archivo | 9 |
| Marcaciones insertadas en `marcacion_cruda` | **7**, atribuidas a los tres trabajadores |
| Descartadas (ID en cero, del propio equipo) | 2 |
| Reimportar el mismo archivo | 0 nuevas, 7 duplicadas |
| Master (ID 1) fuera del padrón | 6 insertadas; su registro se reporta como ID sin asignar |

La última fila cumple dos funciones a la vez: es la política del master y es la red de
seguridad contra un mapeo de campos equivocado. Si el identificador se leyera del byte que
no es, saldrían números que no corresponden a nadie y el sistema **no los inserta**. Ese
listado es lo primero que hay que mirar al cargar un respaldo de producción: si aparece
medio archivo ahí, el mapeo está mal.

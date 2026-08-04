# Juego de datos de prueba — julio 2026

Describe el contenido de `AEH2200760069_attlog.txt` (archivo simulado del ZKTeco G3) usado para
probar la ingesta y el motor de cálculo. **El TXT no se versiona**; este documento sí, porque es
lo que permite reproducirlo y escribir aserciones.

- Periodo: **01/07/2026 → 31/07/2026**
- 50 IDs de marcador · 2 509 marcaciones · orden cronológico estricto
- Las 24 líneas de la muestra original (IDs `2` y `3`, 01–06 de julio) se conservan **byte a byte**

## Formato

| Campo | Valor |
|---|---|
| Delimitador | TAB |
| Columnas | `ID · fecha-hora · 1 · 0 · 1 · 0` |
| ID | alineado a la derecha en **9 caracteres** con espacios (`     0014`) |
| Fecha-hora | `yyyy-MM-dd HH:mm:ss` |
| Tipo de marcación | siempre `0` — **el equipo no distingue entrada de salida** |
| Fin de línea | CRLF; **la última línea no lleva salto**, igual que la muestra real |

## Calendario del periodo

| | |
|---|---|
| Sábados | 04, 11, 18, 25 |
| Domingos | 05, 12, 19, 26 |
| Feriados (Fiestas Patrias) | **martes 28 y miércoles 29** |
| Rotación de turno de los obreros | **lunes 20** |

## Los 50 IDs

`DIU` 07:00→19:00 · `NOC` 19:00→07:00 · `SAB` 07:00→15:00 · `DOM1` 07:00→15:00 · `DOM2` 07:00→19:00

### Consulting Advisers — Planta Detergentes

| ID | Turno | Domingo | Incidencia sembrada |
|---|---|---|---|
| `2` | DIU | DOM2 | Trabaja feriados. Extras los lunes 06, 13, 20, 27 |
| `3` | DIU | DOM2 | Trabaja feriados. Extras los lunes 06, 13, 20, 27 |
| `5` | DIU | — | Puntual, sin incidencias (caso base limpio) |
| `6` | DIU | — | Tardanzas frecuentes (~55 % de los días) |
| `7` | DIU→NOC (20) | — | Extras 02, 21, 30 |
| `8` | DIU→NOC (20) | — | Tardanzas ocasionales |
| `9` | NOC→DIU (20) | — | Extras 07, 22 |
| `10` | NOC→DIU (20) | — | **Una sola marca el 16** |
| `11` | NOC | — | Trabaja feriados en turno nocturno |
| `12` | NOC | — | Extras 03, 10, 17, 24 (salidas ~09:30) |
| `0014` | DIU | — | **Vacaciones 06–11** (ID con ceros a la izquierda) |
| `15` | DIU | — | **Cesa el 15/07**: sin marcas desde el 16 |
| `16` | DIU | — | **Ingresa el 13/07**: sin marcas antes |

### Consulting Advisers — Planta 20 mil

| ID | Turno | Domingo | Incidencia sembrada |
|---|---|---|---|
| `21` | DIU | — | Tardanzas ocasionales |
| `22` | DIU | DOM1 | — |
| `23` | DIU | DOM1 | Extras el 14 |
| `0024` | DIU | — | **Olvida marcar la salida el 08 y el 23** |
| `25` | DIU→NOC (20) | — | Tardanzas ocasionales |
| `26` | NOC→DIU (20) | — | Extras el 27 |
| `27` | NOC | — | — |
| `28` | NOC | — | **Faltas 15 y 16** |
| `29` | DIU | — | **Faltas 09 y 22** + tardanzas frecuentes |
| `30` | DIU | — | **Marca duplicada** al entrar el 02, 17 y 30 |
| `31` | DIU | — | Extras en 9 días + trabaja feriados (caso de mayor costo) |
| `32` | DIU | — | **Descanso médico 20–24** |

### CA Outsourcing — Planta 01

| ID | Turno | Domingo | Incidencia sembrada |
|---|---|---|---|
| `41` | DIU L–V + **SAB** | — | — |
| `42` | DIU L–V + **SAB** | — | Extras el 08 |
| `43` | DIU L–V + **SAB** | — | Tardanzas frecuentes (~60 %) |
| `0044` | DIU L–V + **SAB** | — | **Una sola marca el sábado 11** |
| `45` | DIU (sábado de 12 h) | — | — |
| `46` | DIU | — | Extras 10, 24 |
| `47` | DIU→NOC (20) | — | — |
| `48` | NOC→DIU (20) | — | Extras el 13 |
| `49` | NOC | — | Trabaja feriados en turno nocturno |
| `50` | NOC | — | Extras 09, 23 |
| `51` | DIU | DOM2 | — |
| `52` | DIU | DOM1 | — |
| `53` | DIU | — | **Trabaja los feriados 28 y 29 con horas extras** |

> Los cuatro IDs con turno `SAB` están en Planta 01 a propósito: es la única planta donde el
> catálogo lo habilita. `45` y `46` trabajan el sábado en jornada de 12 h, como en la muestra real.

### CA Outsourcing — Planta Sulfonación

| ID | Turno | Domingo | Incidencia sembrada |
|---|---|---|---|
| `61` | NOC | — | Trabaja feriados |
| `62` | NOC | — | — |
| `63` | NOC | — | Extras 01, 08, 15, 22 |
| `0064` | NOC→DIU (20) | — | — |
| `65` | DIU→NOC (20) | — | **Una sola marca el 24** |
| `66` | DIU | — | — |
| `67` | DIU | — | **Marca duplicada** al entrar el 07 |
| `68` | DIU | DOM2 | Extras el 16 |
| `69` | DIU | — | **Salida antes de hora** el 03, 17 y 31 (~17:30) |
| `70` | DIU | — | Trabaja feriados, con extras el 28 |
| `71` | NOC | — | **Faltas las noches del 06 y 07** |
| `72` | DIU | — | — |

## Casos borde que cubre el archivo

| # | Caso | Dónde verificarlo |
|---|---|---|
| 1 | **Turno nocturno que cruza medianoche** | Cualquier ID `NOC`: entrada 18:5x del día *d*, salida 07:0x del día *d+1*. La jornada se imputa al día de inicio |
| 2 | **Turno nocturno abierto al inicio del periodo** | 01/07 antes de las 08:00 hay **15 salidas sin entrada**: son los turnos que empezaron el martes 30/06 |
| 3 | **Turno nocturno abierto al cierre del periodo** | Viernes 31/07 a las 18:5x hay entradas cuya salida cae el 01/08, fuera del archivo |
| 4 | **Nocturno de sábado que termina el domingo** | Sábados 04, 11, 18, 25: los `NOC` entran y salen el domingo por la mañana. Es la pregunta abierta n.º 8 de `01-requerimientos-negocio.md` |
| 5 | **Rotación de turno** | El lunes 20 los IDs `7 8 25 47 65` pasan de `DIU` a `NOC` y los IDs `9 10 26 48 0064` al revés. Resolver el turno por "el último turno del trabajador" da resultados incorrectos aquí |
| 6 | **Vigencia del ID de marcador** | Ningún ID se reasigna en el mes, pero `15` deja de marcar el 16 y `16` empieza el 13: sirven para probar los límites de vigencia |
| 7 | **Horas extras 25 % / 35 %** | Salidas entre las 20:00 y las 22:45 en los días marcados como extras |
| 8 | **Tardanzas** | Entradas después de las 07:04 (`6`, `43`, `29`) |
| 9 | **Salida antes de hora** | ID `69` el 03, 17 y 31 |
| 10 | **Faltas y ausencias justificadas** | `29` (2 días), `28` (2 noches), `71` (2 noches), `0014` (vacaciones), `32` (descanso médico) |
| 11 | **Marcación impar / olvido de salida** | `10` el 16, `0024` el 08 y 23, `0044` el 11, `65` el 24 |
| 12 | **Marca duplicada por doble pasada** | `30` el 02, 17 y 30; `67` el 07. Segundos de diferencia, mismo ID |
| 13 | **Feriado trabajado (sobretasa 100 %)** | 28 y 29 de julio: solo marcan `2 3 11 31 49 53 61 70`. El resto descansa |
| 14 | **Trabajo dominical** | Domingos 05, 12, 19, 26: `DOM2` (12 h) los IDs `2 3 51 68`; `DOM1` (8 h) los IDs `22 23 52` |
| 15 | **Turno especial de sábado** | `41 42 43 0044` salen 15:0x los sábados; el resto de Planta 01 cumple 12 h |
| 16 | **IDs con ceros a la izquierda** | `0014 0024 0044 0064`. Si el ID se lee como número, el cruce falla |
| 17 | **Idempotencia de la carga** | Volver a cargar el archivo no debe duplicar ninguna fila |
| 18 | **ID no registrado** | Registra solo 49 de los 50 y carga el archivo: las marcaciones del ID restante deben salir en el informe de errores, no abortar la carga |

## Cómo regenerarlo

```bash
python tools/generador-attlog/gen_attlog.py "C:/Users/Sergio/Documents/AEH2200760069_attlog.txt"
```

Es determinista: produce el mismo archivo byte a byte en cada ejecución. Para otro mes basta
cambiar `INICIO`, `FIN`, `ROTACION` y `FERIADOS`; para otra dotación, el bloque `w(...)` del
roster. Sin argumento escribe en la raíz del repositorio — recuerda que ahí el TXT **no debe
versionarse**.

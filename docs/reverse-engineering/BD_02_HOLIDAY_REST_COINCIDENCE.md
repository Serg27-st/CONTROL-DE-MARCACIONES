# BD-02 — Feriado nacional + descanso programado

- **Fecha:** 2026-10-06
- **Estado:** **CERRADA E IMPLEMENTADA ESTRUCTURALMENTE** por R-P1-05
- **Dependencias:** BD-01 cerrada; FER-NAC-01..08 vigentes
- **Alcance:** clasificación implementada y minutos físicos únicos. La valorización
  económica adicional del 1 de mayo queda explícitamente fuera por falta de modelo.

## 1. Principio rector

La programación, el calendario y la evidencia física son dimensiones distintas:

```text
programación = DescansoProgramado
calendario   = FeriadoNacional
trabajo      = intervalo físico delimitado por BD-01
```

La coincidencia conserva los atributos necesarios para auditoría, pero una cantidad física
no se duplica para representar dos etiquetas o una sobretasa.

## 2. Decisiones funcionales aprobadas

### BD02-D01 — feriado ordinario + descanso + sin trabajo

Un feriado nacional ordinario que coincide con `DescansoProgramado` y no se trabaja es un
solo día físico de descanso: cero minutos trabajados, cero HE100 y ninguna duplicación del
descanso. La remuneración ordinaria que corresponda no se representa inventando minutos.

### BD02-D02 — feriado ordinario + descanso + trabajo

BD-01 determina N minutos físicos. Económicamente se clasifican como
`FeriadoTrabajado`; no se genera además `DescansoSemanalTrabajado` sobre los mismos N.

```text
N minutos físicos → una base física N
```

El origen programático `DescansoProgramado` se conserva, pero no crea un segundo pago
independiente por descanso trabajado.

### BD02-D03 — valorización sin descanso sustitutorio

Conceptualmente existen:

1. remuneración ordinaria del feriado;
2. remuneración por el trabajo realizado;
3. sobretasa del 100 % sobre ese trabajo.

El tareo conserva N una sola vez. La liquidación valoriza la base del trabajo y el recargo;
no transforma la sobretasa en minutos físicos adicionales. La remuneración ordinaria del
feriado permanece separada y normalmente está contenida en la remuneración mensual.

### BD02-D04 — descanso sustitutorio

Cuando existe descanso sustitutorio no corresponde el adicional general de trabajo + 100 %.
El sistema futuro debe distinguir:

```text
FeriadoTrabajadoSinDescansoSustitutorio
FeriadoTrabajadoConDescansoSustitutorio
```

o una representación equivalente. El dato no existe actualmente; es un gap de modelo y
workflow separado que no impide modelar `DescansoProgramado`.

### BD02-D05 — feriado ordinario coincidente con descanso

Para cualquier feriado nacional distinto del 1 de mayo, la coincidencia trabajada aplica
la regla de `FeriadoTrabajado`. No se liquidan como conceptos independientes
`FeriadoTrabajado + DescansoSemanalTrabajado` sobre la misma base.

### BD02-D06 — excepción del 1 de mayo trabajado

El 1 de mayo no reutiliza automáticamente BD02-D05. Si coincide con descanso
semanal/programado y se trabaja, se conservan conceptualmente:

1. remuneración por descanso semanal;
2. remuneración por el feriado del 1 de mayo;
3. remuneración por el trabajo realizado;
4. sobretasa del 100 %.

Los N minutos delimitados por BD-01 siguen siendo una sola cantidad física. Los cuatro
componentes son económicos, no cuatro copias del tiempo.

### BD02-D07 — excepción del 1 de mayo no trabajado

Si el 1 de mayo coincide con descanso y no se trabaja, corresponden conceptualmente la
remuneración por descanso semanal y la remuneración por el feriado. El tareo registra cero
minutos trabajados y no inventa una jornada.

### BD02-D08 — cruce hacia feriado

Se conserva FER-NAC-05: una jornada iniciada en un día laborable no se reclasifica por
terminar dentro del feriado. El tramo posterior a medianoche conserva la jornada de inicio.

### BD02-D09 — jornada iniciada en feriado

Se conserva FER-NAC-06: una jornada que comienza durante el feriado se trata como trabajo
iniciado en feriado aunque termine en D+1, sin duplicar minutos físicos.

## 3. Precedencia de clasificación económica

La precedencia aprobada no elimina hechos; decide el concepto económico sobre una base:

| Coincidencia | Minutos físicos | Clasificación económica trabajada |
|---|---:|---|
| DescansoProgramado + domingo | N | HE100 una sola vez |
| DescansoProgramado + feriado ordinario | N | `FeriadoTrabajado`; no sumar `DescansoSemanalTrabajado` |
| DescansoProgramado + 1 de mayo | N | tratamiento especial BD02-D06, con N físico único |
| Cualquiera de las anteriores sin trabajo | 0 | conceptos remunerativos separados; no inventar minutos |

La regla del 1 de mayo exige que la resolución de calendario pueda diferenciarlo de un
feriado nacional ordinario. La forma física definitiva puede ser un tipo explícito o un
resultado semántico equivalente; no debe depender de sumar conceptos por accidente.

## 4. Relación con BD-01

BD-01 decide exclusivamente el intervalo:

```text
jornadas con turno primero
→ excluir reclamadas
→ primera marca válida en D
→ posible salida D+1 antes de la siguiente jornada
→ extremos si inequívocos
→ ambigüedad a validación
```

BD-02 recibe la cantidad física resultante y la clasifica. No modifica las marcas, no abre
otra ventana y no crea una segunda jornada.

## 5. Contratos de aceptación futuros

### ROT-FER-01 — descanso + feriado ordinario + trabajo

```gherkin
Dado un DescansoProgramado que coincide con un feriado nacional distinto del 1 de mayo
Y BD-01 delimitó N minutos trabajados
Cuando se clasifica y liquida el día
Entonces existen N minutos físicos una sola vez
Y el concepto trabajado es FeriadoTrabajado
Y no existe un segundo concepto DescansoSemanalTrabajado sobre N
Y Ordinarios, HE25 y HE35 son 0
```

La valorización sin sustitutorio aplica base del trabajo + 100 %. Con sustitutorio utilizará
el contrato BD02-D04 cuando exista el dato.

### ROT-FER-02 — descanso + 1 de mayo + trabajo

```gherkin
Dado un DescansoProgramado que coincide con el 1 de mayo
Y BD-01 delimitó N minutos trabajados
Cuando se clasifica y liquida el día
Entonces existen N minutos físicos una sola vez
Y se conservan los cuatro componentes económicos de BD02-D06
Y el sistema no degrada el caso al feriado ordinario BD02-D05
```

### ROT-FER-03 — descanso + feriado ordinario + sin trabajo

Cero minutos trabajados y cero HE100; no se duplican estados ni descanso físico.

### ROT-FER-04 — descanso + 1 de mayo + sin trabajo

Cero minutos trabajados; se conservan separadamente los dos componentes remunerativos de
BD02-D07.

### ROT-FER-05 — cruce desde día previo

Una jornada iniciada antes del feriado y terminada dentro de él no produce
`FeriadoTrabajado` por el cruce.

### ROT-FER-06 — inicio en feriado

Una jornada iniciada en feriado conserva esa clasificación hasta su salida en D+1, con una
sola cantidad física.

## 6. Gaps independientes

- Persistir y aprobar el descanso sustitutorio.
- Clasificar explícitamente el día no laborable compensable.

Estos gaps condicionan sus propios casos, pero no bloquean el modelo ni la implementación
base de `DescansoProgramado`.

## 7. Cierre

```text
BD-01 = CERRADA
BD-02 = CERRADA
ROT-DESC = IMPLEMENTADO
```

La implementación debe incluir la distinción del 1 de mayo y mantener separados el origen
programático, la clasificación de calendario y la cantidad física trabajada.

# R-CATTURN-FECHA-001 — Corrección de fechas de programación laboral

Fecha de cierre: 2 de noviembre de 2026  
Estado: **CERRADO**

## Causa raíz

`programacion-integral.xlsx` contiene celdas OpenXML de fecha con `t="d"`. La primera afectada es `Programacion!E2`, con valor bruto:

```text
2026-11-02T00:00:00.000Z
```

ClosedXML 0.105.1 intentaba interpretar ese valor durante `new XLWorkbook(contenido)` y lanzaba `FormatException` desde `XLWorkbook.SetCellValue`, antes de que el importador pudiera conocer la fila o validar el formato. También estaban afectadas `F2` y las fechas específicas ISO de las filas siguientes.

## Corrección

Se agregó `LectorExcelProgramacion`, adaptador exclusivo del importador de programación. Antes de delegar al lector común:

1. abre una copia en memoria del paquete `.xlsx`;
2. inspecciona únicamente celdas OpenXML con tipo fecha `t="d"`;
3. acepta fechas ISO y fecha-hora ISO únicamente cuando la hora es `00:00:00`;
4. conserva exactamente el día calendario escrito, sin conversión de zona horaria;
5. convierte el valor a texto ISO `yyyy-MM-dd` dentro de la copia temporal;
6. conserva valores inválidos como texto para que el validador informe fila, columna, valor y formatos esperados.

El archivo recibido no se modifica. El comportamiento global de ClosedXML y las demás importaciones tampoco cambia.

## Contrato de formatos

Admitidos:

- fecha nativa Excel;
- número serial Excel con formato de fecha;
- texto `yyyy-MM-dd`;
- texto `dd/MM/yyyy` y `d/M/yyyy`;
- OpenXML `t="d"` con ISO de medianoche, con o sin fracciones y zona;
- vacío en campos opcionales.

Rechazados:

- fechas inexistentes;
- formatos ambiguos como `01/02/03`;
- fecha-hora distinta de medianoche;
- intervalo cuyo fin es anterior al inicio;
- cualquier texto fuera de los formatos declarados.

Ejemplo de error controlado:

```text
Fila 2, columna 'Fecha específica': el valor '31/02/2026' no es una fecha válida. Use yyyy-MM-dd o dd/MM/yyyy, sin hora.
```

## Pruebas

Se agregaron `CATDATE-01` a `CATDATE-12` para fechas nativas, seriales/ISO, formato latino, vacíos, errores, ambigüedad, intervalos, preservación del día, solo lectura, persistencia, archivo original y ausencia de huérfanos.

Resultados:

- CATDATE + CATTURN: 23/23 verdes.
- Regresión dirigida de infraestructura CATTURN/CATDATE/CATREADY: 51/51 verdes.
- Regresión dirigida HTTP CATREADY: 7/7 verdes.
- Suite completa: 1125/1125 verdes, 0 fallos, 0 omitidos.
- Build Release: 0 errores, 0 advertencias.

## Validación funcional

Chrome/Playwright cargó `artifacts/CAT-DYN-004/programacion-integral.xlsx` original, sin normalización previa.

- previsualización: 8 filas, 14 fechas, 5 trabajadores, 1 descanso, 5 nocturnos y 0 conflictos;
- fechas mostradas: 02/11/2026 a 06/11/2026, sin desplazamiento;
- confirmación: 5 trabajadores, 14 fechas, 1 descanso y 2 turnos nuevos;
- persistencia: 5 asignaciones, 5 programaciones y 14 tramos;
- integridad: `integrity_check = ok`, `foreign_key_check = 0`;
- marcaciones y tareos: 0 modificaciones.

La lógica de conflictos no cambió. Los contratos CATTURN que bloquean duplicados, solapamientos y definiciones incompatibles continúan verdes.

Producción: **NO TOCADA**  
Migraciones: **NO**  
OP-03: **NO**  
Commit: **NO**

# Módulo Reportes

## Propósito

Consultar resultados persistidos, presentar resumen/excepciones, gestionar reintegros y producir el Excel final para operación y planilla.

## Responsabilidades

- Filtrar por periodo, planta, aparato y estructura organizativa.
- Leer `TareoDiario` y conceptos relacionados.
- Generar resumen, excepciones y tareo final.
- Aplicar alcance por planta.
- Exportar Excel seguro y consistente.
- Gestionar reintegros sin alterar evidencia cruda.

## Entidades

`RangoReporte`, `ReintegroTareo`, `TareoDiario` como fuente y DTO de resumen, excepciones y tareo final.

## Servicios

`ServicioReportes`, `ServicioReintegrosTareo`, `ExportadorReportesExcel`, `SanitizadorExcel` y `RangoReporte`.

## Reglas

- Los reportes consumen tareo persistido; no ejecutan el motor.
- La liquidación de `/Asistencia` expresa valor en horas equivalentes: categorías físicas exclusivas aportan base + recargo; nocturnidad, al ser superpuesta, aporta solo prima. Días no laborables trabajados no vuelven a sumar ordinarios históricos; el crédito de ausencia pagada sigue fuera de esta remediación.
- El archivo observado tiene cuatro hojas y 57 columnas A:BE.
- Valores iniciados con fórmulas deben sanitizarse.
- Filtros respetan alcance por planta y vigencia aplicable.
- Reemitir un periodo cerrado debería ser reproducible, pero hoy no todos los maestros están congelados.

## Flujo

Filtro → alcance/vigencia → consulta de tareo/reintegros → proyección → sanitización → Excel.

Véase [D12](../diagrams/D12_REPORT_GENERATION.md).

## Fuentes de verdad

- `TareoDiario` para resultados calculados.
- Reintegros aprobados para ajustes posteriores.
- Catálogos/vigencias para dimensiones; parte de ellos se resuelve con estado actual.

## Archivos críticos

- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioReportes.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioReintegrosTareo.cs`
- `src/ControlMarcacion.Infrastructure/Reportes/ExportadorReportesExcel.cs`
- `src/ControlMarcacion.Infrastructure/Reportes/SanitizadorExcel.cs`
- `src/ControlMarcacion.Web/Controllers/ReportesController.cs`

## Pruebas

Servicio, exportador final, generación extremo a extremo, filtros por aparato, alcance/vigencia, feriados, reintegros y sanitización.

## Problemas conocidos

- P1-01: doble base nocturna/no laborable — **RESUELTO** en la liquidación de `/Asistencia`; LIQ-01..04 y LIQ-HE-NOC-01 verdes.
- P1: reporte histórico mutable por datos maestros no congelados.
- P1: alerta de frescura incompleta puede permitir exportar tareo desactualizado.
- DOCUMENTACIÓN: comentarios de tres hojas o AO:BF son obsoletos frente a cuatro hojas y A:BE observados.

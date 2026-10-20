# Módulo Solicitudes y justificaciones

## Propósito

Gestionar solicitudes de ausencia, sustento, aprobación/rechazo, justificaciones y regularizaciones que alimentan el cálculo de tareo.

## Responsabilidades

- Crear, anular, sustentar, aprobar o rechazar solicitudes.
- Validar límites, detalle clínico y estados.
- Materializar justificaciones por trabajador/día.
- Registrar correcciones de entrada/salida.
- Recalcular el tareo afectado bajo control de periodo.

## Entidades

`SolicitudAusencia`, `Justificacion`, `TipoAusencia` y relaciones de regularización/incidencia.

## Servicios

`ServicioSolicitudes`, `ServicioJustificaciones`, controladores y DTO de ambos módulos.

## Reglas

- Una justificación vigente por trabajador y día.
- Sustento y aprobación respetan transiciones de estado.
- El detalle clínico tiene restricciones de privacidad y forma.
- Una justificación parcial no equivale a problema completamente resuelto.
- Las marcas crudas se preservan; una regularización aporta horas usadas para calcular.
- Una licencia o ausencia pagada de jornada completa acredita 480 minutos efectivos sin almuerzo, aunque DIU/NOC abarquen 622 minutos de reloj. No genera HE ni nocturnidad por sí sola.
- `JustificadaNoRemunerada` no acredita jornada; `PermisoParcial` conserva el tiempo realmente trabajado y solo cubre la incidencia dentro de su ventana.

## Flujo

Solicitud → sustento → decisión → justificación/regularización → recálculo → incidencia/reporte.

Véase [D11](../diagrams/D11_REQUEST_JUSTIFICATION_REINTEGRO.md).

## Fuentes de verdad

- Estado de solicitud para el trámite.
- Justificación vigente para el día.
- Tareo recalculado para el resultado operativo.
- Marcas crudas para evidencia original.

## Archivos críticos

- `src/ControlMarcacion.Domain/Solicitudes/SolicitudAusencia.cs`
- `src/ControlMarcacion.Domain/Justificaciones/Justificacion.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioSolicitudes.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioJustificaciones.cs`
- `src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs`

## Pruebas

Validadores de solicitudes, sustento/aprobación, flujo de regularización, unicidad diaria, paginación, pago al recalcular y casos INC-01..03.

R-P1-04 añade VIG-02/03: una justificación aprobada nueva invalida el tareo, y su
anulación o borrado físico también. El borrado no se infiere de las filas sobrevivientes:
se usa `registro_auditoria`, que conserva trabajador, fecha, estado previo y momento.

R-P1-08 añade AUS-PAG-01..05: VAC y LCG completas quedan en 480, DIU y NOC coinciden,
no aparecen HE ni nocturnidad automática y el permiso parcial no se convierte en una
jornada plana de 480. El código productivo ya cumplía; se reforzaron contratos y documentación.

## Problemas conocidos

- RESUELTO/R-P1-03: una justificación parcial conserva la incidencia independiente; la regularización incompleta permanece en la bandeja y la completa sale de pendientes.
- GAP/P2: política de aprobación parcial y composición de decisiones requiere definición completa.
- RIESGO: estados aprobados pueden coexistir con datos temporales omitidos en ciertos caminos.
- El borrado físico sigue prohibido como operación funcional aunque ya no pueda producir un falso “actualizado”.

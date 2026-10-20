# D05 — Modelo de dominio conceptual

```mermaid
erDiagram
    EMPRESA ||--o{ TRABAJADOR : emplea_actualmente
    PLANTA ||--o{ TRABAJADOR : ubica_actualmente
    AREA ||--o{ ASIGNACION_AREA : historiza
    TRABAJADOR ||--o{ ASIGNACION_AREA : posee
    TRABAJADOR ||--o{ PERIODO_LABORAL : posee
    EMPRESA ||--o{ PERIODO_LABORAL : contextualiza

    PLANTA ||--o{ EQUIPO_MARCADOR : contiene
    EQUIPO_MARCADOR ||--o{ MARCADOR : cataloga
    TRABAJADOR ||--o{ ASIGNACION_MARCADOR : recibe
    MARCADOR ||--o{ ASIGNACION_MARCADOR : se_asigna
    EQUIPO_MARCADOR ||--o{ IMPORTACION_MARCADOR : origina
    IMPORTACION_MARCADOR ||--o{ MARCACION_CRUDA : incorpora
    EQUIPO_MARCADOR ||--o{ IMPORTACION_PROVISIONAL : origina
    IMPORTACION_PROVISIONAL ||--o{ MARCACION_PROVISIONAL : incorpora
    IMPORTACION_PROVISIONAL ||--o{ ERROR_IMPORTACION_PROVISIONAL : registra
    EQUIPO_MARCADOR ||--o{ MARCACION_PROVISIONAL : identifica_origen
    ASIGNACION_MARCADOR }o--o{ MARCACION_PROVISIONAL : atribuye_en_consulta

    TRABAJADOR ||--o{ ASIGNACION_TURNO : posee
    TURNO ||--o{ ASIGNACION_TURNO : directo
    CUADRILLA ||--o{ ASIGNACION_TURNO : por_grupo
    TURNO ||--o{ VERSION_TURNO : historiza_horario
    PLANTA ||--o{ CUADRILLA : contiene
    CUADRILLA ||--o{ PROGRAMACION_CUADRILLA : programa
    PROGRAMACION_CUADRILLA ||--|{ TRAMO_ROTACION : ordena
    TURNO ||--o{ TRAMO_ROTACION : aplica

    TRABAJADOR ||--o{ TAREO_DIARIO : produce
    TAREO_DIARIO }o--o{ MARCACION_CRUDA : deriva_de
    TRABAJADOR ||--o{ SOLICITUD_AUSENCIA : solicita
    SOLICITUD_AUSENCIA ||--o{ JUSTIFICACION : genera_al_aprobar
    TRABAJADOR ||--o{ JUSTIFICACION : posee
    JUSTIFICACION }o--|| TAREO_DIARIO : modifica_calculo
    PERIODO ||--o{ TAREO_DIARIO : agrupa_por_fecha
    TRABAJADOR ||--o{ REINTEGRO_TAREO : ajusta_periodo
    PERIODO ||--o{ REINTEGRO_TAREO : recibe
```

Precisiones:

- `Jornada` no es una entidad persistida; es un concepto calculado alrededor de trabajador, fecha, turno resuelto, ventana y marcaciones.
- La relación visual entre `TareoDiario` y `MarcacionCruda` es conceptual: el resultado guarda horas de entrada/salida, no una tabla puente de trazabilidad a cada marca.
- La pertenencia histórica a planta no tiene una entidad equivalente a `AsignacionArea` en el modelo observado.
- `MarcacionProvisional` no guarda trabajador: la relación con `AsignacionMarcador` es conceptual y se resuelve por equipo, ID y fecha durante la consulta.
- Las tres entidades provisionales son una rama separada y no alimentan `MarcacionCruda` ni `TareoDiario`.

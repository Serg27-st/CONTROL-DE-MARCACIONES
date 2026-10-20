-- P1-DATA-PERIODOLABORAL / Fase 3B
-- Inventario reproducible. Debe ejecutarse con sqlite3 -readonly.
-- No contiene UPDATE, DELETE, INSERT persistente ni DDL persistente.

.headers on
.mode column

SELECT 'integrity_check' AS metrica, integrity_check AS valor
FROM pragma_integrity_check;

SELECT 'foreign_key_violations' AS metrica, COUNT(*) AS valor
FROM pragma_foreign_key_check;

SELECT 'trabajadores' AS metrica, COUNT(*) AS valor FROM trabajador
UNION ALL SELECT 'periodos', COUNT(*) FROM periodo_laboral
UNION ALL SELECT 'periodos_abiertos', COUNT(*) FROM periodo_laboral WHERE fecha_cese IS NULL
UNION ALL SELECT 'trabajadores_sin_periodo', COUNT(*)
  FROM trabajador t WHERE NOT EXISTS (SELECT 1 FROM periodo_laboral p WHERE p.trabajador_id=t.id)
UNION ALL SELECT 'trabajadores_un_periodo', COUNT(*) FROM (
  SELECT trabajador_id FROM periodo_laboral GROUP BY trabajador_id HAVING COUNT(*)=1)
UNION ALL SELECT 'trabajadores_mas_de_un_periodo', COUNT(*) FROM (
  SELECT trabajador_id FROM periodo_laboral GROUP BY trabajador_id HAVING COUNT(*)>1)
UNION ALL SELECT 'trabajadores_mas_de_un_abierto', COUNT(*) FROM (
  SELECT trabajador_id FROM periodo_laboral WHERE fecha_cese IS NULL
  GROUP BY trabajador_id HAVING COUNT(*)>1)
UNION ALL SELECT 'fechas_invalidas', COUNT(*) FROM periodo_laboral
  WHERE fecha_cese IS NOT NULL AND fecha_cese < fecha_ingreso
UNION ALL SELECT 'trabajadores_sin_planta_actual', COUNT(*) FROM trabajador t
  WHERE t.planta_id IS NULL OR NOT EXISTS (SELECT 1 FROM planta p WHERE p.id=t.planta_id)
UNION ALL SELECT 'plantas_actuales_distintas', COUNT(DISTINCT planta_id) FROM trabajador
UNION ALL SELECT 'plantas_catalogo', COUNT(*) FROM planta;

-- Igualdad semantica deliberada. Metadatos de auditoria no forman parte de la
-- semantica del vinculo; observacion se normaliza solo con TRIM/NULL vacio.
WITH grupos AS (
  SELECT trabajador_id, empresa_id, fecha_ingreso,
         COALESCE(fecha_cese,'') fecha_cese_norm,
         COALESCE(motivo_cese,'') motivo_cese_norm,
         COALESCE(TRIM(observacion_cese),'') observacion_cese_norm,
         activo, COUNT(*) cantidad
  FROM periodo_laboral
  GROUP BY trabajador_id, empresa_id, fecha_ingreso,
           COALESCE(fecha_cese,''), COALESCE(motivo_cese,''),
           COALESCE(TRIM(observacion_cese),''), activo
  HAVING COUNT(*) > 1
)
SELECT COUNT(*) AS grupos_duplicados_exactos,
       COALESCE(SUM(cantidad),0) AS filas_involucradas,
       COALESCE(SUM(cantidad-1),0) AS filas_redundantes
FROM grupos;

-- Todos los pares solapados, antes de deduplicar.
SELECT COUNT(*) AS pares_solapados
FROM periodo_laboral a
JOIN periodo_laboral b
  ON b.trabajador_id=a.trabajador_id AND b.id>a.id
WHERE a.fecha_ingreso <= COALESCE(b.fecha_cese,'9999-12-31')
  AND b.fecha_ingreso <= COALESCE(a.fecha_cese,'9999-12-31');

-- Huecos entre periodos ordenados. En SQLite julianday admite TEXT ISO.
WITH secuencia AS (
  SELECT p.*,
         LAG(fecha_cese) OVER (PARTITION BY trabajador_id ORDER BY fecha_ingreso,id) fin_previo
  FROM periodo_laboral p
)
SELECT COUNT(*) AS huecos
FROM secuencia
WHERE fin_previo IS NOT NULL AND julianday(fecha_ingreso)-julianday(fin_previo)>1;

-- Modelo que quedaria tras conservar la fila original (creado_en, luego id).
WITH ranked AS (
  SELECT p.*,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id,empresa_id,fecha_ingreso,
                        COALESCE(fecha_cese,''),COALESCE(motivo_cese,''),
                        COALESCE(TRIM(observacion_cese),''),activo
           ORDER BY creado_en,id) rn
  FROM periodo_laboral p
), canon AS (SELECT * FROM ranked WHERE rn=1)
SELECT
  COUNT(*) AS periodos_canonicos,
  SUM(CASE WHEN t.fecha_ingreso<>c.fecha_ingreso THEN 1 ELSE 0 END) AS fecha_ingreso_diverge,
  SUM(CASE WHEN COALESCE(t.fecha_cese,'')<>COALESCE(c.fecha_cese,'') THEN 1 ELSE 0 END) AS fecha_cese_diverge,
  SUM(CASE WHEN t.empresa_id<>c.empresa_id THEN 1 ELSE 0 END) AS empresa_diverge,
  SUM(CASE WHEN t.activo<>c.activo THEN 1 ELSE 0 END) AS activo_diverge
FROM canon c JOIN trabajador t ON t.id=c.trabajador_id;

-- Evidencia temporal de planta. Nivel A exige una unica planta explicita que
-- cubra todo el periodo; no usa Trabajador.PlantaId para decidir.
WITH ranked AS (
  SELECT p.*,
         ROW_NUMBER() OVER (
           PARTITION BY trabajador_id,empresa_id,fecha_ingreso,
                        COALESCE(fecha_cese,''),COALESCE(motivo_cese,''),
                        COALESCE(TRIM(observacion_cese),''),activo
           ORDER BY creado_en,id) rn
  FROM periodo_laboral p
), canon AS (SELECT * FROM ranked WHERE rn=1),
evidencia AS (
  SELECT c.id periodo_id,a.planta_id,'AREA' fuente
  FROM canon c JOIN asignacion_area aa ON aa.trabajador_id=c.trabajador_id
  JOIN area a ON a.id=aa.area_id
  WHERE aa.fecha_inicio<=c.fecha_ingreso
    AND (c.fecha_cese IS NULL AND aa.fecha_fin IS NULL
         OR c.fecha_cese IS NOT NULL AND COALESCE(aa.fecha_fin,'9999-12-31')>=c.fecha_cese)
  UNION ALL
  SELECT c.id,em.planta_id,'MARCADOR'
  FROM canon c JOIN asignacion_marcador am ON am.trabajador_id=c.trabajador_id
  JOIN equipo_marcador em ON em.id=am.equipo_id
  WHERE am.fecha_inicio<=c.fecha_ingreso
    AND (c.fecha_cese IS NULL AND am.fecha_fin IS NULL
         OR c.fecha_cese IS NOT NULL AND COALESCE(am.fecha_fin,'9999-12-31')>=c.fecha_cese)
), resumen AS (
  SELECT c.id,
         COUNT(DISTINCT e.planta_id) plantas,
         MAX(e.planta_id) planta_deterministica
  FROM canon c LEFT JOIN evidencia e ON e.periodo_id=c.id
  GROUP BY c.id
)
SELECT
  SUM(plantas=1) AS nivel_a,
  0 AS nivel_b,
  0 AS nivel_c,
  SUM(plantas=0 OR plantas>1) AS nivel_d
FROM resumen;

